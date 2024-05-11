using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace KLG.Tool.Patcher.PatcherFrontends;

internal class BasicPatcherFrontend : IPatcherFrontend
{
    private static readonly Dictionary<string, string> _macros = [];

    private readonly Dictionary<string, Command> _cmds;

    public BasicPatcherFrontend(IPatcherBackend patcher)
    {
        Patcher = patcher;
        _cmds = new()
        {
            { "target", new(1, LoadTarget ) },
            { "output", new(1, args => Output = args[0]) },
            { "arch", new(1, args => Arch = args[0]) },
            { "text-section-base", new(1, args => TextSectionBase = Convert.ToUInt64(args[0], 16)) },
            { "dasm", new(1, LoadDasm) },
            { "patch", new(2, Patch) },
            { "patch-u32", new(2, PatchUInt32) },
            { "patch-u64", new(2, PatchUInt64) },
            { "patch-u32@", new(2, PatchUInt32At) },
            { "kill-func", new(1, KillFunctionCalls) },
        };
    }

    public byte[]? Data { get; private set; }
    public string? Target { get; private set; }
    public string? Arch { get; private set; }
    public string? Output { get; set; }
    public ulong? TextSectionBase { get; private set; }
    public string? Dasm { get; private set; }
    public bool AllowFailure { get; private set; } = false;
    public IPatcherBackend Patcher { get; }

    public void ExecPatcherScript(string file)
    {
        string[] cfgLines = File.ReadAllLines(file);
        for (int lineNum = 0; lineNum < cfgLines.Length; lineNum++)
        {
            string line = cfgLines[lineNum].Trim();
            string[] tokens = line.Split('#', 2);
            line = tokens[0];
            if (string.IsNullOrWhiteSpace(line))
                continue;
            tokens = line.Trim().Split(':', 2);
            if (tokens.Length != 2)
                throw new Exception($"Line[{lineNum + 1}]: Script syntax error. Expecting: COMMAND: ARG_0 ARG_1 ...");
            string cmdStr = tokens[0].Trim();
            string[] args = tokens[1].Trim().Split();
            if (!_cmds.TryGetValue(cmdStr, out Command? cmd))
            {
                Console.WriteLine($"Unknown command \"{cmdStr} ignored.\"");
                continue;
            }
            ReplaceMacros(args);
            try
            {
                cmd.Invoke(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute line {lineNum + 1}: {ex.Message}\n{ex.StackTrace}");
                if (!AllowFailure)
                    Environment.Exit(-1);
            }
        }
    }

    public async Task SaveOutput(string file)
    {
        Needs_Data();
        if (string.IsNullOrWhiteSpace(file))
        {
            CheckSpecified_Output();
            file = Output;
        }
        await File.WriteAllBytesAsync(file, Data);
        Console.WriteLine($"Output saved to \"{file}\"");
    }

    public void LoadTarget(string[] args)
    {
        Target = args[0];
        Data = File.ReadAllBytes(Target);
        Console.WriteLine($"Target file loaded from \"{Target}\"");
    }

    public void LoadDasm(string[] args)
    {
        Dasm = File.ReadAllText(args[0]);
        Console.WriteLine($"Disassembly file loaded from \"{args[0]}\"");
    }

    public void KillFunctionCalls(string[] args)
    {
        Needs_Dasm();
        Needs_Data();
        Needs_TextSectionBase();
        Needs_Arch();
        string funcName = args[0];
        FunctionCall[] calls = FunctionCall.SearchFunctionCalls(Dasm, funcName);
        if (!AllowFailure && calls.Length == 0)
            throw new Exception($"Failed to find call to function \"{funcName}\"");
        Console.WriteLine($"Found {calls.Length} call(s) to function \"{funcName}\"");
        foreach (var call in calls)
        {
            int offset = (int)(call.VirtualAddress - TextSectionBase);
            uint inst = BitConverter.ToUInt32(Data, offset);
            if (inst != call.Instruction)
                throw new Exception($"Unexpected instruction value at offset 0x{offset:X}: Expecting (by disassembly): {call.Instruction:X}  Actual: {inst:X}");
            Console.WriteLine($"  - Killing function call at offset 0x{offset:X}");
            Data.WriteUInt32Data(offset, Instructions.Nop[Arch]);
        }
        Console.WriteLine($"{calls.Length} function call(s) patched\n");
    }

    public void PatchUInt32(string[] args)
    {
        Needs_Data();
        Span<byte> numData = stackalloc byte[sizeof(uint)];
        Span<byte> patchData = stackalloc byte[sizeof(uint)];
        BitConverter.TryWriteBytes(numData, Convert.ToUInt32(args[0], 16));
        BitConverter.TryWriteBytes(patchData, Convert.ToUInt32(args[1], 16));
        Patcher.Patch(Data, numData, patchData);
    }

    public void PatchUInt32At(string[] args)
    {
        Needs_Data();
        int offset = args[0].StartsWith("0x") ? Convert.ToInt32(args[0], 16) : Convert.ToInt32(args[0]);
        Span<byte> patchData = stackalloc byte[sizeof(uint)];
        BitConverter.TryWriteBytes(patchData, Convert.ToUInt32(args[1], 16));
        Patcher.PatchAt(Data, patchData, offset);
    }

    public void PatchUInt64(string[] args)
    {
        Needs_Data();
        Span<byte> numData = stackalloc byte[sizeof(ulong)];
        Span<byte> patchData = stackalloc byte[sizeof(ulong)];
        BitConverter.TryWriteBytes(numData, Convert.ToUInt64(args[0], 16));
        BitConverter.TryWriteBytes(patchData, Convert.ToUInt64(args[1], 16));
        Patcher.Patch(Data, numData, patchData);
    }

    public void Patch(string[] args)
    {
        Needs_Data();
        Span<byte> signatureData = stackalloc byte[args[0].Length / 2];
        Span<byte> patchData = stackalloc byte[args[1].Length / 2];
        args[0].ParseHexString(signatureData);
        args[1].ParseHexString(patchData);
        Patcher.Patch(Data, signatureData, patchData);
    }

    [MemberNotNull(nameof(TextSectionBase))]
    private void Needs_TextSectionBase()
    {
        if (TextSectionBase == null)
            throw new NullReferenceException("Text section base not set");
    }

    [MemberNotNull(nameof(Output))]
    private void CheckSpecified_Output()
    {
        if (Output == null)
            throw new NullReferenceException("Output path not specified");
    }

    [MemberNotNull(nameof(Arch))]
    private void Needs_Arch()
    {
        if (Arch == null)
            throw new NullReferenceException("Architechture not specified");
    }

    [MemberNotNull(nameof(Data))]
    private void Needs_Data()
    {
        if (Data == null)
            throw new NullReferenceException("Source data not loaded");
    }

    [MemberNotNull(nameof(Dasm))]
    private void Needs_Dasm()
    {
        if (Dasm == null)
            throw new NullReferenceException("Disassembly file not loaded");
    }

    private static void ReplaceMacros(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (_macros.TryGetValue(args[i], out string? value))
                args[i] = value;
        }
    }
}

