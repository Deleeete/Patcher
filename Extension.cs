using System.Text;

namespace KLG.Tool.Patcher;

internal static class Extension
{
    internal static string ToHexString(this ReadOnlySpan<byte> bin)
    {
        StringBuilder sb = new(bin.Length * 3 + 2);
        sb.Append('[');
        foreach (var b in bin)
            sb.AppendFormat("{0:X2}|", b);
        if (sb[^1] == '|')
            sb.Length--;
        sb.Append(']');
        return sb.ToString();
    }

    internal static string ToHexString(this Span<byte> bin)
    {
        StringBuilder sb = new(bin.Length * 3 + 2);
        sb.Append('[');
        foreach (var b in bin)
            sb.AppendFormat("{0:X2}|", b);
        if (sb[^1] == '|')
            sb.Length--;
        sb.Append(']');
        return sb.ToString();
    }

    internal static void ParseHexString(this string hexStr, Span<byte> buffer, int offset = 0)
    {
        Span<char> hexStrFiltered = stackalloc char[hexStr.Length];
        int writeCursor = 0;
        for (int readCursor = 0; readCursor < hexStr.Length; readCursor++)
        {
            if (hexStr[readCursor] == '[' | hexStr[readCursor] == ']')
                continue;
            hexStrFiltered[writeCursor] = hexStr[readCursor];
            writeCursor++;
        }
        hexStrFiltered = hexStrFiltered[..writeCursor];
        if (hexStrFiltered.Length % 2 != 0)
            throw new ArgumentException("Invalid hex string: String length is not an even number");
        if (buffer.Length != hexStrFiltered.Length / 2)
            throw new ArgumentException("Invalid buffer: Buffer length should be 2x string length");
        for (int i = offset; i < buffer.Length; i++)
        {
            int hexStrOffset = i * 2;
            int h = hexStrFiltered[hexStrOffset] switch
            {
                '0' or '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9' => hexStrFiltered[hexStrOffset] - '0',
                'A' or 'B' or 'C' or 'D' or 'E' or 'F' => hexStrFiltered[hexStrOffset] - 'A' + 0xA,
                'a' or 'b' or 'c' or 'd' or 'e' or 'f' => hexStrFiltered[hexStrOffset] - 'a' + 0xA,
                _ => throw new ArgumentException("Not a hex char"),
            };
            int l = hexStrFiltered[hexStrOffset + 1] switch
            {
                '0' or '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9' => hexStrFiltered[hexStrOffset + 1] - '0',
                'A' or 'B' or 'C' or 'D' or 'E' or 'F' => hexStrFiltered[hexStrOffset + 1] - 'A' + 0xA,
                'a' or 'b' or 'c' or 'd' or 'e' or 'f' => hexStrFiltered[hexStrOffset + 1] - 'a' + 0xA,
                _ => throw new ArgumentException("Not a hex char"),
            };
            buffer[i] = (byte)((h << 4) | l);
        }
    }

    internal static void WriteUInt32Data(this byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
