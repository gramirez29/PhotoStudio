using Amazon.Runtime;
using Amazon.S3;
using PhotoStudio.Infrastructure.Storage;

namespace PhotoStudio.Infrastructure.IntegrationTests.Storage;

/// <summary>
/// Provides the object storage for the tests: the S3-compatible server of <c>docker-compose.yml</c> by default, or any other
/// (for example a test bucket of R2) through the <c>STORAGE_TEST_*</c> variables. Each test class uses its own key prefix and
/// cleans it. When no server answers, <see cref="IsAvailable"/> is false and the tests skip themselves.
/// </summary>
public sealed class StorageFixture : IAsyncLifetime
{
    private const string DefaultEndpoint = "http://127.0.0.1:9020";
    private const string DefaultBucket = "photostudio-test";
    private const string DefaultAccessKeyId = "dev-storage-access-key";
    private const string DefaultSecretAccessKey = "dev-storage-secret-key-0123456789";

    /// <summary>
    /// Gets a value indicating whether the storage answered and the bucket exists.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Gets the settings of the storage under test.
    /// </summary>
    public ObjectStorageOptions Options { get; private set; } = ObjectStorageOptions.Create(DefaultEndpoint, DefaultBucket, DefaultAccessKeyId, DefaultSecretAccessKey);

    /// <summary>
    /// Connects to the storage and makes sure the bucket exists.
    /// </summary>
    /// <returns>A task that completes when the attempt finishes.</returns>
    public async ValueTask InitializeAsync()
    {
        Options = ObjectStorageOptions.Create(
            Environment.GetEnvironmentVariable("STORAGE_TEST_ENDPOINT") ?? DefaultEndpoint,
            Environment.GetEnvironmentVariable("STORAGE_TEST_BUCKET") ?? DefaultBucket,
            Environment.GetEnvironmentVariable("STORAGE_TEST_ACCESS_KEY_ID") ?? DefaultAccessKeyId,
            Environment.GetEnvironmentVariable("STORAGE_TEST_SECRET_ACCESS_KEY") ?? DefaultSecretAccessKey);

        using var client = new AmazonS3Client(
            new BasicAWSCredentials(Options.AccessKeyId, Options.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = Options.Endpoint.ToString().TrimEnd('/'),
                ForcePathStyle = true,
                AuthenticationRegion = "auto",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                MaxErrorRetry = 0,
                Timeout = TimeSpan.FromSeconds(3),
            });

        try
        {
            var buckets = await client.ListBucketsAsync();
            if (buckets.Buckets?.Any(bucket => bucket.BucketName == Options.Bucket) != true)
            {
                await client.PutBucketAsync(Options.Bucket);
            }

            IsAvailable = true;
        }
        catch (Exception exception) when (exception is AmazonServiceException or AmazonClientException or HttpRequestException or TaskCanceledException)
        {
            IsAvailable = false;
        }
    }

    /// <summary>
    /// Nothing to release: the bucket is kept between runs and every test cleans its own keys.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
