using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Expenses.Entities;

public sealed class Expense : OrganizationEntity
{
    public required Guid PropertyId { get; init; }
    public Guid? UnitId { get; init; }
    public Guid? ContractorId { get; init; }
    public required string Description { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required DateOnly IncurredDate { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;
}

public enum ExpenseStatus { Draft, Approved, Paid, Cancelled }
