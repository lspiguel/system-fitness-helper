using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using SystemFitnessHelper.Ipc.Protocol;

namespace SystemFitnessHelper.Ipc.Pipes;

public sealed class CommandPipeClient
{
    private static int _nextId;

    public async Task<TResult> SendAsync<TResult>(string method, object? @params, CancellationToken ct = default)
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

        using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(5));
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await pipe.ConnectAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException("Could not connect to the SystemFitnessHelper service within 5 seconds.");
        }

        await PipeFraming.WriteMessageAsync(pipe, requestJson, ct).ConfigureAwait(false);
        string responseJson = await PipeFraming.ReadMessageAsync(pipe, ct).ConfigureAwait(false);

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
