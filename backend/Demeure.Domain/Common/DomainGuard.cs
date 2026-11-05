using System.Net.Mail;

namespace Demeure.Domain.Common;

internal static class DomainGuard
{
    internal static Guid Id(Guid value, string name)
    {
        if (value == Guid.Empty) throw new ArgumentException("An identifier must not be empty.", name);
        return value;
    }

    internal static Guid? OptionalId(Guid? value, string name) => value.HasValue ? Id(value.Value, name) : null;

    internal static string Text(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ArgumentException($"A nonblank value of at most {maxLength} characters is required.", name);
        return value.Trim();
    }

    internal static string? OptionalText(string? value, int maxLength, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : Text(value, maxLength, name);

    internal static string Email(string? value, string name)
    {
        var email = Text(value, 254, name);
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email ||
            !email.Contains('@') || email.Any(char.IsWhiteSpace))
            throw new ArgumentException("A valid email address is required.", name);
        return email;
    }

    internal static T Defined<T>(T value, string name) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(name, value, "An undefined enum value is not allowed.");
        return value;
    }

    internal static void Transition<T>(T current, T target, bool allowed) where T : struct, Enum
    {
        Defined(target, nameof(target));
        if (!allowed) throw new InvalidOperationException($"Cannot transition from {current} to {target}.");
    }

    internal static decimal Money(decimal value, string name, bool allowZero = false)
    {
        if (value < 0 || !allowZero && value == 0 || value > 9999999999999999.99m ||
            decimal.Round(value, 2) != value)
            throw new ArgumentOutOfRangeException(name, value,
                allowZero ? "Money must fit decimal(18,2) and be nonnegative." : "Money must fit decimal(18,2) and be positive.");
        return value;
    }

    internal static string Currency(string? value)
    {
        var currency = Text(value, 3, nameof(value)).ToUpperInvariant();
        if (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Currency must be a three-letter code.", nameof(value));
        return currency;
    }

    internal static DateOnly Date(DateOnly value, string name)
    {
        if (value == default) throw new ArgumentException("A calendar date is required.", name);
        return value;
    }

    internal static DateTimeOffset Instant(DateTimeOffset value, string name)
    {
        if (value == default) throw new ArgumentException("A timestamp is required.", name);
        return value.ToUniversalTime();
    }
}
