using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Contracts;

/// <summary>
/// Tests of <see cref="ContractTemplates"/> and <see cref="ContractRenderer"/>.
/// </summary>
public sealed class ContractRendererTests
{
    private static readonly DateTimeOffset SignedAt = new(2026, 10, 2, 21, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// The current template ships inside the assembly.
    /// </summary>
    [Fact]
    public void GetCurrent_ReturnsTheEmbeddedTemplate()
    {
        var template = ContractTemplates.GetCurrent();

        template.ShouldStartWith("CONTRATO DE SERVICIOS FOTOGRÁFICOS");
        template.ShouldNotContain("\r");
    }

    /// <summary>
    /// A version that is not embedded, or that is not shaped like a version, finds nothing.
    /// </summary>
    /// <param name="version">Version to look for.</param>
    [Theory]
    [InlineData("v999")]
    [InlineData("")]
    [InlineData("../v1")]
    [InlineData("v1.0.0.0.0")]
    public void Find_WithAnUnknownVersion_ReturnsNull(string version)
    {
        ContractTemplates.Find(version).ShouldBeNull();
    }

    /// <summary>
    /// Every variable the template uses is one the renderer knows, so a typo in the wording cannot reach a client as raw braces.
    /// </summary>
    [Fact]
    public void Template_UsesOnlyVariablesTheRendererKnows()
    {
        var used = ContractRenderer.UsedVariables(ContractTemplates.GetCurrent());
        var known = ContractRenderer.KnownVariables(BookingFactory.Tentative());

        used.Except(known).ShouldBeEmpty();
        used.ShouldNotBeEmpty();
    }

    /// <summary>
    /// A template that uses a variable that does not exist fails loudly.
    /// </summary>
    [Fact]
    public void Render_WithAnUnknownVariable_Throws()
    {
        Should.Throw<InvalidOperationException>(() =>
            ContractRenderer.Render("Hola {{nope}}", BookingFactory.Tentative(), null, null, null));
    }

    /// <summary>
    /// Before signing, the booking data is filled in and what only the client knows stays blank.
    /// </summary>
    [Fact]
    public void Render_BeforeSigning_FillsTheBookingAndLeavesTheClientBlanksEmpty()
    {
        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingFactory.Tentative(), null, null, null);

        text.ShouldContain("María Pérez");
        text.ShouldContain("Retrato familiar");
        text.ShouldContain("domingo 11 de octubre de 2026 a las 6:00 a. m.");
        text.ShouldContain("Total: ₡100 000");
        text.ShouldContain("el 50% (₡50 000)");
        text.ShouldContain($"cédula {ContractRenderer.Blank}");
        text.ShouldContain($"para {ContractRenderer.Blank} personas");
        text.ShouldContain("[ ] Autorizo");
        text.ShouldContain("[ ] No autorizo");
        text.ShouldNotContain("{{");
    }

