using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the contract on the photographer's phone: the photographer reads the contract, the client signs it there with
/// what they declare, and the exact text is kept, shown again and emailed.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class ContractOnThePhoneApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    /// <summary>
    /// The whole flow: read the contract, sign it on the phone with the declaration, read it back as signed, see the declaration in
    /// the booking and receive the copy by email once.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Flow_ReadSignReadBackAndEmailACopy()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var email = $"cliente{Guid.NewGuid():N}@example.com";
        var booking = await client.CreateBookingAsync(session.AccessToken, new Dictionary<string, object> { ["clientEmail"] = email });
        var id = booking.GetProperty("id").GetGuid();

        var before = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}/contract", session.AccessToken)).ReadJsonAsync();
        before.GetProperty("signed").GetBoolean().ShouldBeFalse();
        before.GetProperty("text").GetString()!.ShouldContain("María Pérez");
        var version = before.GetProperty("templateVersion").GetString()!;

        var signed = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/contract/in-person", session.AccessToken, Body(version));
        signed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var contract = (await signed.ReadJsonAsync()).GetProperty("contract");
        contract.GetProperty("channel").GetString().ShouldBe("InPerson");
        contract.GetProperty("declaration").GetProperty("idNumber").GetString().ShouldBe("112345678");
        contract.GetProperty("declaration").GetProperty("imageUse").GetString().ShouldBe("NotAuthorized");

        var after = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}/contract", session.AccessToken)).ReadJsonAsync();
        after.GetProperty("signed").GetBoolean().ShouldBeTrue();
        after.GetProperty("text").GetString()!.ShouldContain("cédula 112345678");
        after.GetProperty("text").GetString()!.ShouldContain("[x] No autorizo");

        await RunMaintenancePassAsync();
        await RunMaintenancePassAsync();
        var copy = fixture.Emails.Messages
            .Where(message => string.Equals(message.To, email, StringComparison.OrdinalIgnoreCase) && message.Subject.Contains("contrato firmado", StringComparison.OrdinalIgnoreCase))
            .ShouldHaveSingleItem();
        copy.TextBody.ShouldContain("cédula 112345678");

        var again = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/contract/in-person", session.AccessToken, Body(version));
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.ReadCodeAsync()).ShouldBe("booking.contract_already_signed");
    }

    /// <summary>
    /// Another photographer cannot read or sign the contract of a booking that is not theirs, and nobody can without signing in.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task OtherPhotographersAndAnonymousCallers_AreRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var owner = await client.LoginAsync(await fixture.CreateUserAsync());
        var stranger = await client.LoginAsync(await fixture.CreateUserAsync());
        var id = (await client.CreateBookingAsync(owner.AccessToken)).GetProperty("id").GetGuid();

        (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}/contract", stranger.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/contract/in-person", stranger.AccessToken, Body("v1"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}/contract", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var seenByOwner = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}", owner.AccessToken)).ReadJsonAsync();
        seenByOwner.GetProperty("contract").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    /// <summary>
    /// Signing refuses a version of the contract that is not the current one, and an invalid identity number.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_RejectsAnOutdatedVersionAndInvalidValues()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var id = (await client.CreateBookingAsync(session.AccessToken)).GetProperty("id").GetGuid();
        var version = (await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}/contract", session.AccessToken)).ReadJsonAsync())
            .GetProperty("templateVersion").GetString()!;

        var outdated = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/contract/in-person", session.AccessToken, Body("v0"));
        var badId = await client.SendAsync(HttpMethod.Post, $"/api/bookings/{id}/contract/in-person", session.AccessToken, Body(version, idNumber: "123"));

        outdated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await outdated.ReadCodeAsync()).ShouldBe("contract.template_changed");
        badId.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await badId.ReadCodeAsync()).ShouldBe("contract.invalid_id_number");
        (await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}", session.AccessToken)).ReadJsonAsync())
            .GetProperty("contract").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    /// <summary>
    /// A contract on paper is registered with only a name: no declaration, no text, and the contract endpoint says it is signed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Paper_IsRegisteredWithoutDeclarationOrText()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateUserAsync());
        var id = (await client.CreateBookingAsync(session.AccessToken)).GetProperty("id").GetGuid();

        var signed = await client.SendAsync(
            HttpMethod.Post,
            $"/api/bookings/{id}/contract/in-person",
            session.AccessToken,
            new { signerName = "María Pérez", templateVersion = "v1", isPaperContract = true });

        signed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await signed.ReadJsonAsync()).GetProperty("contract").GetProperty("channel").GetString().ShouldBe("External");
        var contract = await (await client.SendAsync(HttpMethod.Get, $"/api/bookings/{id}/contract", session.AccessToken)).ReadJsonAsync();
        contract.GetProperty("signed").GetBoolean().ShouldBeTrue();
        contract.GetProperty("text").GetString().ShouldBeEmpty();
    }

    /// <summary>
    /// Builds the body that signs on the phone.
    /// </summary>
    /// <param name="templateVersion">Version of the contract the client read.</param>
    /// <param name="idNumber">Identity number typed by the client.</param>
    /// <returns>The request body.</returns>
    private static object Body(string templateVersion, string idNumber = "1-1234-5678") => new
    {
        signerName = "María Pérez",
        templateVersion,
        isPaperContract = false,
        declaration = new { idNumber, participantCount = 3, sessionLocation = "Parque La Sabana", imageUse = "NotAuthorized" },
    };

    /// <summary>
    /// Runs one maintenance pass in-process, the way the worker does, so the outbox delivers the events of the test.
    /// </summary>
    /// <returns>A task that completes when the pass ends.</returns>
    private async Task RunMaintenancePassAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RunMaintenanceCommand, MaintenanceResponse>>();
        await handler.HandleAsync(new RunMaintenanceCommand(), TestContext.Current.CancellationToken);
    }
}
