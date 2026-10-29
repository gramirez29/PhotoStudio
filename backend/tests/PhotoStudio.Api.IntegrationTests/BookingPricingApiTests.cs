using System.Net;
using System.Text.Json;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the price of a booking made of a package, extras and a discount.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class BookingPricingApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    /// <summary>
    /// A booking with a package, extras and a discount is stored, read back with its pieces, and charged the total.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CreatingWithExtrasAndDiscount_StoresThePiecesAndChargesTheTotal()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());

        var created = await client.CreateBookingAsync(session.AccessToken, new Dictionary<string, object>
        {
            ["packagePrice"] = 65000,
            ["packageTier"] = "Standard",
            ["extras"] = new[] { new { code = "extra-hour", name = "Hora adicional", unitPrice = 15000, quantity = 2 } },
            ["discount"] = 5000,
        });

        var read = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{created.GetProperty("id").GetGuid()}", session.AccessToken)).ReadJsonAsync();
        read.GetProperty("packageTier").GetString().ShouldBe("Standard");
        read.GetProperty("basePrice").GetProperty("amount").GetDecimal().ShouldBe(65000m);
        read.GetProperty("discount").GetProperty("amount").GetDecimal().ShouldBe(5000m);
        var extra = read.GetProperty("extras").EnumerateArray().Single();
        extra.GetProperty("name").GetString().ShouldBe("Hora adicional");
        extra.GetProperty("quantity").GetInt32().ShouldBe(2);
        extra.GetProperty("total").GetProperty("amount").GetDecimal().ShouldBe(30000m);
        read.GetProperty("packagePrice").GetProperty("amount").GetDecimal().ShouldBe(90000m);
        read.GetProperty("depositRequired").GetProperty("amount").GetDecimal().ShouldBe(45000m);
    }

    /// <summary>
    /// A booking sent the old way, with no tier, extras or discount, still works and is priced as the package alone.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CreatingTheOldWay_ChargesThePackageAlone()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());

        var created = await client.CreateBookingAsync(session.AccessToken);

        created.GetProperty("packageTier").ValueKind.ShouldBe(JsonValueKind.Null);
        created.GetProperty("extras").GetArrayLength().ShouldBe(0);
        created.GetProperty("discount").GetProperty("amount").GetDecimal().ShouldBe(0m);
        created.GetProperty("packagePrice").GetProperty("amount").GetDecimal().ShouldBe(100000m);
    }

    /// <summary>
    /// A discount larger than the price is a validation error, not a negative price.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CreatingWithADiscountLargerThanThePrice_IsRejected()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var start = DateTimeOffset.UtcNow.AddDays(10);

        var response = await client.SendAsync(HttpMethod.Post, "/api/bookings", session.AccessToken, new Dictionary<string, object>
        {
            ["clientName"] = "María Pérez",
            ["clientPhone"] = "+50670189220",
            ["packageName"] = "Retrato Familiar",
            ["packagePrice"] = 35000,
            ["currency"] = "CRC",
            ["sessionStart"] = start,
            ["sessionEnd"] = start.AddHours(1),
            ["discount"] = 40000,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadCodeAsync()).ShouldBe("booking.invalid_discount");
    }

    /// <summary>
    /// The extras, the discount and the package are corrected through the same endpoint; a request that does not mention them
    /// keeps them, and the contract of a booking shows what it was sold with.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EditingTheExtrasAndTheDiscount_ChangesTheTotalAndTheContract()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var created = await client.CreateBookingAsync(session.AccessToken, new Dictionary<string, object>
        {
            ["packagePrice"] = 65000,
            ["packageTier"] = "Standard",
            ["extras"] = new[] { new { code = "makeup", name = "Maquillaje", unitPrice = 20000, quantity = 1 } },
        });
        var id = created.GetProperty("id").GetGuid();
        var body = (object extras, decimal? discount) => new
        {
            clientName = "María Pérez",
            clientPhone = "+50670189220",
            packageName = "Retrato Familiar",
            packagePrice = 65000,
            packageTier = "Premium",
            extras,
            discount,
        };

        var edited = await client.SendAsync(
            HttpMethod.Post,
            $"/api/bookings/{id}/details",
            session.AccessToken,
            body(new[] { new { code = "extra-hour", name = "Hora adicional", unitPrice = 30000, quantity = 2 } }, 5000m));

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await edited.ReadJsonAsync();
        json.GetProperty("packageTier").GetString().ShouldBe("Premium");
        json.GetProperty("extras").EnumerateArray().Single().GetProperty("name").GetString().ShouldBe("Hora adicional");
        json.GetProperty("discount").GetProperty("amount").GetDecimal().ShouldBe(5000m);
        json.GetProperty("packagePrice").GetProperty("amount").GetDecimal().ShouldBe(120000m);
        var contract = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}/contract", session.AccessToken)).ReadJsonAsync();
        contract.GetProperty("templateVersion").GetString().ShouldBe("v2");
        var text = contract.GetProperty("text").GetString()!;
        text.ShouldContain("con el paquete Premium");
        text.ShouldContain("Hora adicional × 2");
        text.ShouldContain("descuento -₡5 000");
    }

    /// <summary>
    /// After the contract is signed the extras and the discount can no longer change.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EditingTheExtras_AfterTheContractIsSigned_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var created = await client.CreateBookingAsync(session.AccessToken);
        var id = created.GetProperty("id").GetGuid();
        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/contract/in-person", session.AccessToken,
            new { signerName = "María Pérez", templateVersion = "v2", isPaperContract = true })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/details", session.AccessToken, new
        {
            clientName = "María Pérez",
            clientPhone = "+50670189220",
            packageName = "Retrato Familiar",
            packagePrice = 100000,
            discount = 1000,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadCodeAsync()).ShouldBe("booking.guard_failed");
    }
}
