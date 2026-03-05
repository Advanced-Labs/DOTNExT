namespace Scynapse.Security.Crypto;

/// <summary>
/// CRC-16/CCITT-FALSE implementation for key encoding checksums.
/// Same polynomial used by NATS NKeys for key integrity verification.
/// </summary>
internal static class Crc16
{
    private const ushort Polynomial = 0x1021;
    private static readonly ushort[] Table = GenerateTable();

    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (byte b in data)
        {
            crc = (ushort)((crc << 8) ^ Table[((crc >> 8) ^ b) & 0xFF]);
        }
        return crc;
    }

    private static ushort[] GenerateTable()
    {
        var table = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            ushort crc = (ushort)(i << 8);
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ Polynomial)
                    : (ushort)(crc << 1);
            }
            table[i] = crc;
        }
        return table;
    }
}
