namespace SystemFitnessHelper.Ipc.Protocol;

public sealed class JsonRpcException : Exception
{
    public JsonRpcErrorCode Code { get; }

    public JsonRpcException(JsonRpcErrorCode code, string message)
        : base(message)
    {
        this.Code = code;
    }
}
