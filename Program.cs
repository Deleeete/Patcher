using KLG.Tool.Patcher.PatcherFrontends;
using KLG.Tool.Patcher.PatcherBackends;

if (args.Length < 1)
{
    Console.WriteLine("Usage: patcher CONFIG");
    return;
}

var backend = new BasicPatcherBackend();
var frontend = new BasicPatcherFrontend(backend);
frontend.ExecPatcherScript(args[0]);
string output = args.Length >= 2 ? args[1] : string.Empty;
await frontend.SaveOutput(output);
