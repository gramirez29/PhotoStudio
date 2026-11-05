using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Contracts;

/// <summary>
/// Tests of the clauses of the contract that say what the photographer keeps or returns when a booking ends. They come from the
/// policy frozen in the booking, so the contract always says what the settlement will do, and with the usual policy the text is the
/// one the contract always had.
/// </summary>
public sealed class ContractPolicyClausesTests
{
    /// <summary>
    /// With the default policy the clauses are word for word the ones the contract had before they were built from the policy, so
    /// contracts already signed and contracts about to be signed read the same.
    /// </summary>
    [Fact]
    public void Render_WithTheDefaultPolicy_KeepsTheOriginalWording()
    {
        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingFactory.Tentative(), null, null, null);

        text.ShouldContain(
            "5. Cancelación por el Cliente. Si el Cliente cancela con al menos 72 horas de aviso, se le devuelve todo lo pagado. " +
            "Si cancela con menos de 72 horas de aviso, o no se presenta a la sesión (se considera ausente pasados 30 minutos de la hora acordada), " +
            "el Fotógrafo retiene el adelanto y devuelve cualquier monto pagado por encima de él.");
        text.ShouldContain("Si el Fotógrafo no puede realizar la sesión por enfermedad, fuerza mayor u otra causa, devuelve el 100% de lo pagado.");
    }

    /// <summary>
    /// When both cases keep the same share, one sentence says it.
    /// </summary>
    [Fact]
    public void Render_WithOneSharedRetention_SaysItOnceForBothCases()
    {
        var policy = BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, lateCancellationRetention: 0.5m, clientAbsentRetention: 0.5m);

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingWith(policy), null, null, null);

        text.ShouldContain(
            "Si cancela con menos de 72 horas de aviso, o no se presenta a la sesión (se considera ausente pasados 30 minutos de la hora acordada), " +
            "el Fotógrafo retiene el 50% del adelanto y devuelve todo lo demás que haya pagado.");
    }

    /// <summary>
    /// When the two cases keep different shares, each gets its own sentence.
    /// </summary>
    [Fact]
    public void Render_WithDifferentRetentions_SaysEachCaseApart()
    {
        var policy = BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, lateCancellationRetention: 0.5m, clientAbsentRetention: 1m);

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingWith(policy), null, null, null);

        text.ShouldContain("Si cancela con menos de 72 horas de aviso, el Fotógrafo retiene el 50% del adelanto y devuelve todo lo demás que haya pagado.");
        text.ShouldContain(
            "Si no se presenta a la sesión (se considera ausente pasados 30 minutos de la hora acordada), " +
            "el Fotógrafo retiene el adelanto y devuelve cualquier monto pagado por encima de él.");
    }

    /// <summary>
    /// A policy that keeps nothing says so, instead of saying it keeps a share of zero.
    /// </summary>
    [Fact]
    public void Render_WithNothingRetained_SaysNothingIsKept()
    {
        var policy = BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, lateCancellationRetention: 0m, clientAbsentRetention: 0m);

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingWith(policy), null, null, null);

        text.ShouldContain("el Fotógrafo no retiene nada del adelanto y devuelve todo lo pagado.");
        text.ShouldNotContain("retiene el 0%");
    }

    /// <summary>
    /// The refund when the photographer cancels is the one of the policy.
    /// </summary>
    [Fact]
    public void Render_UsesTheRefundOfThePolicyWhenThePhotographerCancels()
    {
        var policy = BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, photographerCancellationRefund: 0.75m);

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), BookingWith(policy), null, null, null);

        text.ShouldContain("devuelve el 75% de lo pagado.");
        text.ShouldNotContain("devuelve el 100% de lo pagado");
    }

    /// <summary>
    /// Builds a booking under a policy.
    /// </summary>
    /// <param name="policy">Policy frozen into the booking.</param>
    /// <returns>The booking.</returns>
    private static Booking BookingWith(BookingPolicy policy) => Booking.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        ClientContact.Create("María Pérez", "+506 8888-8888"),
        "Retrato familiar",
        Money.Create(100_000m, "CRC"),
        TimeSlot.Create(BookingFactory.SessionStart, BookingFactory.SessionStart.AddHours(2)),
        policy,
        BookingFactory.CreatedAt);
}
