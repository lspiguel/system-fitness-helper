namespace SystemFitnessHelper.Actions;

public sealed class ActionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }

    public static ActionResult Ok(string message)
        => new() { Success = true, Message = message };

    public static ActionResult Fail(string message, Exception? ex = null)
        => new() { Success = false, Message = message, Exception = ex };
}
