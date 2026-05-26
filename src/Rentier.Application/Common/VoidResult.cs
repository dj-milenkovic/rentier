namespace Rentier.Application.Common;

public sealed record VoidResult
{
    public static readonly VoidResult Value = new();
    private VoidResult() { }
}
