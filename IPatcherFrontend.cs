namespace KLG.Tool.Patcher;

internal interface IPatcherFrontend
{
    void ExecPatcherScript(string file);
    Task SaveOutput(string file);
}
