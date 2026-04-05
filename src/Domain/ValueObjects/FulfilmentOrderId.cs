namespace Elsa.Workflow.Domain.ValueObjects;

public readonly record struct FulfilmentOrderId(Guid Value)
{
    public static FulfilmentOrderId From(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("FulfilmentOrderId cannot be an empty GUID.", nameof(id));

        return new FulfilmentOrderId(id);
    }

    public override string ToString() => Value.ToString();
}
