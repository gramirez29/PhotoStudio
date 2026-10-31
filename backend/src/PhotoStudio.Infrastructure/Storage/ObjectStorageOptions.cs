namespace PhotoStudio.Infrastructure.Storage;

/// <summary>
/// Settings of the object storage, read from environment variables (Railway service variables in production). The access key
/// and the secret are secrets and must never be committed or logged.
/// </summary>
/// <param name="Endpoint">S3 address of the storage: `https://&lt;account&gt;.r2.cloudflarestorage.com` for R2.</param>
/// <param name="Bucket">Name of the private bucket.</param>
/// <param name="AccessKeyId">Access key id of a token limited to that bucket.</param>
/// <param name="SecretAccessKey">Secret of that token.</param>
public sealed record ObjectStorageOptions(Uri Endpoint, string Bucket, string AccessKeyId, string SecretAccessKey)
{
    /// <summary>Characters of a Cloudflare account id.</summary>
    private const int AccountIdLength = 32;

    /// <summary>Characters of the Access Key ID of an R2 token.</summary>
    private const int AccessKeyIdLength = 32;

    /// <summary>Characters of the Secret Access Key of an R2 token.</summary>
    private const int SecretLength = 64;

    /// <summary>Environment variable with the Cloudflare account id, from which the R2 address is built.</summary>
    public const string AccountIdVariable = "R2_ACCOUNT_ID";

    /// <summary>Environment variable with the S3 address, which replaces the one built from the account id (for the local emulator).</summary>
    public const string EndpointVariable = "R2_ENDPOINT";

    /// <summary>Environment variable with the bucket name.</summary>
    public const string BucketVariable = "R2_BUCKET";

    /// <summary>Environment variable with the access key id.</summary>
    public const string AccessKeyIdVariable = "R2_ACCESS_KEY_ID";

    /// <summary>Environment variable with the secret access key.</summary>
    public const string SecretAccessKeyVariable = "R2_SECRET_ACCESS_KEY";

    /// <summary>
    /// Reads the options from the process environment. Either nothing is set (the storage is off) or everything is: a half
    /// configured storage is a deployment mistake and fails loudly at startup instead of silently not working.
    /// </summary>
    /// <returns>The options, or <see langword="null"/> when none of the variables is set.</returns>
    /// <exception cref="InvalidOperationException">When some variables are set and others are missing, the address is not safe, or the credentials of R2 do not have the shape R2 issues.</exception>
    public static ObjectStorageOptions? FromEnvironment()
    {
        var accountId = Read(AccountIdVariable);
        var endpoint = Read(EndpointVariable);
        var bucket = Read(BucketVariable);
        var accessKeyId = Read(AccessKeyIdVariable);
        var secret = Read(SecretAccessKeyVariable);

        if (accountId is null && endpoint is null && bucket is null && accessKeyId is null && secret is null)
        {
            return null;
        }

        var missing = new List<string>();
        if (accountId is null && endpoint is null)
        {
            missing.Add($"{AccountIdVariable} (or {EndpointVariable})");
        }

        if (bucket is null)
        {
            missing.Add(BucketVariable);
        }

        if (accessKeyId is null)
        {
            missing.Add(AccessKeyIdVariable);
        }

        if (secret is null)
        {
            missing.Add(SecretAccessKeyVariable);
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"The object storage is partly configured; these variables are missing: {string.Join(", ", missing)}.");
        }

        if (endpoint is null)
        {
            // Against the real R2 the three values have a fixed shape. The usual mistakes (the whole address in the account id, the
            // value of the API token in the key id) otherwise only show up as an opaque 400 in the browser, so they fail here, naming
            // the variable and never its value. A custom address is the local emulator, whose development keys have any shape.
            RequireHex(AccountIdVariable, accountId!, AccountIdLength, "the Cloudflare account id, without the address");
            RequireHex(AccessKeyIdVariable, accessKeyId!, AccessKeyIdLength, "the Access Key ID shown when the token was created, not the token value");
            RequireHex(SecretAccessKeyVariable, secret!, SecretLength, "the Secret Access Key shown when the token was created");
        }

        return Create(endpoint ?? $"https://{accountId}.r2.cloudflarestorage.com", bucket!, accessKeyId!, secret!);
    }

    /// <summary>
    /// Builds the options after checking them.
    /// </summary>
    /// <param name="endpoint">S3 address.</param>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="accessKeyId">Access key id.</param>
    /// <param name="secretAccessKey">Secret access key.</param>
    /// <returns>The options.</returns>
    /// <exception cref="InvalidOperationException">When the address is not absolute, or is plain http for a host that is not this machine (the keys would travel in the clear), or the bucket name is not a valid one.</exception>
    public static ObjectStorageOptions Create(string endpoint, string bucket, string accessKeyId, string secretAccessKey)
    {
        if (!Uri.TryCreate(endpoint.TrimEnd('/'), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"'{EndpointVariable}' must be an absolute http or https address.");
        }

        if (uri.Scheme == "http" && !uri.IsLoopback)
        {
            throw new InvalidOperationException($"The object storage address must use https unless it is on this machine; got '{uri.Host}'.");
        }

        if (!IsValidBucketName(bucket))
        {
            throw new InvalidOperationException($"'{BucketVariable}' is not a valid bucket name (3 to 63 lowercase letters, digits and hyphens).");
        }

        return new ObjectStorageOptions(uri, bucket, accessKeyId, secretAccessKey);
    }

    /// <summary>
    /// Checks that a value is a hexadecimal string of an exact length. The value is never part of the message.
    /// </summary>
    /// <param name="variable">Name of the variable, for the message.</param>
    /// <param name="value">The value.</param>
    /// <param name="length">Characters it must have.</param>
    /// <param name="meaning">What the variable must hold, for the message.</param>
    /// <exception cref="InvalidOperationException">When the value is not hexadecimal or does not have that length.</exception>
    private static void RequireHex(string variable, string value, int length, string meaning)
    {
        if (value.Length != length || !value.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException($"'{variable}' must be {length} hexadecimal characters: {meaning}. It has {value.Length} characters.");
        }
    }

    /// <summary>
    /// Indicates whether a name is a valid bucket name: 3 to 63 lowercase letters, digits and hyphens, not starting or ending with a hyphen.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <returns><see langword="true"/> when it is valid.</returns>
    private static bool IsValidBucketName(string name) =>
        name.Length is >= 3 and <= 63
        && name[0] != '-'
        && name[^1] != '-'
        && name.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    /// <summary>
    /// Reads one variable.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <returns>Its trimmed value, or <see langword="null"/> when it is missing or blank.</returns>
    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
