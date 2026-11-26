using System.Security.Cryptography;
using System.Text;

namespace SR2MP.Shared.Utils;

internal static class GuestProfileEnvelope
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PGGUEST1");
    internal const int MaximumSize = 16777216;
    internal static byte[] Write(byte[] references, byte[] player)
    {
        if (references.Length > 4194304 || player.Length == 0 || references.Length + player.Length > MaximumSize - 48)
            throw new InvalidDataException("Invalid guest profile size.");
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Encoding.UTF8, true))
        { writer.Write(references.Length); writer.Write(references); writer.Write(player); }
        var bytes = payload.ToArray();
        return Magic.Concat(SHA256.HashData(bytes)).Concat(bytes).ToArray();
    }

    internal static (byte[]? references, byte[] player) Read(byte[] bytes)
    {
        if (bytes.Length is < 4 or > MaximumSize) throw new InvalidDataException("Invalid guest profile size.");
        if (!bytes.Take(Magic.Length).SequenceEqual(Magic)) return (null, bytes); // Legacy PlayerV12 snapshot.
        if (bytes.Length < 45) throw new InvalidDataException("Truncated guest profile.");
        var payload = bytes.AsSpan(40);
        if (!CryptographicOperations.FixedTimeEquals(bytes.AsSpan(8, 32), SHA256.HashData(payload)))
            throw new InvalidDataException("Guest profile checksum mismatch. The original file has been preserved.");
        var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload);
        if (length < 0 || length > 4194304 || length >= payload.Length - 4) throw new InvalidDataException("Invalid guest reference table.");
        return (payload.Slice(4, length).ToArray(), payload.Slice(4 + length).ToArray());
    }
}
