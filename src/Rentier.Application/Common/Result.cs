namespace Rentier.Application.Common;

public sealed class Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    public bool IsSuccess { get; }
    public TValue Value => IsSuccess ? _value! : throw new InvalidOperationException("Result is failure.");
    public TError Error => !IsSuccess ? _error! : throw new InvalidOperationException("Result is success.");

    private Result(TValue value) { _value = value; IsSuccess = true; }
    private Result(TError error) { _error = error; IsSuccess = false; }

    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);
}
