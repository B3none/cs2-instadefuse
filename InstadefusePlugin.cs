using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
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
    
    // Local
    private float _c4PlantTime = 0.0f;
    private bool _hasBeenDefused = false;
    private bool _willMakeDefuse = false;
    private bool _isActiveGrenade = false;
    private bool _isActiveMolotov = false;
    
    public override void Load(bool hotReload)
    {
        Console.WriteLine("Instadefuse plugin loaded!");
        
        _bombDuration = float.Parse(ConVar.Find("mp_c4timer")?.StringValue ?? string.Empty);
    }
    
    [GameEventHandler]
    public HookResult OnEventRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        Logger.LogInformation("Round has started with Timelimit: {Timelimit}", @event.Timelimit);

        _c4PlantTime = 0.0f;
        _hasBeenDefused = false;
        _willMakeDefuse = false;
        
        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        AttemptInstadefuse();

        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        _c4PlantTime = Server.CurrentTime;

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
        
        if (_isActiveGrenade || _isActiveMolotov)
        {
            Logger.LogInformation("There is an active grenade / molotov somewhere.");
            return;
        }

        CCSPlayerController? defuser = GetDefuser();
        
        if (defuser == null)
        {
            Logger.LogInformation("Defuser not found.");
            return;
        }

        float c4TimeLeft = _bombDuration - (Server.CurrentTime - _c4PlantTime);
        float defuseTime = defuser.PawnHasDefuser ? 5.0f : 10.0f;

        CCSGameRules gameRules = GetGameRules();
        
        if (c4TimeLeft > defuseTime)
        {
            gameRules.TerminateRound(0.0f, RoundEndReason.BombDefused);
        }
    }

    private CCSPlayerController? GetDefuser()
    {
        var players = Utilities.GetPlayers();

        foreach (var player in players)
        {
            if (player.PlayerPawn.Value != null && player.PlayerPawn.Value.IsDefusing)
            {
                return player;
            }
        }
        
        return null;
    }
    
    private static CCSGameRules GetGameRules()
    {
        return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
    }
}