namespace KLG.Tool.Patcher;

internal static class Instructions
{
    public static readonly Dictionary<string, uint> Nop = new()
    {
        { "aarch64", 0xd503201f }
    };
}
