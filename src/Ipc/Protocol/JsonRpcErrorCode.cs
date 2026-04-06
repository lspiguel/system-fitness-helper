namespace SystemFitnessHelper.Ipc.Protocol;

public enum JsonRpcErrorCode
{
    ParseError = -32700,
    InvalidRequest = -32600,
    MethodNotFound = -32601,
    InternalError = -32603,
    ConfigNotFound = -32000,
    RuleSetNotFound = -32001,
    ExecutionFailed = -32002,
}
