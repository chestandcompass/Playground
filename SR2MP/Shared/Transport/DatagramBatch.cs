using System.Buffers.Binary;

namespace SR2MP.Shared.Transport;

// Preserve UDP datagram boundaries inside one bounded WebSocket message.
internal static class DatagramBatch
{
    internal const int MaxFrame = 16384;
    internal const int MaxDatagram = 2048;
    internal const int MaxCount = 512;
    internal static byte[] Encode(IReadOnlyList<byte[]> packets)
    {
        if (packets.Count is < 1 or > MaxCount) throw new InvalidDataException("Invalid datagram count.");
        var length = 6;
        foreach (var packet in packets)
        {
            if (packet.Length is < 1 or > MaxDatagram) throw new InvalidDataException("Invalid datagram size.");
            length = checked(length + 2 + packet.Length);
        }
        if (length > MaxFrame) throw new InvalidDataException("Batch too large.");
        var frame = new byte[length];
        frame[0] = (byte)'P'; frame[1] = (byte)'G'; frame[2] = (byte)'D'; frame[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), (ushort)packets.Count);
        var offset = 6;
        foreach (var packet in packets)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), (ushort)packet.Length);
            packet.CopyTo(frame, offset + 2); offset += 2 + packet.Length;
        }
        return frame;
    }
    internal static byte[][] Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length is < 6 or > MaxFrame || frame[0] != 'P' || frame[1] != 'G' || frame[2] != 'D' || frame[3] != 1)
            throw new InvalidDataException("Invalid relay frame.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(4));
        if (count is < 1 or > MaxCount) throw new InvalidDataException("Invalid datagram count.");
        // Validate the entire message before releasing any datagram to gameplay.
        var offset = 6;
        for (var i = 0; i < count; i++)
        {
            if (offset + 2 > frame.Length) throw new InvalidDataException("Truncated relay frame.");
            var size = BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(offset)); offset += 2;
            if (size is < 1 or > MaxDatagram || size > frame.Length - offset) throw new InvalidDataException("Invalid datagram size.");
            offset += size;
        }
        if (offset != frame.Length) throw new InvalidDataException("Trailing relay data.");
        var packets = new byte[count][]; offset = 6;
        for (var i = 0; i < count; i++)
        {
            var size = BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(offset)); offset += 2;
            packets[i] = frame.Slice(offset, size).ToArray(); offset += size;
        }
        return packets;
    }
}
