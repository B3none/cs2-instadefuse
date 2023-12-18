using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
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
        _c4PlantTime = 0.0f;
        
        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        AttemptInstadefuse();

        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnBombBegindefuse(EventBombBegindefuse @event, GameEventInfo info)
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
        
        var players = Utilities.GetPlayers();

        CCSPlayerController? defuser = null;
        
        foreach (var player in players)
        {
            if (player is { TeamNum: (byte)CsTeam.Terrorist, PawnIsAlive: true })
            {
                Logger.LogInformation("There is a terrorist alive.");
                return;
            }
            
            if (player.PlayerPawn.Value!.IsDefusing)
            {
                defuser = player;
            }
        }
        
        if (defuser == null)
        {
            Logger.LogInformation("Defuser not found.");
            return;
        }

        float c4TimeLeft = _bombDuration - (Server.CurrentTime - _c4PlantTime);
        float defuseTime = defuser.PawnHasDefuser ? 5.0f : 10.0f;

        CCSGameRules gameRules = GetGameRules();
        
        if (defuseTime > c4TimeLeft)
        {
            gameRules.TerminateRound(0.0f, RoundEndReason.TargetBombed);
        }
        
        if (_isActiveGrenade || _isActiveMolotov)
        {
            Logger.LogInformation("There is an active grenade / molotov somewhere.");
            return;
        }
        
        gameRules.TerminateRound(0.0f, RoundEndReason.BombDefused);
    }
    
    private static CCSGameRules GetGameRules()
    {
        return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
    }
}