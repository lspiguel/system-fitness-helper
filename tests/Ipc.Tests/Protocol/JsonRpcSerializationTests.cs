using System.Text.Json;
using FluentAssertions;
using SystemFitnessHelper.Ipc.Protocol;
using Xunit;

namespace SystemFitnessHelper.Ipc.Tests.Protocol;

public sealed class JsonRpcSerializationTests
{
    [Fact]
    public void JsonRpcRequest_RoundTrip_PreservesAllFields()
    {
        JsonRpcRequest request = new()
        {
            Id = 42,
            Method = "sfh.list",
            Params = JsonSerializer.SerializeToElement(new { configPath = (string?)null, ruleSetName = "work" }),
        };

        string json = JsonSerializer.Serialize(request);
        JsonRpcRequest? deserialized = JsonSerializer.Deserialize<JsonRpcRequest>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Jsonrpc.Should().Be("2.0");
        deserialized.Id.Should().Be(42);
        deserialized.Method.Should().Be("sfh.list");
        deserialized.Params.Should().NotBeNull();
    }

    [Fact]
    public void JsonRpcResponse_Success_RoundTrip()
    {
        JsonRpcResponse response = JsonRpcResponse.Success(1, new { value = "hello" });

        string json = JsonSerializer.Serialize(response);
        JsonRpcResponse? deserialized = JsonSerializer.Deserialize<JsonRpcResponse>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Jsonrpc.Should().Be("2.0");
        deserialized.Id.Should().Be(1);
        deserialized.Result.Should().NotBeNull();
        deserialized.Error.Should().BeNull();
    }

    [Fact]
    public void JsonRpcResponse_Failure_RoundTrip()
    {
        JsonRpcResponse response = JsonRpcResponse.Failure(5, JsonRpcErrorCode.MethodNotFound, "No such method");

        string json = JsonSerializer.Serialize(response);
        JsonRpcResponse? deserialized = JsonSerializer.Deserialize<JsonRpcResponse>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(5);
        deserialized.Result.Should().BeNull();
        deserialized.Error.Should().NotBeNull();
        deserialized.Error!.Code.Should().Be((int)JsonRpcErrorCode.MethodNotFound);
        deserialized.Error.Message.Should().Be("No such method");
    }

    [Fact]
    public void JsonRpcNotification_RoundTrip_PreservesAllFields()
    {
        JsonRpcNotification notification = new()
        {
            Method = "sfh.action.executed",
            Params = JsonSerializer.SerializeToElement(new { processName = "chrome" }),
        };

        string json = JsonSerializer.Serialize(notification);
        JsonRpcNotification? deserialized = JsonSerializer.Deserialize<JsonRpcNotification>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Jsonrpc.Should().Be("2.0");
        deserialized.Method.Should().Be("sfh.action.executed");
        deserialized.Params.Should().NotBeNull();
    }

    [Theory]
    [InlineData(JsonRpcErrorCode.ParseError, -32700)]
    [InlineData(JsonRpcErrorCode.InvalidRequest, -32600)]
    [InlineData(JsonRpcErrorCode.MethodNotFound, -32601)]
    [InlineData(JsonRpcErrorCode.InternalError, -32603)]
    [InlineData(JsonRpcErrorCode.ConfigNotFound, -32000)]
    [InlineData(JsonRpcErrorCode.RuleSetNotFound, -32001)]
    [InlineData(JsonRpcErrorCode.ExecutionFailed, -32002)]
    public void JsonRpcErrorCode_HasExpectedValues(JsonRpcErrorCode code, int expectedValue)
    {
        ((int)code).Should().Be(expectedValue);
    }
}
