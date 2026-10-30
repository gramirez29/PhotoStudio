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
        Set(account: "abc123", bucket: "photostudio-media", key: "key", secret: "secret");

        var options = ObjectStorageOptions.FromEnvironment();

        options.ShouldNotBeNull();
        options.Endpoint.ToString().ShouldBe("https://abc123.r2.cloudflarestorage.com/");
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
        Set(account: "abc123", bucket: "photostudio-media", secret: "top-secret-value");

        var error = Should.Throw<InvalidOperationException>(() => ObjectStorageOptions.FromEnvironment());

        error.Message.ShouldContain(ObjectStorageOptions.AccessKeyIdVariable);
        error.Message.ShouldNotContain("top-secret-value");
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
