using System.Buffers.Binary;

namespace Protocol;

public class Protocol
{
    public const int MESSAGE_MAX_BYTE_SIZE = 1024 * 32;

    public enum DataType : uint
    {
        MessageTextUTF8,
        UserNameUTF8,
        ChatMessageJson
    }

    public record ChatMessage(string SenderName, string Text, DateTimeOffset Timestamp);

    public record OutgoingData(DataType Type, ReadOnlyMemory<byte> Data);

    public record ReceivedData(DataType Type, byte[] Data);

    public static async Task WriteAsync(Stream stream, OutgoingData data, CancellationToken cancellationToken)
    {
        if (data.Data.Length > MESSAGE_MAX_BYTE_SIZE)
        {
            throw new ArgumentException($"Message exceeds the maximum size of {MESSAGE_MAX_BYTE_SIZE} bytes.", nameof(data));
        }

        var buffer = new byte[sizeof(uint) * 2 + data.Data.Length];

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, sizeof(uint)), (uint)data.Data.Length);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(sizeof(uint), sizeof(uint)), (uint)data.Type);

        data.Data.Span.CopyTo(buffer.AsSpan(sizeof(uint) * 2));

        await stream.WriteAsync(buffer, cancellationToken);
    }

    public static async Task<ReceivedData?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[4];

        if (!await ReadExactlyAsync(stream, lengthBytes, cancellationToken))
        {
            return null;
        }

        var dataLength = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);

        if (dataLength > MESSAGE_MAX_BYTE_SIZE)
        {
            throw new InvalidDataException($"Message exceeds the maximum size of {MESSAGE_MAX_BYTE_SIZE} bytes.");
        }

        var typeBytes = new byte[4];

        if (!await ReadExactlyAsync(stream, typeBytes, cancellationToken))
        {
            return null;
        }

        var typeValue = BinaryPrimitives.ReadUInt32BigEndian(typeBytes);

        if (!Enum.IsDefined(typeof(DataType), typeValue))
        {
            throw new InvalidDataException($"Unknown data type: {typeValue}");
        }

        var type = (DataType)typeValue;

        var data = new byte[dataLength];

        if (!await ReadExactlyAsync(stream, data, cancellationToken))
        {
            return null;
        }

        return new ReceivedData(type, data);
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var totalBytesRead = 0;

        while (totalBytesRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer[totalBytesRead..], cancellationToken);

            if (bytesRead <= 0)
            {
                return false;
            }

            totalBytesRead += bytesRead;
        }

        return true;
    }
}
