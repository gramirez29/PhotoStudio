using System.Globalization;
using System.Net;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Storage;

namespace PhotoStudio.Infrastructure.Storage;

/// <summary>
/// Object storage over the S3 API, used with Cloudflare R2 in production and with a local S3-compatible server in development.
/// Signed addresses are computed locally (no request is made); only reading the size of an object and deleting go to the network.
/// Failures are reported as <see cref="StorageUnavailableException"/> without the key, the address or the response, which could echo them.
/// </summary>
public sealed partial class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly Protocol _protocol;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<S3ObjectStorage> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ObjectStorage"/> class.
    /// </summary>
    /// <param name="options">Settings of the storage.</param>
    /// <param name="timeProvider">Clock abstraction, so the expiry of the addresses is testable.</param>
    /// <param name="logger">Logger.</param>
    public S3ObjectStorage(ObjectStorageOptions options, TimeProvider timeProvider, ILogger<S3ObjectStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint.ToString().TrimEnd('/'),
            ForcePathStyle = true,
            // R2 signs with the region "auto".
            AuthenticationRegion = "auto",
            // The SDK sends integrity checksums by default; R2 and the local servers only want them where the API requires them.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            MaxErrorRetry = 2,
            Timeout = TimeSpan.FromSeconds(15),
        };

        _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), config);
        _bucket = options.Bucket;
        // Signed addresses follow the scheme of the storage: https for R2, and plain http only for the emulator on this machine.
        _protocol = options.Endpoint.Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PresignedUrl> CreateUploadUrlAsync(string key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ObjectKey.EnsureValid(key, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        EnsureLifetime(lifetime, ObjectStorageLimits.MaxUploadLifetime);

        var expiresAt = _timeProvider.GetUtcNow().Add(lifetime);
        var url = await SignAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Protocol = _protocol,
            Expires = expiresAt.UtcDateTime,
            ContentType = contentType,
        });

        return new PresignedUrl(url, expiresAt, new Dictionary<string, string> { ["Content-Type"] = contentType });
    }

    /// <inheritdoc />
    public async Task<PresignedUrl> CreateDownloadUrlAsync(string key, TimeSpan lifetime, string? downloadFileName, CancellationToken cancellationToken)
    {
        ObjectKey.EnsureValid(key, nameof(key));
        EnsureLifetime(lifetime, ObjectStorageLimits.MaxDownloadLifetime);

        var expiresAt = _timeProvider.GetUtcNow().Add(lifetime);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = _protocol,
            Expires = expiresAt.UtcDateTime,
        };
        if (!string.IsNullOrWhiteSpace(downloadFileName))
        {
            request.ResponseHeaderOverrides.ContentDisposition = BuildContentDisposition(downloadFileName);
        }

        return new PresignedUrl(await SignAsync(request), expiresAt, new Dictionary<string, string>());
    }

    /// <inheritdoc />
    public async Task<StoredObjectInfo?> GetInfoAsync(string key, CancellationToken cancellationToken)
    {
        ObjectKey.EnsureValid(key, nameof(key));

        try
        {
            var metadata = await _client.GetObjectMetadataAsync(_bucket, key, cancellationToken);
            return new StoredObjectInfo(metadata.ContentLength, metadata.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception exception) when (exception is AmazonServiceException or AmazonClientException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw Unavailable("read the size of an object", exception);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ObjectKey.EnsureValid(key, nameof(key));

        try
        {
            await _client.DeleteObjectAsync(_bucket, key, cancellationToken);
        }
        catch (Exception exception) when (exception is AmazonServiceException or AmazonClientException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw Unavailable("delete an object", exception);
        }
    }

    /// <inheritdoc />
    public async Task DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            ObjectKey.EnsureValid(key, nameof(keys));
        }

        foreach (var batch in keys.Distinct().Chunk(ObjectStorageLimits.MaxKeysPerDelete))
        {
            try
            {
                var response = await _client.DeleteObjectsAsync(
                    new DeleteObjectsRequest
                    {
                        BucketName = _bucket,
                        Quiet = true,
                        Objects = [.. batch.Select(key => new KeyVersion { Key = key })],
                    },
                    cancellationToken);
                if (response.DeleteErrors is { Count: > 0 } errors)
                {
                    throw Unavailable($"delete {errors.Count} of {batch.Length} objects", null);
                }
            }
            catch (Exception exception) when (exception is AmazonServiceException or AmazonClientException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw Unavailable("delete objects", exception);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    /// <summary>
    /// Builds the value of the <c>Content-Disposition</c> header that makes the browser save the file, with a safe name: the name
    /// loses path separators, quotes, semicolons and control characters, and non-ASCII letters go in the RFC 5987 form.
    /// </summary>
    /// <param name="fileName">The name the file should have when saved.</param>
    /// <returns>The header value.</returns>
    public static string BuildContentDisposition(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var clean = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            if (char.IsControl(character) || character is '"' or '\\' or '/' or ';' or '%')
            {
                clean.Append('_');
            }
            else
            {
                clean.Append(character);
            }
        }

        var name = clean.ToString().Trim();
        if (name.Length == 0)
        {
            name = "download";
        }

        var ascii = new string([.. name.Select(character => character is >= ' ' and <= '~' ? character : '_')]);
        return string.Create(CultureInfo.InvariantCulture, $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(name)}");
    }

    /// <summary>
    /// Refuses a lifetime that is not positive or is longer than the limit.
    /// </summary>
    /// <param name="lifetime">The requested lifetime.</param>
    /// <param name="maximum">The longest allowed.</param>
    /// <exception cref="ArgumentOutOfRangeException">When the lifetime is out of bounds.</exception>
    private static void EnsureLifetime(TimeSpan lifetime, TimeSpan maximum)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, $"The lifetime must be more than zero and at most {maximum}.");
        }
    }

    /// <summary>
    /// Signs a request locally.
    /// </summary>
    /// <param name="request">What to sign.</param>
    /// <returns>The signed address.</returns>
    private async Task<Uri> SignAsync(GetPreSignedUrlRequest request) => new(await _client.GetPreSignedURLAsync(request));

    /// <summary>
    /// Logs a failure of the storage, without the key or the response, and builds the exception the caller gets.
    /// </summary>
    /// <param name="operation">What was being done.</param>
    /// <param name="inner">The underlying failure.</param>
    /// <returns>The exception to throw.</returns>
    private StorageUnavailableException Unavailable(string operation, Exception? inner)
    {
        LogStorageFailure(_logger, operation, inner?.GetType().Name ?? "none", inner);
        return new StorageUnavailableException($"The object storage could not {operation}.", inner);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The object storage failed to {Operation} ({FailureType}).")]
    private static partial void LogStorageFailure(ILogger logger, string operation, string failureType, Exception? exception);
}
