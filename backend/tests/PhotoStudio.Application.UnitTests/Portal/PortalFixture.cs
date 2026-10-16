using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Portal;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.UnitTests.Portal;

/// <summary>
/// Shared setup of the portal handler tests: substituted ports, a fixed clock, a booking with a client email and an active link
/// whose token is <see cref="Token"/>, so each test only changes what it is about.
/// </summary>
internal sealed class PortalFixture
{
    /// <summary>
    /// Secret token of the link of the fixture.
    /// </summary>
    public const string Token = "link-token";

    /// <summary>
    /// Instant returned by the clock of every test.
    /// </summary>
    public static readonly DateTimeOffset Now = BookingFactory.CreatedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalFixture"/> class and wires the substitutes to the booking and the link.
    /// </summary>
    public PortalFixture()
    {
        Booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ClientContact.Create("María Pérez", "+506 8888-8888", "maria@example.com"),
            "Retrato familiar",
            Money.Create(100_000m, "CRC"),
            TimeSlot.Create(BookingFactory.SessionStart, BookingFactory.SessionStart.AddHours(2)),
            BookingPolicy.Default,
            Now);
        Link = BookingLink.Issue(Booking.Id, Booking.PhotographerId, "hash-of-" + Token, Now, Now.AddDays(30));

        Tokens.Hash(Arg.Any<string>()).Returns(call => "hash-of-" + call.Arg<string>());
        Tokens.Generate().Returns(new GeneratedRefreshToken("new-token", "hash-of-new-token"));
        Links.GetByTokenHashAsync("hash-of-" + Token, Arg.Any<CancellationToken>()).Returns(Link);
        Bookings.GetByIdAsync(Booking.Id, Arg.Any<CancellationToken>()).Returns(Booking);
        Users.GetByIdAsync(Booking.PhotographerId, Arg.Any<CancellationToken>())
            .Returns(Domain.Identity.User.Restore(Booking.PhotographerId, 1, "ana", "ana@example.com", "hash", "Ana Pérez", "+50670189220", Now, 0, null));
        CodeService.Generate().Returns("123456");
        CodeService.Hash(Arg.Any<Guid>(), Arg.Any<string>()).Returns(call => "code-hash-" + call.Arg<string>());
        CodeService.Matches(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(0) == call.ArgAt<string>(1));
        Sessions.Issue(Booking.Id, Arg.Any<DateTimeOffset>()).Returns(new AccessToken("portal-jwt", Now.AddMinutes(30)));
    }

    /// <summary>Gets the booking the link opens, tentative and with a client email.</summary>
    public Booking Booking { get; }

    /// <summary>Gets the active link of the booking.</summary>
    public BookingLink Link { get; }

    /// <summary>Gets the substituted booking repository.</summary>
    public IBookingRepository Bookings { get; } = Substitute.For<IBookingRepository>();

    /// <summary>Gets the substituted link repository.</summary>
    public IBookingLinkRepository Links { get; } = Substitute.For<IBookingLinkRepository>();

    /// <summary>Gets the substituted verification code repository.</summary>
    public IPortalOtpRepository Codes { get; } = Substitute.For<IPortalOtpRepository>();

    /// <summary>Gets the substituted user repository.</summary>
    public IUserRepository Users { get; } = Substitute.For<IUserRepository>();

    /// <summary>Gets the substituted code service.</summary>
    public IOtpCodeService CodeService { get; } = Substitute.For<IOtpCodeService>();

    /// <summary>Gets the substituted token generator.</summary>
    public IRefreshTokenGenerator Tokens { get; } = Substitute.For<IRefreshTokenGenerator>();

    /// <summary>Gets the substituted email sender.</summary>
    public IEmailSender Email { get; } = Substitute.For<IEmailSender>();

    /// <summary>Gets the substituted portal session issuer.</summary>
    public IPortalSessionIssuer Sessions { get; } = Substitute.For<IPortalSessionIssuer>();

    /// <summary>Gets the portal settings.</summary>
    public PortalSettings Settings { get; } = new("https://book.example.test");

    /// <summary>Gets the clock, fixed at <see cref="Now"/>.</summary>
    public TimeProvider Clock { get; } = new FixedTimeProvider(Now);

    /// <summary>
    /// Builds the record of a code that was sent.
    /// </summary>
    /// <param name="code">Code the record belongs to.</param>
    /// <param name="sentAgo">How long before <see cref="Now"/> it was sent.</param>
    /// <param name="failedAttempts">Wrong guesses already counted.</param>
    /// <param name="sendCount">Codes sent in the current window.</param>
    /// <returns>The record.</returns>
    public PortalOtp StoredCode(string code = "123456", TimeSpan? sentAgo = null, int failedAttempts = 0, int sendCount = 1)
    {
        var sentAt = Now - (sentAgo ?? TimeSpan.FromMinutes(1));
        return new PortalOtp(Booking.Id, "code-hash-" + code, sentAt + PortalOtp.Lifetime, sentAt, failedAttempts, sendCount, sentAt);
    }
}
