namespace OptimizeAll.SharedKernel.Results;

/// <summary>The outcome of an operation that can fail for expected reasons.</summary>
public class Result
{
    private readonly Error? _error;

    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error is null)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>The failure reason. Throws when the result is a success — check <see cref="IsFailure"/> first.</summary>
    public Error Error => _error
        ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Succeeded(value);

    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failed(error);

    /// <summary>Returns the first failure among the supplied results, or success when all succeeded.</summary>
    public static Result FirstFailureOrSuccess(params Result[] results)
    {
        foreach (Result result in results)
        {
            if (result.IsFailure)
            {
                return Failure(result.Error);
            }
        }

        return Success();
    }
}

/// <summary>The outcome of an operation that yields a value on success.</summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(bool isSuccess, TValue? value, Error? error)
        : base(isSuccess, error)
        => _value = value;

    /// <summary>The produced value. Throws when the result is a failure — check <see cref="Result.IsSuccess"/> first.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A failed result has no value. Error: {Error}");

    public static implicit operator Result<TValue>(TValue value) => Succeeded(value);

    public static implicit operator Result<TValue>(Error error) => Failed(error);

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<Error, TResult> onFailure)
        => IsSuccess ? onSuccess(Value) : onFailure(Error);

    public Result<TNext> Map<TNext>(Func<TValue, TNext> transform)
        => IsSuccess ? Result<TNext>.Succeeded(transform(Value)) : Result<TNext>.Failed(Error);

    public Result<TNext> Bind<TNext>(Func<TValue, Result<TNext>> next)
        => IsSuccess ? next(Value) : Result<TNext>.Failed(Error);

    internal static Result<TValue> Succeeded(TValue value) => new(true, value, null);

    internal static Result<TValue> Failed(Error error) => new(false, default, error);
}
