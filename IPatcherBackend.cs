namespace KLG.Tool.Patcher;

internal interface IPatcherBackend
{
    void Patch(Span<byte> data, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> patch, int startIndex = 0, int searchLength = -1);
    void PatchAt(Span<byte> data, ReadOnlySpan<byte> patch, int offset);
}
