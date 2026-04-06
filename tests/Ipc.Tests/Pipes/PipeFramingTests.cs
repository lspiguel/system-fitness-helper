using FluentAssertions;
using SystemFitnessHelper.Ipc.Pipes;
using Xunit;

namespace SystemFitnessHelper.Ipc.Tests.Pipes;

public sealed class PipeFramingTests
{
    [Fact]
    public async Task WriteAndRead_RoundTrip_PreservesMessage()
    {
        const string message = """{"jsonrpc":"2.0","id":1,"method":"sfh.list","params":null}""";
        using MemoryStream stream = new();

        await PipeFraming.WriteMessageAsync(stream, message);
        stream.Position = 0;
        string result = await PipeFraming.ReadMessageAsync(stream);

        result.Should().Be(message);
    }

    [Fact]
    public async Task WriteAndRead_EmptyString_RoundTrip()
    {
        using MemoryStream stream = new();

        await PipeFraming.WriteMessageAsync(stream, string.Empty);
        stream.Position = 0;
        string result = await PipeFraming.ReadMessageAsync(stream);

        result.Should().Be(string.Empty);
    }

    [Fact]
    public async Task WriteAndRead_UnicodeContent_RoundTrip()
    {
        const string message = "{ \"value\": \"日本語テスト\" }";
        using MemoryStream stream = new();

        await PipeFraming.WriteMessageAsync(stream, message);
        stream.Position = 0;
        string result = await PipeFraming.ReadMessageAsync(stream);

        result.Should().Be(message);
    }

    [Fact]
    public async Task WriteAndRead_MultipleMessages_InSequence()
    {
        string[] messages = ["first", "second", "third"];
        using MemoryStream stream = new();

        foreach (string msg in messages)
            await PipeFraming.WriteMessageAsync(stream, msg);

        stream.Position = 0;
        foreach (string expected in messages)
        {
            string actual = await PipeFraming.ReadMessageAsync(stream);
            actual.Should().Be(expected);
        }
    }

    [Fact]
    public async Task WriteMessage_ExceedsMaxSize_Throws()
    {
        string hugMessage = new('x', PipeConstants.MaxMessageBytes + 1);
        using MemoryStream stream = new();

        Func<Task> act = () => PipeFraming.WriteMessageAsync(stream, hugMessage);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReadMessage_StreamClosed_ThrowsEndOfStream()
    {
        using MemoryStream stream = new(new byte[] { 10, 0, 0, 0 }); // little-endian 10: says 10 bytes but none follow

        Func<Task> act = () => PipeFraming.ReadMessageAsync(stream);

        await act.Should().ThrowAsync<EndOfStreamException>();
    }
}
