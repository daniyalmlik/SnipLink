namespace SnipLink.Shared.Common;

/// <summary>
/// Discriminated union for service operation outcomes.
/// Use Match to exhaustively handle all cases without null checks or exceptions.
/// </summary>
public abstract class ServiceResult<T>
{
    private ServiceResult() { }

    public sealed class Success : ServiceResult<T>
    {
        public T Value { get; }
        public Success(T value) => Value = value;
    }

    public sealed class Conflict : ServiceResult<T>
    {
        public string Message { get; }
        public Conflict(string message) => Message = message;
    }

    public sealed class Invalid : ServiceResult<T>
    {
        public string Message { get; }
        public Invalid(string message) => Message = message;
    }

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onConflict,
        Func<string, TResult> onInvalid) => this switch
    {
        Success s  => onSuccess(s.Value),
        Conflict c => onConflict(c.Message),
        Invalid i  => onInvalid(i.Message),
        _          => throw new InvalidOperationException("Unknown ServiceResult subtype.")
    };
}
