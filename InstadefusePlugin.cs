using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace InstadefusePlugin;

[MinimumApiVersion(129)]
public class InstadefusePlugin : BasePlugin
{
    public override string ModuleName => "Instadefuse Plugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "B3none";
    public override string ModuleDescription => "Allows a CT to instantly defuse the bomb when all Ts are dead and nothing can prevent the defusal.";

    // ConVars
    private float _bombDuration;
    
    private float c4PlantTime = 0.0f;
    private bool hasBeenDefused = false;
    private bool willMakeDefuse = false;
    
    public override void Load(bool hotReload)
    {
        Console.WriteLine("Instadefuse plugin loaded!");
        
        _bombDuration = float.Parse(ConVar.Find("mp_c4timer")?.StringValue ?? string.Empty);
    }
    
    [GameEventHandler]
    public HookResult OnEventRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        Logger.LogInformation("Round has started with Timelimit: {Timelimit}", @event.Timelimit);
        
        hasBeenDefused = false;
        willMakeDefuse = false;
        
        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerBlind @event, GameEventInfo info)
    {
        AttemptInstadefuse();

        return HookResult.Continue;
    }

    private void AttemptInstadefuse()
    {
        Logger.LogInformation("Instadefuse attempted.");
        
        if (_bombDuration == 0.0)
        {
            Logger.LogInformation("Bomb duration ConVar not set.");
            return;
        }

        int defuser = GetDefuser();
        
        if (defuser < 1)
        {
            Logger.LogInformation("Defuser not found.");
            return;
        }
        
        var victim = @event.Userid;
        var attacker = @event.Attacker;
    }

    private int GetDefuser()
    {
        
        
        return 0;
    }
}