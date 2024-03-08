namespace KLG.Tool.Patcher.PatcherBackends;

internal class BasicPatcherBackend : IPatcherBackend
{
    public void Patch(Span<byte> data, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> patch, int startIndex = 0, int searchLength = -1)
    {
        searchLength = searchLength == -1 ? data.Length : searchLength;
        int searchEnd = searchLength - signature.Length;
        for (int dataIndex = startIndex; dataIndex < searchEnd; dataIndex++)
        {
            for (int signatureOffset = 0; signatureOffset < signature.Length; signatureOffset++)
            {
                if (data[dataIndex + signatureOffset] != signature[signatureOffset])
                    goto next_src_index;
            }
            Console.WriteLine($"Found @ 0x{dataIndex:X8}");
            Console.WriteLine($"  Signature:   {signature.ToHexString()}");
            Console.WriteLine($"  Patching to: {patch.ToHexString()}");
            patch.CopyTo(data[dataIndex..]);
            Console.WriteLine($"{signature.Length} bytes patched\n");
            return;
        next_src_index:
            continue;
        }
        throw new Exception($"\x1b[31mSignature {signature.ToHexString()} NOT FOUND\x1b[0m");
    }
    public void PatchAt(Span<byte> data, ReadOnlySpan<byte> patch, int offset)
        => patch.CopyTo(data[offset..]);
}