    /// <summary>
    /// The numbers of the clauses come from the policy frozen in the booking, not from fixed text.
    /// </summary>
    [Fact]
    public void Render_UsesThePolicyOfTheBooking()
    {
        var policy = BookingPolicy.Create(24, 0.3m, 96, 12, 2, 15, 7);
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ClientContact.Create("María Pérez", "+506 8888-8888"),
            "Retrato familiar",
            Money.Create(100_000m, "CRC"),
            TimeSlot.Create(BookingFactory.SessionStart, BookingFactory.SessionStart.AddHours(2)),
            policy,
            BookingFactory.CreatedAt);

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), booking, null, null, null);

        text.ShouldContain("apartada de forma provisional por 24 horas");
        text.ShouldContain("el 30% (₡30 000)");
        text.ShouldContain("El 70% restante (₡70 000)");
        text.ShouldContain("Con al menos 12 horas de aviso, el Cliente puede reprogramar la sesión hasta 2 veces");
        text.ShouldContain("Si el Cliente cancela con al menos 96 horas de aviso");
        text.ShouldContain("pasados 15 minutos");
    }

    /// <summary>
    /// A policy that lets the client reschedule once says so in words.
    /// </summary>
    [Fact]
    public void Render_WithTheDefaultPolicy_SaysTheClientCanRescheduleOnce()
    {
        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingFactory.Tentative(), null, null, null);

        text.ShouldContain("Con al menos 48 horas de aviso, el Cliente puede reprogramar la sesión una sola vez");
        text.ShouldContain("Si el Cliente cancela con al menos 72 horas de aviso");
    }

    /// <summary>
    /// After signing, the declaration, the signer and the date fill the blanks and the chosen image option is ticked.
    /// </summary>
    /// <param name="imageUse">Choice of the client.</param>
    /// <param name="authorizedMark">Expected mark of the "authorize" option.</param>
    /// <param name="deniedMark">Expected mark of the "do not authorize" option.</param>
    [Theory]
    [InlineData(ImageUse.Authorized, "[x]", "[ ]")]
    [InlineData(ImageUse.NotAuthorized, "[ ]", "[x]")]
    public void Render_AfterSigning_FillsTheDeclarationAndTicksTheChosenImageOption(ImageUse imageUse, string authorizedMark, string deniedMark)
    {
        var declaration = ClientDeclaration.Create("1-1234-5678", 4, "Parque La Sabana", imageUse);

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingFactory.Tentative(), declaration, "  María Pérez  ", SignedAt);

        text.ShouldContain("cédula 112345678");
        text.ShouldContain("en Parque La Sabana, para 4 personas");
        text.ShouldContain($"{authorizedMark} Autorizo");
        text.ShouldContain($"{deniedMark} No autorizo");
        text.ShouldContain("Firmado electrónicamente el viernes 2 de octubre de 2026, 3:30 p. m.");
        text.ShouldContain($"El Cliente (o representante legal): María Pérez    Cédula: 112345678");
        text.ShouldNotContain(ContractRenderer.Blank);
    }

    /// <summary>
    /// A client who leaves the location blank is told it will be agreed with the photographer, and no blank is left in the text.
    /// </summary>
    [Fact]
    public void Render_WithoutALocation_SaysItWillBeAgreedWithThePhotographer()
    {
        var declaration = ClientDeclaration.Create("112345678", 2, null, ImageUse.Authorized);

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingFactory.Tentative(), declaration, "María Pérez", SignedAt);

        text.ShouldContain("en un lugar que se definirá con el Fotógrafo");
    }

    /// <summary>
    /// A Costa Rican phone is written as 7018-9220; another shape is left as it was saved.
    /// </summary>
    /// <param name="saved">Phone as saved in the booking.</param>
    /// <param name="expected">Phone as written in the contract.</param>
    [Theory]
    [InlineData("+50670189220", "teléfono 7018-9220")]
    [InlineData("+12025550123", "teléfono +12025550123")]
    public void Render_WritesThePhone(string saved, string expected)
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ClientContact.Create("María Pérez", saved),
            "Retrato familiar",
            Money.Create(100_000m, "CRC"),
            TimeSlot.Create(BookingFactory.SessionStart, BookingFactory.SessionStart.AddHours(2)),
            BookingPolicy.Default,
            BookingFactory.CreatedAt);

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), booking, null, null, null);

        text.ShouldContain(expected);
    }

    /// <summary>
    /// The contract names the package, lists the extras and shows how the total is made up, so the client signs exactly what was sold.
    /// </summary>
    [Fact]
    public void Render_WithPackageExtrasAndDiscount_ShowsWhatWasSold()
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ClientContact.Create("María Pérez", "+506 8888-8888"),
            "Retrato familiar",
            Money.Create(65_000m, "CRC"),
            TimeSlot.Create(BookingFactory.SessionStart, BookingFactory.SessionStart.AddHours(2)),
            BookingPolicy.Default,
            BookingFactory.CreatedAt,
            PackageTier.Standard,
            [BookingExtra.Create("extra-hour", "Hora adicional", Money.Create(30_000m, "CRC"), 2)],
            Money.Create(5_000m, "CRC"));

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), booking, null, null, null);

        text.ShouldContain("Sesión de Retrato familiar, con el paquete Estándar,");
        text.ShouldContain("Extras contratados: Hora adicional × 2 (₡60 000).");
        text.ShouldContain("Total: ₡120 000 (paquete ₡65 000, extras ₡60 000, descuento -₡5 000)");
    }

    /// <summary>
    /// A booking sold as the package alone reads as before: no package text, no extras and no breakdown.
    /// </summary>
    [Fact]
    public void Render_WithThePackageAlone_AddsNothing()
    {
        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingFactory.Tentative(), null, null, null);

        text.ShouldContain("Sesión de Retrato familiar, el ");
        text.ShouldNotContain("Extras contratados");
        text.ShouldContain("Total: ₡100 000, más desplazamiento si aplica.");
    }

    /// <summary>
    /// The new wording is version 2 and version 1 stays readable for the contracts already signed with it.
    /// </summary>
    [Fact]
    public void CurrentVersion_IsTwo_AndTheFirstOneIsStillThere()
    {
        ContractTemplates.CurrentVersion.ShouldBe("v2");
        ContractTemplates.Find("v1").ShouldNotBeNull();
        ContractTemplates.Find("v1")!.ShouldNotContain("{{extrasText}}");
    }
}
