using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using SystemFitnessHelper.Ipc.Protocol;

namespace SystemFitnessHelper.Ipc.Pipes;

public sealed class CommandPipeClient
{
    private static int _nextId;

    /// <summary>Applies to the whole call, not just the connect.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Sends a JSON-RPC request and awaits its response.
    /// </summary>
    /// <param name="overallTimeout">
    /// Bounds connect, write and read together. A previous version timed out only the connect, so
    /// a service that accepted the connection but then stalled hung the calling UI thread forever.
    /// </param>
    public async Task<TResult> SendAsync<TResult>(
        string method,
        object? @params,
        CancellationToken ct = default,
        TimeSpan? overallTimeout = null)
    {
        int id = System.Threading.Interlocked.Increment(ref _nextId);

        JsonRpcRequest request = new()
        {
            Id = id,
            Method = method,
            Params = @params is null ? null : JsonSerializer.SerializeToElement(@params),
        };

        string requestJson = JsonSerializer.Serialize(request);

        using NamedPipeClientStream pipe = new(
            ".",
            PipeConstants.SfhCommand,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        TimeSpan timeout = overallTimeout ?? DefaultTimeout;
        using CancellationTokenSource timeoutCts = new(timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        string responseJson;
        try
        {
            await pipe.ConnectAsync(linkedCts.Token).ConfigureAwait(false);
            await PipeFraming.WriteMessageAsync(pipe, requestJson, linkedCts.Token).ConfigureAwait(false);
            responseJson = await PipeFraming.ReadMessageAsync(pipe, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No response from the SystemFitnessHelper service within {timeout.TotalSeconds:0.#} seconds.");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException(
                "Access to the SystemFitnessHelper service pipe was denied. The service may be running " +
                "with a pipe ACL that excludes the current user.",
                ex);
        }

        JsonRpcResponse? response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson)
            ?? throw new InvalidOperationException("Received null response from service.");

        if (response.Error is not null)
            throw new JsonRpcException((JsonRpcErrorCode)response.Error.Code, response.Error.Message);

        if (response.Result is null)
            return default!;

        return JsonSerializer.Deserialize<TResult>(response.Result.Value.GetRawText())
            ?? throw new InvalidOperationException("Failed to deserialize response result.");
    }
}
