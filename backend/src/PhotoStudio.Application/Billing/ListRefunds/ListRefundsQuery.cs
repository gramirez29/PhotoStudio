namespace PhotoStudio.Application.Billing.ListRefunds;

/// <summary>
/// Query for the refunds a photographer still has to give back.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
public sealed record ListRefundsQuery(Guid PhotographerId);
