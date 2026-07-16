namespace InstaSafe.Domain.ValueObjects;

public record Money(decimal Amount, string Currency = "NGN")
{
    public decimal Amount { get; init; } = Amount >= 0 ? Amount : throw new ArgumentException("Amount cannot be negative.", nameof(Amount));
}
