using System.Net;
using System.Text.Json;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of correcting a booking: the client data while the booking is alive, and the package and price only before the
/// contract is signed or any payment is recorded.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class EditBookingApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    /// <summary>
    /// The case that motivated it: a booking created without the client email gets it afterwards, and the portal can then send codes.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddingTheEmailAfterwards_LetsThePortalSendCodes()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var booking = await client.CreateBookingAsync(session.AccessToken);
        var id = booking.GetProperty("id").GetGuid();
        booking.GetProperty("clientEmail").ValueKind.ShouldBe(JsonValueKind.Null);
        var actions = booking.GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString()).ToList();
        actions.ShouldContain("EditDetails");
        actions.ShouldContain("EditPackage");
        var token = (await (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/link", session.AccessToken)).ReadJsonAsync()).GetProperty("token").GetString()!;
        (await (await client.SendAsync(HttpMethod.Post, $"/public/bookings/{token}/code", null)).ReadCodeAsync()).ShouldBe("portal.email_missing");
        var email = $"cliente{Guid.NewGuid():N}@example.com";

        var edited = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/details", session.AccessToken, Body(email: email, name: "María Pérez Solano", price: 120000));

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await edited.ReadJsonAsync();
        json.GetProperty("clientEmail").GetString().ShouldBe(email);
        json.GetProperty("clientName").GetString().ShouldBe("María Pérez Solano");
        json.GetProperty("packagePrice").GetProperty("amount").GetDecimal().ShouldBe(120000m);
        json.GetProperty("depositRequired").GetProperty("amount").GetDecimal().ShouldBe(60000m);
        (await (await client.SendAsync(HttpMethod.Get, $"/public/bookings/{token}", null)).ReadJsonAsync()).GetProperty("canReceiveCode").GetBoolean().ShouldBeTrue();
        (await client.SendAsync(HttpMethod.Post, $"/public/bookings/{token}/code", null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        fixture.Emails.LastCodeFor(email).Length.ShouldBe(6);
    }

    /// <summary>
    /// Changing the email cancels the code that was sent to the old address, so it cannot be used any more.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ChangingTheEmail_CancelsTheCodeSentToTheOldOne()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var oldEmail = $"vieja{Guid.NewGuid():N}@example.com";
        var booking = await client.CreateBookingAsync(session.AccessToken, new Dictionary<string, object> { ["clientEmail"] = oldEmail });
        var id = booking.GetProperty("id").GetGuid();
        var token = (await (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/link", session.AccessToken)).ReadJsonAsync()).GetProperty("token").GetString()!;
        (await client.SendAsync(HttpMethod.Post, $"/public/bookings/{token}/code", null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var oldCode = fixture.Emails.LastCodeFor(oldEmail);

        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/details", session.AccessToken, Body(email: $"nueva{Guid.NewGuid():N}@example.com"))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var verify = await client.SendAsync(HttpMethod.Post, $"/public/bookings/{token}/code/verify", null, new { code = oldCode });
        verify.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await verify.ReadCodeAsync()).ShouldBe("portal.invalid_code");
    }

    /// <summary>
    /// After the contract is signed the price cannot change, but the client can still be corrected.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AfterTheContractIsSigned_ThePriceIsLocked_ButTheClientCanStillBeEdited()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var id = (await client.CreateBookingAsync(session.AccessToken)).GetProperty("id").GetGuid();
        (await client.SendAsync(
            HttpMethod.Post,
            $"/api/bookings/{id}/contract/in-person",
            session.AccessToken,
            new { signerName = "María Pérez", templateVersion = "v1", isPaperContract = true })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var price = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/details", session.AccessToken, Body(price: 90000));
        var name = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/details", session.AccessToken, Body(name: "María P. Solano"));

        price.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await price.ReadCodeAsync()).ShouldBe("booking.guard_failed");
        name.StatusCode.ShouldBe(HttpStatusCode.OK);
        var seen = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}", session.AccessToken)).ReadJsonAsync();
        seen.GetProperty("packagePrice").GetProperty("amount").GetDecimal().ShouldBe(100000m);
        var actions = seen.GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString()).ToList();
        actions.ShouldContain("EditDetails");
        actions.ShouldNotContain("EditPackage");
    }

    /// <summary>
    /// A cancelled booking cannot be edited; another photographer and an anonymous caller are refused; invalid values are 422.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Refusals_CancelledBooking_OtherPhotographer_Anonymous_InvalidValues()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var stranger = await client.LoginAsync(await fixture.CreateUserAsync());
        var id = (await client.CreateBookingAsync(owner.AccessToken)).GetProperty("id").GetGuid();

        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/details", stranger.AccessToken, Body())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/details", null, Body())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var badEmail = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/details", owner.AccessToken, Body(email: "no-es-un-correo"));
        badEmail.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await badEmail.ReadCodeAsync()).ShouldBe("user.invalid_email");

        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/cancel", owner.AccessToken, new { })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var cancelled = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/details", owner.AccessToken, Body());
        cancelled.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await cancelled.ReadCodeAsync()).ShouldBe("booking.invalid_transition");
    }

    /// <summary>
    /// Builds the body that corrects the booking made by <c>CreateBookingAsync</c>: every value as it was, except the ones given.
    /// </summary>
    /// <param name="name">New client name.</param>
    /// <param name="email">New client email, or null for none.</param>
    /// <param name="price">New package price.</param>
    /// <returns>The request body.</returns>
    private static object Body(string name = "María Pérez", string? email = null, decimal price = 100000m) => new
    {
        clientName = name,
        clientPhone = "+50670189220",
        clientEmail = email,
        packageName = "Retrato Familiar",
        packagePrice = price,
    };
}
