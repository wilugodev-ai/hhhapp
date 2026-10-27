namespace Hhh.Domain;

public sealed record FinancialVariance(decimal? Amount, decimal? Percent, string Status)
{
    public static FinancialVariance Calculate(decimal? actual, decimal? budget, bool expense)
    {
        if (actual is null || budget is null) return new(null, null, "unavailable");
        var amount = actual.Value - budget.Value;
        return new(amount, FinancialMetrics.Change(actual.Value, budget.Value),
            amount == 0 ? "on-budget" : (expense ? amount < 0 : amount > 0) ? "favorable" : "unfavorable");
    }
}

public readonly record struct CohortKey(DateOnly Month, Guid Payer, Guid Center, Guid Provider, Guid Contract, Guid Plan, Guid LineOfBusiness, Guid Region)
{
    public static CohortKey From(FinancialSnapshot x) => new(x.EffectiveMonth, x.PayerId, x.MedicalCenterId, x.ProviderId, x.ContractId, x.PlanId, x.LineOfBusinessId, x.RegionId);
    public static CohortKey From(BudgetSnapshot x) => new(x.Month, x.PayerId, x.MedicalCenterId, x.ProviderId, x.ContractId, x.PlanId, x.LineOfBusinessId, x.RegionId);
    public CohortKey Shift(int months) => this with { Month = Month.AddMonths(months) };
}