internal record Command(int ArgCount, Action<string[]> Action)
{
    public void Invoke(string[] args)
    {
        if (args.Length != -1 && args.Length != ArgCount)
            throw new ArgumentException($"Invalid argument count {args.Length}. Expecting: {ArgCount}");
        Action(args);
    }
}

internal record struct FunctionCall(ulong VirtualAddress, ulong Instruction)
{
    private static readonly Dictionary<string, Regex> _callPatterns = [];

    public static FunctionCall[] SearchFunctionCalls(string dasm, string funcName)
    {
        Regex regex = BuildOrGetRegex(funcName);
        var matches = regex.Matches(dasm);
        return matches.Select(Parse).ToArray();
    }

    private static Regex BuildOrGetRegex(string funcName)
    {
        if (!_callPatterns.TryGetValue(funcName, out Regex? regex))
        {
            string regexPattern = @"\s*([0-9a-f]+):\s+([0-9a-f]{8})\s+bl\s+[0-9a-f]+\s+<" + funcName + ">";
            regex = new(regexPattern, RegexOptions.Compiled);
            _callPatterns.Add(funcName, regex);
        }
        return regex;
    }

    private static FunctionCall Parse(Match match)
    {
        ulong va = Convert.ToUInt64(match.Groups[1].Value, 16);
        ulong inst = Convert.ToUInt64(match.Groups[2].Value, 16);
        return new FunctionCall(va, inst);
    }
}
