namespace Elsa.Workflow.Domain.ValueObjects;

public readonly record struct StoreId(string Value)
{
    public static StoreId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("StoreId cannot be null or whitespace.", nameof(value));

        return new StoreId(value.Trim());
    }

    public override string ToString() => Value;
}
