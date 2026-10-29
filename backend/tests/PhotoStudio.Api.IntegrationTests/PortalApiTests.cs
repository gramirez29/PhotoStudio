using System.Globalization;
using System.Net;
using System.Text.Json;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the client portal against the API running in memory: the link, the verification code by email, the
/// portal session and the payment proof the photographer then verifies or rejects.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class PortalApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    /// <summary>
    /// The whole remote flow: link, read, code by email, session, proof, and the photographer verifying it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ClientFlow_FromLinkToVerifiedPayment()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var client = scenario.Client;

        var view = await client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}", null);
        view.StatusCode.ShouldBe(HttpStatusCode.OK);
        var viewJson = await view.ReadJsonAsync();
        viewJson.GetProperty("packageName").GetString().ShouldBe("Retrato Familiar");
        viewJson.GetProperty("clientName").GetString().ShouldBe("María Pérez");
        viewJson.GetProperty("canSubmitProof").GetBoolean().ShouldBeTrue();
        viewJson.GetProperty("canReceiveCode").GetBoolean().ShouldBeTrue();
        viewJson.GetProperty("photographerName").GetString().ShouldBe("Ana Pérez");

        var requested = await client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code", null);
        requested.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await requested.ReadJsonAsync()).GetProperty("maskedEmail").GetString().ShouldBe($"{scenario.ClientEmail[0]}***@example.com");

        var verified = await client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code/verify", null, new { code = fixture.Emails.LastCodeFor(scenario.ClientEmail) });
        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
        var portalToken = (await verified.ReadJsonAsync()).GetProperty("accessToken").GetString()!;

        var proof = await client.SendAsync(
            HttpMethod.Post,
            $"/public/bookings/{scenario.Token}/payments",
            portalToken,
            new { amount = 50_000, reference = "SINPE-4455", idempotencyKey = Guid.NewGuid().ToString() });
        proof.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterProof = await proof.ReadJsonAsync();
        afterProof.GetProperty("payments").GetArrayLength().ShouldBe(1);
        afterProof.GetProperty("payments")[0].GetProperty("status").GetString().ShouldBe("PendingVerification");
        afterProof.GetProperty("totalPaid").GetProperty("amount").GetDecimal().ShouldBe(0m);

        var seenByPhotographer = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{scenario.BookingId}", scenario.Session.AccessToken)).ReadJsonAsync();
        seenByPhotographer.GetProperty("clientEmail").GetString().ShouldBe(scenario.ClientEmail);
        var payment = seenByPhotographer.GetProperty("payments")[0];
        payment.GetProperty("reference").GetString().ShouldBe("SINPE-4455");
        var paymentId = payment.GetProperty("id").GetGuid();

        var verifiedPayment = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/payments/{paymentId}/verify", scenario.Session.AccessToken);
        verifiedPayment.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterVerify = await verifiedPayment.ReadJsonAsync();
        afterVerify.GetProperty("totalPaid").GetProperty("amount").GetDecimal().ShouldBe(50_000m);
        afterVerify.GetProperty("payments")[0].GetProperty("status").GetString().ShouldBe("Verified");

        var again = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/payments/{paymentId}/verify", scenario.Session.AccessToken);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.ReadCodeAsync()).ShouldBe("payment.invalid_status");
    }

    /// <summary>
    /// What the link shows leaves out the client's phone and email: holding the link must not reveal them.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PortalView_DoesNotExposeTheClientsPhoneOrEmail()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();

        var response = await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}", null);

        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        raw.ShouldNotContain("70189220");
        raw.ShouldNotContain(scenario.ClientEmail);
        raw.ShouldNotContain("clientPhone");
        raw.ShouldNotContain(scenario.BookingId.ToString());
    }

    /// <summary>
    /// A token that opens nothing is a 404 on every public endpoint.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path under the unknown token.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "/code")]
    [InlineData("POST", "/code/verify")]
    public async Task UnknownToken_Answers404(string method, string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);

        var response = await fixture.CreateClient().SendAsync(new HttpMethod(method), $"/public/bookings/{Guid.NewGuid():N}{path}", null, method == "POST" ? new { code = "123456" } : null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Making a new link cancels the old one.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task NewLink_RevokesThePreviousOne()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();

        var second = await scenario.Client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/link", scenario.Session.AccessToken);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        var secondToken = (await second.ReadJsonAsync()).GetProperty("token").GetString()!;

        (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{secondToken}", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The link response carries the portal URL, and only a photographer can create one for their own booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CreateLink_ReturnsTheUrl_AndIsScopedToTheOwner()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var stranger = await scenario.Client.LoginAsync(await fixture.CreateUserAsync());

        var mine = await scenario.Client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/link", scenario.Session.AccessToken);
        var theirs = await scenario.Client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/link", stranger.AccessToken);
        var anonymous = await scenario.Client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/link", null);

        var json = await mine.ReadJsonAsync();
        json.GetProperty("url").GetString().ShouldEndWith($"/b/{json.GetProperty("token").GetString()}");
        theirs.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A booking without a client email cannot receive codes: the client is told so, and cannot type another address.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RequestCode_WithoutAClientEmail_Answers409()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync(withEmail: false);

        var view = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}", null)).ReadJsonAsync();
        var response = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code", null);

        view.GetProperty("canReceiveCode").GetBoolean().ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadCodeAsync()).ShouldBe("portal.email_missing");
    }

    /// <summary>
    /// A second code asked for right away is refused with the time to wait.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RequestCode_Twice_AnswersTooManyRequestsWithRetryAfter()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        (await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code", null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var second = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code", null);

        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await second.ReadCodeAsync()).ShouldBe("portal.otp_rate_limited");
        int.Parse(second.Headers.GetValues("Retry-After").Single(), CultureInfo.InvariantCulture).ShouldBeInRange(1, 60);
    }

    /// <summary>
    /// A wrong code is a 422, and after five wrong guesses even the right code no longer works.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task VerifyCode_AfterFiveWrongGuesses_SpendsTheCode()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code", null);
        var right = fixture.Emails.LastCodeFor(scenario.ClientEmail);
        var wrong = right == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code/verify", null, new { code = wrong });
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await response.ReadCodeAsync()).ShouldBe("portal.invalid_code");
        }

        var afterwards = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code/verify", null, new { code = right });
        afterwards.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// A code works once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task VerifyCode_CannotBeUsedTwice()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code", null);
        var code = fixture.Emails.LastCodeFor(scenario.ClientEmail);

        var first = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code/verify", null, new { code });
        var second = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/code/verify", null, new { code });

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// Acting on the booking needs a portal session: nothing, a photographer token, or a made-up one is a 401.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SubmitProof_WithoutAPortalSession_Answers401()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var body = new { amount = 10_000, reference = "x", idempotencyKey = "k-1" };

        var anonymous = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/payments", null, body);
        var photographer = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/payments", scenario.Session.AccessToken, body);
        var forged = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/payments", "not.a.token", body);

        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        photographer.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        forged.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The contract flow: the client reads it from the link, signs it with their session, and the photographer then sees what they
    /// declared. Reading again returns the exact text that was signed, and a second signature is a conflict.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ContractFlow_ClientReadsAndSigns_AndThePhotographerSeesTheDeclaration()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var client = scenario.Client;

        // The text holds the client's phone and, once signed, their identity number, so reading it needs the portal session.
        (await client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/contract", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var portalToken = await scenario.OpenPortalSessionAsync(fixture);
        var before = await (await client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/contract", portalToken)).ReadJsonAsync();
        before.GetProperty("signed").GetBoolean().ShouldBeFalse();
        var version = before.GetProperty("templateVersion").GetString()!;
        before.GetProperty("text").GetString()!.ShouldContain("María Pérez");
        (await (await client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}", null)).ReadJsonAsync())
            .GetProperty("canSignContract").GetBoolean().ShouldBeTrue();

        var body = new
        {
            templateVersion = version,
            signerName = "María Pérez",
            idNumber = "1-1234-5678",
            participantCount = 3,
            sessionLocation = "Parque La Sabana",
            imageUse = "Authorized",
            acceptsTerms = true,
        };
        var signed = await client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/contract", portalToken, body);

        signed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterSigning = await signed.ReadJsonAsync();
        afterSigning.GetProperty("contractSigned").GetBoolean().ShouldBeTrue();
        afterSigning.GetProperty("canSignContract").GetBoolean().ShouldBeFalse();

        var after = await (await client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/contract", portalToken)).ReadJsonAsync();
        after.GetProperty("signed").GetBoolean().ShouldBeTrue();
        after.GetProperty("text").GetString()!.ShouldContain("cédula 112345678");

        var seenByPhotographer = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{scenario.BookingId}", scenario.Session.AccessToken)).ReadJsonAsync();
        var contract = seenByPhotographer.GetProperty("contract");
        contract.GetProperty("channel").GetString().ShouldBe("Portal");
        contract.GetProperty("declaration").GetProperty("idNumber").GetString().ShouldBe("112345678");
        contract.GetProperty("declaration").GetProperty("participantCount").GetInt32().ShouldBe(3);
        contract.GetProperty("declaration").GetProperty("imageUse").GetString().ShouldBe("Authorized");

        var again = await client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/contract", portalToken, body);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.ReadCodeAsync()).ShouldBe("booking.contract_already_signed");
    }

    /// <summary>
    /// Signing needs a portal session, an accepted contract, the version the client read and valid values.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SignContract_RejectsMissingSessionAndInvalidRequests()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var client = scenario.Client;
        var portalToken = await scenario.OpenPortalSessionAsync(fixture);
        var version = (await (await client.SendAsync(HttpMethod.Get, $"/public/bookings/{scenario.Token}/contract", portalToken)).ReadJsonAsync())
            .GetProperty("templateVersion").GetString()!;
        object Body(string templateVersion = "", string idNumber = "112345678", bool accepts = true) => new
        {
            templateVersion = templateVersion.Length == 0 ? version : templateVersion,
            signerName = "María Pérez",
            idNumber,
            participantCount = 2,
            imageUse = "NotAuthorized",
            acceptsTerms = accepts,
        };

        (await client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/contract", null, Body())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/contract", scenario.Session.AccessToken, Body())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var notAccepted = await client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/contract", portalToken, Body(accepts: false));
        var badId = await client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/contract", portalToken, Body(idNumber: "123"));
        var outdated = await client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/contract", portalToken, Body(templateVersion: "v0"));

        notAccepted.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        badId.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await badId.ReadCodeAsync()).ShouldBe("contract.invalid_id_number");
        outdated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await outdated.ReadCodeAsync()).ShouldBe("contract.template_changed");
        (await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{scenario.BookingId}", scenario.Session.AccessToken)).ReadJsonAsync())
            .GetProperty("contract").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>
    /// A portal session opens the contract of its own booking only: with a link that opens nothing, or with the link of another
    /// booking, the answer is a 404 and the text (which holds personal data) is not returned.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Contract_WithAnUnknownTokenOrAnotherBookingsLink_Answers404()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var mine = await NewScenarioAsync();
        var other = await NewScenarioAsync();
        var portalToken = await mine.OpenPortalSessionAsync(fixture);

        var unknown = await mine.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{Guid.NewGuid():N}/contract", portalToken);
        var ofAnotherBooking = await mine.Client.SendAsync(HttpMethod.Get, $"/public/bookings/{other.Token}/contract", portalToken);

        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ofAnotherBooking.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ofAnotherBooking.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("CONTRATO");
    }

    /// <summary>
    /// A portal session is not a photographer token: the photographer API refuses it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task PortalSession_IsRefusedByThePhotographerApi()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var portalToken = await scenario.OpenPortalSessionAsync(fixture);

        var bookings = await scenario.Client.SendAsync(HttpMethod.Get, "/api/bookings", portalToken);
        var single = await scenario.Client.SendAsync(HttpMethod.Get, $"/api/bookings/{scenario.BookingId}", portalToken);
        var verify = await scenario.Client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/payments/{Guid.NewGuid()}/verify", portalToken);

        bookings.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        single.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        verify.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A session obtained for one booking cannot act through the link of another.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SubmitProof_WithTheSessionOfAnotherBooking_Answers404()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var mine = await NewScenarioAsync();
        var other = await NewScenarioAsync();
        var portalTokenOfMine = await mine.OpenPortalSessionAsync(fixture);

        var response = await other.Client.SendAsync(
            HttpMethod.Post,
            $"/public/bookings/{other.Token}/payments",
            portalTokenOfMine,
            new { amount = 10_000, reference = "x", idempotencyKey = "k-1" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The client cannot declare more than the booking still owes.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SubmitProof_WithMoreThanTheBalance_Answers422()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var portalToken = await scenario.OpenPortalSessionAsync(fixture);

        var response = await scenario.Client.SendAsync(
            HttpMethod.Post,
            $"/public/bookings/{scenario.Token}/payments",
            portalToken,
            new { amount = 100_001, reference = "x", idempotencyKey = "k-1" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadCodeAsync()).ShouldBe("money.invalid_amount");
    }

    /// <summary>
    /// Sending the same proof twice (a double tap) registers it once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SubmitProof_WithTheSameIdempotencyKey_RegistersOnce()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var portalToken = await scenario.OpenPortalSessionAsync(fixture);
        var body = new { amount = 50_000, reference = "SINPE-1", idempotencyKey = "same-key" };

        var first = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/payments", portalToken, body);
        var second = await scenario.Client.SendAsync(HttpMethod.Post, $"/public/bookings/{scenario.Token}/payments", portalToken, body);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.ReadJsonAsync()).GetProperty("payments").GetArrayLength().ShouldBe(1);
    }

    /// <summary>
    /// Rejecting a proof needs a reason; with one, the proof stays as rejected evidence and does not count.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RejectPayment_NeedsAReason_AndKeepsTheProof()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var portalToken = await scenario.OpenPortalSessionAsync(fixture);
        await scenario.Client.SendAsync(
            HttpMethod.Post,
            $"/public/bookings/{scenario.Token}/payments",
            portalToken,
            new { amount = 50_000, reference = "SINPE-2", idempotencyKey = "k-reject" });
        var booking = await (await scenario.Client.SendAsync(HttpMethod.Get, $"/api/bookings/{scenario.BookingId}", scenario.Session.AccessToken)).ReadJsonAsync();
        var paymentId = booking.GetProperty("payments")[0].GetProperty("id").GetGuid();

        var withoutReason = await scenario.Client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/payments/{paymentId}/reject", scenario.Session.AccessToken, new { reason = " " });
        var rejected = await scenario.Client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/payments/{paymentId}/reject", scenario.Session.AccessToken, new { reason = "No aparece en el banco" });

        withoutReason.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await withoutReason.ReadCodeAsync()).ShouldBe("booking.reason_required");
        rejected.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await rejected.ReadJsonAsync();
        json.GetProperty("payments")[0].GetProperty("status").GetString().ShouldBe("Rejected");
        json.GetProperty("totalPaid").GetProperty("amount").GetDecimal().ShouldBe(0m);
    }

    /// <summary>
    /// A photographer cannot verify a payment of another photographer's booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task VerifyPayment_OfAnotherPhotographersBooking_Answers404()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var scenario = await NewScenarioAsync();
        var stranger = await scenario.Client.LoginAsync(await fixture.CreateUserAsync());

        var response = await scenario.Client.SendAsync(HttpMethod.Post, $"/api/bookings/{scenario.BookingId}/payments/{Guid.NewGuid()}/verify", stranger.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A booking cannot be created with a malformed client email.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CreateBooking_WithAMalformedClientEmail_Answers422()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var start = DateTimeOffset.UtcNow.AddDays(20);

        var response = await client.SendAsync(
            HttpMethod.Post,
            "/api/bookings",
            session.AccessToken,
            new
            {
                clientName = "María Pérez",
                clientPhone = "+50670189220",
                clientEmail = "not-an-email",
                packageName = "Retrato Familiar",
                packagePrice = 100_000,
                currency = "CRC",
                sessionStart = start,
                sessionEnd = start.AddHours(2),
            });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadCodeAsync()).ShouldBe("user.invalid_email");
    }

    /// <summary>
    /// Creates a photographer, a booking (optionally with a client email) and its link.
    /// </summary>
    /// <param name="withEmail">Whether the booking has a client email.</param>
    /// <returns>The scenario.</returns>
    private async Task<PortalScenario> NewScenarioAsync(bool withEmail = true)
    {
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var clientEmail = $"c{Guid.NewGuid():N}@example.com";
        var extra = withEmail ? new Dictionary<string, object> { ["clientEmail"] = clientEmail } : null;
        var booking = await client.CreateBookingAsync(session.AccessToken, extra);
        var bookingId = booking.GetProperty("id").GetGuid();

        var link = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{bookingId}/link", session.AccessToken);
        link.StatusCode.ShouldBe(HttpStatusCode.Created);
        var token = (await link.ReadJsonAsync()).GetProperty("token").GetString()!;

        return new PortalScenario(client, session, bookingId, token, clientEmail);
    }

    /// <summary>
    /// A photographer, one of their bookings and the link of it, ready for a test to act as the client.
    /// </summary>
    /// <param name="Client">HTTP client, with its own address for the rate limits.</param>
    /// <param name="Session">Session of the photographer.</param>
    /// <param name="BookingId">Booking identifier.</param>
    /// <param name="Token">Secret token of the link.</param>
    /// <param name="ClientEmail">Email registered for the client, or one that is not registered when the booking has none.</param>
    private sealed record PortalScenario(HttpClient Client, SessionTokens Session, Guid BookingId, string Token, string ClientEmail)
    {
        /// <summary>
        /// Asks for a code, reads it from the captured email and exchanges it for a portal session.
        /// </summary>
        /// <param name="fixture">Shared API instance, whose captured emails hold the code.</param>
        /// <returns>The portal access token.</returns>
        public async Task<string> OpenPortalSessionAsync(ApiFixture fixture)
        {
            (await Client.SendAsync(HttpMethod.Post, $"/public/bookings/{Token}/code", null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
            var verified = await Client.SendAsync(HttpMethod.Post, $"/public/bookings/{Token}/code/verify", null, new { code = fixture.Emails.LastCodeFor(ClientEmail) });
            verified.StatusCode.ShouldBe(HttpStatusCode.OK);
            return (await verified.ReadJsonAsync()).GetProperty("accessToken").GetString()!;
        }
    }
}
