using PhotoStudio.Infrastructure.Storage;

namespace PhotoStudio.Infrastructure.IntegrationTests.Storage;

/// <summary>
/// Tests of <see cref="ObjectStorageOptions"/>: how the storage is configured and what it refuses.
/// </summary>
[Collection(EnvironmentCollection.Name)]
public sealed class ObjectStorageOptionsTests : IDisposable
{
    private static readonly string[] Variables =
    [
        ObjectStorageOptions.AccountIdVariable,
        ObjectStorageOptions.EndpointVariable,
        ObjectStorageOptions.BucketVariable,
        ObjectStorageOptions.AccessKeyIdVariable,
        ObjectStorageOptions.SecretAccessKeyVariable,
    ];

    /// <summary>A well formed Cloudflare account id, which has the same shape as an access key id.</summary>
    private const string AccountId = "0123456789abcdef0123456789abcdef";

    /// <summary>A well formed secret access key.</summary>
    private const string Secret = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly Dictionary<string, string?> _saved = Variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStorageOptionsTests"/> class, clearing the variables.
    /// </summary>
    public ObjectStorageOptionsTests()
    {
        foreach (var name in Variables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    /// <summary>
    /// Puts the variables back as they were.
    /// </summary>
    public void Dispose()
    {
        foreach (var (name, value) in _saved)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <summary>
    /// With no variable set the storage is off, not an error: the process starts and the storage answers "unavailable".
    /// </summary>
    [Fact]
    public void FromEnvironment_WithNothingSet_ReturnsNull() => ObjectStorageOptions.FromEnvironment().ShouldBeNull();

    /// <summary>
    /// The R2 address is built from the account id.
    /// </summary>
    [Fact]
    public void FromEnvironment_BuildsTheR2AddressFromTheAccountId()
    {
        Set(account: AccountId, bucket: "photostudio-media", key: AccountId, secret: Secret);

        var options = ObjectStorageOptions.FromEnvironment();

        options.ShouldNotBeNull();
        options.Endpoint.ToString().ShouldBe($"https://{AccountId}.r2.cloudflarestorage.com/");
        options.Bucket.ShouldBe("photostudio-media");
    }

    /// <summary>
    /// An explicit address replaces the one built from the account id, for the local emulator.
    /// </summary>
    [Fact]
    public void FromEnvironment_AnExplicitAddressWins()
    {
        Set(account: "abc123", endpoint: "http://127.0.0.1:9020/", bucket: "photostudio-dev", key: "key", secret: "secret");

        ObjectStorageOptions.FromEnvironment()!.Endpoint.ToString().ShouldBe("http://127.0.0.1:9020/");
    }

    /// <summary>
    /// A storage configured in part fails loudly, naming what is missing and never the values.
    /// </summary>
    [Fact]
    public void FromEnvironment_WithSomeVariablesMissing_FailsNamingThem()
    {
        Set(account: AccountId, bucket: "photostudio-media", secret: "top-secret-value");

        var error = Should.Throw<InvalidOperationException>(() => ObjectStorageOptions.FromEnvironment());

        error.Message.ShouldContain(ObjectStorageOptions.AccessKeyIdVariable);
        error.Message.ShouldNotContain("top-secret-value");
    }

    /// <summary>
    /// The usual mistakes with the credentials of R2 stop the start and name the variable, never its value: the whole address in the
    /// account id, and the value of the API token (<c>cfat_…</c>) in the key id.
    /// </summary>
    /// <param name="variable">Variable that is wrong.</param>
    /// <param name="account">Account id.</param>
    /// <param name="key">Access key id.</param>
    /// <param name="secret">Secret access key.</param>
    [Theory]
    [InlineData(ObjectStorageOptions.AccountIdVariable, "https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com", AccountId, Secret)]
    [InlineData(ObjectStorageOptions.AccountIdVariable, "0123456789abcdef", AccountId, Secret)]
    [InlineData(ObjectStorageOptions.AccessKeyIdVariable, AccountId, "cfat_NotARealTokenValueJustAnExampleOfItsShape00", Secret)]
    [InlineData(ObjectStorageOptions.SecretAccessKeyVariable, AccountId, AccountId, "short-secret")]
    public void FromEnvironment_WithCredentialsOfTheWrongShape_FailsNamingTheVariableButNotTheValue(string variable, string account, string key, string secret)
    {
        Set(account: account, bucket: "photostudio-media", key: key, secret: secret);

        var error = Should.Throw<InvalidOperationException>(() => ObjectStorageOptions.FromEnvironment());

        error.Message.ShouldContain($"'{variable}'");
        error.Message.ShouldNotContain("cfat_");
        error.Message.ShouldNotContain("short-secret");
    }

    /// <summary>
    /// A custom address is the local emulator, whose development keys have any shape.
    /// </summary>
    [Fact]
    public void FromEnvironment_WithACustomAddress_DoesNotRequireTheShapeOfR2()
    {
        Set(endpoint: "http://127.0.0.1:9020", bucket: "photostudio-dev", key: "dev-key", secret: "dev-secret");

        ObjectStorageOptions.FromEnvironment().ShouldNotBeNull();
    }

    /// <summary>
    /// The keys never travel over plain http to another machine.
    /// </summary>
    /// <param name="endpoint">The address.</param>
    [Theory]
    [InlineData("http://storage.example.com")]
    [InlineData("ftp://storage.example.com")]
    [InlineData("not an address")]
    public void Create_RefusesAnUnsafeAddress(string endpoint) =>
        Should.Throw<InvalidOperationException>(() => ObjectStorageOptions.Create(endpoint, "photostudio-media", "key", "secret"));

    /// <summary>
    /// Plain http is fine on this machine, which is where the local emulator is.
    /// </summary>
    /// <param name="endpoint">The address.</param>
    [Theory]
    [InlineData("http://127.0.0.1:9020")]
    [InlineData("http://localhost:9020")]
    [InlineData("https://abc123.r2.cloudflarestorage.com")]
    public void Create_AcceptsHttpsAndTheLocalMachine(string endpoint) =>
        ObjectStorageOptions.Create(endpoint, "photostudio-media", "key", "secret").Bucket.ShouldBe("photostudio-media");

    /// <summary>
    /// A bucket name must be a real one: lowercase letters, digits and hyphens.
    /// </summary>
    /// <param name="bucket">The name.</param>
    [Theory]
    [InlineData("ab")]
    [InlineData("Photo-Media")]
    [InlineData("-photo")]
    [InlineData("photo-")]
    [InlineData("photo_media")]
    [InlineData("photo/media")]
    public void Create_RefusesAnInvalidBucketName(string bucket) =>
        Should.Throw<InvalidOperationException>(() => ObjectStorageOptions.Create("https://abc123.r2.cloudflarestorage.com", bucket, "key", "secret"));

    /// <summary>
    /// Sets the variables of a scenario.
    /// </summary>
    /// <param name="account">Account id.</param>
    /// <param name="endpoint">Explicit address.</param>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="key">Access key id.</param>
    /// <param name="secret">Secret access key.</param>
    private static void Set(string? account = null, string? endpoint = null, string? bucket = null, string? key = null, string? secret = null)
    {
        Environment.SetEnvironmentVariable(ObjectStorageOptions.AccountIdVariable, account);
        Environment.SetEnvironmentVariable(ObjectStorageOptions.EndpointVariable, endpoint);
        Environment.SetEnvironmentVariable(ObjectStorageOptions.BucketVariable, bucket);
        Environment.SetEnvironmentVariable(ObjectStorageOptions.AccessKeyIdVariable, key);
        Environment.SetEnvironmentVariable(ObjectStorageOptions.SecretAccessKeyVariable, secret);
    }
}
