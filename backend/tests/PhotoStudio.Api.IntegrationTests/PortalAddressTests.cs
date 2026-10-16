using Microsoft.Extensions.DependencyInjection;
using PhotoStudio.Application.Portal;
using PhotoStudio.Infrastructure.Portal;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// Tests of how the API reads the public address of the site (<c>PORTAL_BASE_URL</c>), which ends up in every link sent to a
/// client and in the origin CORS allows. They change process environment variables, so they run in the same collection as the
/// other API tests, one at a time, and put every variable back.
/// </summary>
/// <param name="fixture">Shared API instance; it makes the collection run these tests one at a time.</param>
[Collection(ApiCollection.Name)]
public sealed class PortalAddressTests(ApiFixture fixture)
{
    private const string AddressVariable = "PORTAL_BASE_URL";
    private const string SigningKeyVariable = "JWT_SIGNING_KEY";
    private const string ProductionKey = "a-signing-key-generated-for-this-environment-0123456789";

    /// <summary>
    /// Outside Development the address is mandatory: a default would silently put a wrong address in every link.
    /// </summary>
    [Fact]
    public void Outside_Development_AMissingAddressStopsTheApi()
    {
        _ = fixture;

        var error = Should.Throw<InvalidOperationException>(() => RegisterWith(address: null, isDevelopment: false));

        error.Message.ShouldContain(AddressVariable);
    }

    /// <summary>
    /// A blank address counts as missing.
    /// </summary>
    [Fact]
    public void Outside_Development_ABlankAddressStopsTheApi()
    {
        Should.Throw<InvalidOperationException>(() => RegisterWith(address: "   ", isDevelopment: false)).Message.ShouldContain(AddressVariable);
    }

    /// <summary>
    /// In Development a missing address falls back to the local server of the portal.
    /// </summary>
    [Fact]
    public void InDevelopment_AMissingAddressFallsBackToTheLocalServer()
    {
        RegisterWith(address: null, isDevelopment: true).ShouldBe("http://localhost:5173");
    }

    /// <summary>
    /// A configured address is used as given, without the trailing slash, so links are built as <c>{address}/b/{token}</c>.
    /// </summary>
    [Fact]
    public void AConfiguredAddress_IsUsedWithoutTheTrailingSlash()
    {
        RegisterWith(address: " https://develop.photosensestudio.com/ ", isDevelopment: false).ShouldBe("https://develop.photosensestudio.com");
    }

    /// <summary>
    /// An address that is not an absolute http(s) address stops the API instead of producing broken links.
    /// </summary>
    /// <param name="address">Invalid address.</param>
    [Theory]
    [InlineData("develop.photosensestudio.com")]
    [InlineData("ftp://develop.photosensestudio.com")]
    public void AnInvalidAddress_StopsTheApi(string address)
    {
        Should.Throw<InvalidOperationException>(() => RegisterWith(address, isDevelopment: false)).Message.ShouldContain(AddressVariable);
    }

    /// <summary>
    /// Registers the client portal with the given address and returns the address the settings ended up with, restoring the
    /// environment afterwards.
    /// </summary>
    /// <param name="address">Value of the variable, or <see langword="null"/> to leave it unset.</param>
    /// <param name="isDevelopment">Whether to register as in Development.</param>
    /// <returns>The base address of the portal settings.</returns>
    private static string RegisterWith(string? address, bool isDevelopment)
    {
        var previousAddress = Environment.GetEnvironmentVariable(AddressVariable);
        var previousKey = Environment.GetEnvironmentVariable(SigningKeyVariable);
        try
        {
            Environment.SetEnvironmentVariable(AddressVariable, address);

            // The test key of the fixture contains the development marker, which is refused outside Development.
            Environment.SetEnvironmentVariable(SigningKeyVariable, ProductionKey);

            var services = new ServiceCollection();
            services.AddClientPortal(isDevelopment);
            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<PortalSettings>().BaseUrl;
        }
        finally
        {
            Environment.SetEnvironmentVariable(AddressVariable, previousAddress);
            Environment.SetEnvironmentVariable(SigningKeyVariable, previousKey);
        }
    }
}
