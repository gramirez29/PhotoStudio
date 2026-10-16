using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the push: registering the phone, and the whole path from a proof of payment uploaded by the client to
/// the push the photographer's phone receives.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class PushDevicesApiTests(ApiFixture fixture)
{
    /// <summary>
    /// Both endpoints require a signed-in user.
    /// </summary>
    /// <param name="path">Path of the endpoint.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("/api/push-devices")]
    [InlineData("/api/push-devices/unregister")]
    public async Task Endpoints_WithoutToken_Return401(string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var response = await fixture.CreateClient().SendAsync(HttpMethod.Post, path, accessToken: null, new { token = "ExponentPushToken[abc]", platform = "Android" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A token that is not an Expo push token answers 422 with its own code, and nothing is stored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WithAnInvalidToken_Answers422()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());

        var invalid = await client.SendAsync(HttpMethod.Post, "/api/push-devices", session.AccessToken, new { token = "not-a-token", platform = "Android" });
        var missing = await client.SendAsync(HttpMethod.Post, "/api/push-devices", session.AccessToken, new { platform = "Android" });

        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await invalid.ReadCodeAsync()).ShouldBe("push.invalid_token");
        missing.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await DevicesOfAsync(session.PhotographerId)).ShouldBeEmpty();
    }

    /// <summary>
    /// Registering stores the phone of the signed-in photographer (twice is harmless), and unregistering removes it; a
    /// photographer cannot remove the phone of another one.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterAndUnregister_ManageThePhoneOfTheSignedInPhotographer()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var token = $"ExponentPushToken[{Guid.NewGuid():N}]";

        (await client.SendAsync(HttpMethod.Post, "/api/push-devices", owner.AccessToken, new { token, platform = "Android" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.SendAsync(HttpMethod.Post, "/api/push-devices", owner.AccessToken, new { token, platform = "Android" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DevicesOfAsync(owner.PhotographerId)).ShouldBe([token]);

        (await client.SendAsync(HttpMethod.Post, "/api/push-devices/unregister", other.AccessToken, new { token })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DevicesOfAsync(owner.PhotographerId)).ShouldBe([token]);

        (await client.SendAsync(HttpMethod.Post, "/api/push-devices/unregister", owner.AccessToken, new { token })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DevicesOfAsync(owner.PhotographerId)).ShouldBeEmpty();
    }

    /// <summary>
    /// A client uploads a proof of payment through the portal: without anyone running a maintenance pass, the photographer's
    /// phone receives the push in seconds, and the notice is in the inbox. Another photographer's phone receives nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ProofSubmittedByTheClient_PushesToThePhotographerPhoneWithoutWaitingForTheWorker()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var other = await client.LoginAsync(await fixture.CreateUserAsync());
        var ownerToken = $"ExponentPushToken[{Guid.NewGuid():N}]";
        var otherToken = $"ExponentPushToken[{Guid.NewGuid():N}]";
        (await client.SendAsync(HttpMethod.Post, "/api/push-devices", owner.AccessToken, new { token = ownerToken, platform = "Android" })).EnsureSuccessStatusCode();
        (await client.SendAsync(HttpMethod.Post, "/api/push-devices", other.AccessToken, new { token = otherToken, platform = "Android" })).EnsureSuccessStatusCode();

        var clientEmail = $"c{Guid.NewGuid():N}@example.com";
        var booking = await client.CreateBookingAsync(owner.AccessToken, new Dictionary<string, object> { ["clientEmail"] = clientEmail });
        var bookingId = booking.GetProperty("id").GetGuid();
        var link = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{bookingId}/link", owner.AccessToken);
        var portalToken = (await link.ReadJsonAsync()).GetProperty("token").GetString()!;
        (await client.SendAsync(HttpMethod.Post, $"/public/bookings/{portalToken}/code", null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var verified = await client.SendAsync(HttpMethod.Post, $"/public/bookings/{portalToken}/code/verify", null, new { code = fixture.Emails.LastCodeFor(clientEmail) });
        var session = (await verified.ReadJsonAsync()).GetProperty("accessToken").GetString()!;

        (await client.SendAsync(
            HttpMethod.Post,
            $"/public/bookings/{portalToken}/payments",
            session,
            new { amount = 25_000, reference = "SINPE-4455", idempotencyKey = Guid.NewGuid().ToString() })).StatusCode.ShouldBe(HttpStatusCode.OK);

        // No maintenance pass is run here: the API itself wakes up after the request.
        var push = await WaitForPushAsync(ownerToken);

        push.Title.ShouldBe("Comprobante por verificar");
        push.Body.ShouldBe("María Pérez envió un comprobante de ₡25 000.");
        push.Data["bookingId"].ShouldBe(bookingId.ToString());
        push.Data["type"].ShouldBe("PaymentProofSubmitted");
        fixture.Pushes.Messages.Any(message => message.Token == otherToken).ShouldBeFalse();

        var inbox = await (await client.SendAsync(HttpMethod.Get, "/api/notifications", owner.AccessToken)).ReadJsonAsync();
        inbox.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("bookingId").GetGuid() == bookingId)
            .GetProperty("id").GetGuid().ToString().ShouldBe(push.Data["notificationId"]);
    }

    /// <summary>
    /// Waits, up to a few seconds, for the push to a token to be captured.
    /// </summary>
    /// <param name="token">Expo push token of the phone.</param>
    /// <returns>The push.</returns>
    private async Task<PushMessage> WaitForPushAsync(string token)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var push = fixture.Pushes.Messages.FirstOrDefault(message => message.Token == token);
            if (push is not null)
            {
                return push;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No push reached {token} in 15 seconds.");
    }

    /// <summary>
    /// Lists the tokens registered for a photographer, read straight from the repository of the API under test.
    /// </summary>
    /// <param name="photographerId">Photographer identifier.</param>
    /// <returns>The tokens.</returns>
    private async Task<IReadOnlyList<string>> DevicesOfAsync(Guid photographerId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var devices = await scope.ServiceProvider.GetRequiredService<IPushDeviceRepository>()
            .ListByPhotographerAsync(photographerId, TestContext.Current.CancellationToken);
        return [.. devices.Select(device => device.Token)];
    }
}
