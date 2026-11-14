using System.Buffers.Binary;

namespace CinzasDaMare.Shared.Net;

/// <summary>Quadros com prefixo de 4 bytes (big-endian) + payload. Tamanho máximo protege contra quadros gigantes.</summary>
public static class Framing
{
    // 256 KB: os mapas do midgame (centenas de obstáculos/props) passam de 64 KB na mensagem de mapa.
    public const int MaxFrameBytes = 2 * 1024 * 1024;   // 11/11 noite: o catálogo do modo deus (todos os itens) passou de 256 KB e derrubava a conexão do admin

    public static async Task WriteAsync(Stream stream, byte[] payload, CancellationToken ct)
    {
        if (payload.Length > MaxFrameBytes)
            throw new InvalidDataException($"Quadro de {payload.Length} bytes excede o limite de {MaxFrameBytes}.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>Lê um quadro. Devolve null se a conexão fechou. Lança InvalidDataException se o tamanho for inválido.</summary>
    public static async Task<byte[]?> ReadAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        if (!await ReadExactAsync(stream, header, 4, ct)) return null;
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length < 0 || length > MaxFrameBytes)
            throw new InvalidDataException($"Tamanho de quadro inválido: {length}.");
        var payload = new byte[length];
        if (!await ReadExactAsync(stream, payload, length, ct)) return null;
        return payload;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        var offset = 0;
        while (offset < count)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (n == 0) return false;
            offset += n;
        }
        return true;
    }
}
