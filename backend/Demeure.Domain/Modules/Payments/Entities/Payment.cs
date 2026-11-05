using System.Diagnostics.CodeAnalysis;
using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Payments.Entities;

public sealed class Payment : OrganizationEntity
{
    private Payment() { }

    [SetsRequiredMembers]
    public Payment(Guid organizationId, Guid leaseId, decimal amount, string currency, DateOnly dueDate)
    {
        OrganizationId = DomainGuard.Id(organizationId, nameof(organizationId));
        LeaseId = DomainGuard.Id(leaseId, nameof(leaseId));
        Amount = DomainGuard.Money(amount, nameof(amount));
        Currency = DomainGuard.Currency(currency);
        DueDate = DomainGuard.Date(dueDate, nameof(dueDate));
    }

    public Guid LeaseId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public DateOnly DueDate { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public string? Reference { get; private set; }
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    public void Complete(DateTimeOffset paidAt, string? reference = null)
    {
        RequireStatus(PaymentStatus.Pending);
        var timestamp = DomainGuard.Instant(paidAt, nameof(paidAt));
        var validatedReference = DomainGuard.OptionalText(reference, 200, nameof(reference));
        PaidAt = timestamp;
        Reference = validatedReference;
        Status = PaymentStatus.Completed;
    }

    public void Fail()
    {
        RequireStatus(PaymentStatus.Pending);
        Status = PaymentStatus.Failed;
    }

    public void Retry()
    {
        RequireStatus(PaymentStatus.Failed);
        Status = PaymentStatus.Pending;
    }

    public void Cancel()
    {
        if (Status is not (PaymentStatus.Pending or PaymentStatus.Failed))
            throw new InvalidOperationException("Only pending or failed payments can be cancelled.");
        Status = PaymentStatus.Cancelled;
    }

    public void Refund()
    {
        RequireStatus(PaymentStatus.Completed);
        Status = PaymentStatus.Refunded;
    }

    private void RequireStatus(PaymentStatus expected)
    {
        if (Status != expected) throw new InvalidOperationException($"The payment must be {expected} for this operation.");
    }
}

public enum PaymentStatus { Pending, Completed, Failed, Cancelled, Refunded }
