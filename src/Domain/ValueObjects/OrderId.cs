namespace Elsa.Workflow.Domain.ValueObjects;

public readonly record struct OrderId(string Value)
{
    public static OrderId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OrderId cannot be null or whitespace.", nameof(value));

        return new OrderId(value.Trim());
    }

    public override string ToString() => Value;
}
