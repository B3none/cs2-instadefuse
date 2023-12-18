using CounterStrikeSharp.API.Core;

namespace InstadefusePlugin;

public class InstadefusePlugin : BasePlugin
{
    public override string ModuleName => "Instadefuse Plugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "B3none";

    public override void Load(bool hotReload)
    {
        Console.WriteLine("Instadefuse plugin loaded!");
    }
}