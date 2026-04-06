using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;

namespace SystemFitnessHelper.Ipc.Pipes;

public static class PipeFraming
{
    public static async Task WriteMessageAsync(Stream stream, string json, CancellationToken ct = default)
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        if (payload.Length > PipeConstants.MaxMessageBytes)
            throw new InvalidOperationException($"Message exceeds maximum size of {PipeConstants.MaxMessageBytes} bytes.");

        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);

        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<string> ReadMessageAsync(Stream stream, CancellationToken ct = default)
    {
        byte[] header = new byte[4];
        await ReadExactAsync(stream, header, ct).ConfigureAwait(false);

        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length < 0 || length > PipeConstants.MaxMessageBytes)
            throw new InvalidOperationException($"Invalid message length: {length}.");

        byte[] payload = new byte[length];
        await ReadExactAsync(stream, payload, ct).ConfigureAwait(false);

        return Encoding.UTF8.GetString(payload);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), ct).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Pipe closed before all bytes were read.");
            offset += read;
        }
    }
}
