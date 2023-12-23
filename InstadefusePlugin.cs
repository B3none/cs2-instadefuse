using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using System.Numerics;
using CounterStrikeSharp.API.Core.Attributes.Registration;

namespace InstadefusePlugin;

[MinimumApiVersion(129)]
public class InstadefusePlugin : BasePlugin
{
    public override string ModuleName => "Instadefuse Plugin";
    public override string ModuleVersion => "1.2.0";
    public override string ModuleAuthor => "B3none";
    public override string ModuleDescription => "Allows a CT to instantly defuse the bomb when all Ts are dead and nothing can prevent the defusal.";

    private static readonly string MessagePrefix = $"[{ChatColors.DarkBlue}Instadefuse{ChatColors.White}] ";
    private static string LogPrefix = $"[{ConsoleColor.Green}Instadefuse{ConsoleColor.White}] ";

    private float _bombPlantedTime = float.NaN;
    private bool _bombTicking = false;
    private int _molotovThreat = 0;
    private int _heThreat = 0;

    private List<int> _infernoThreat = new();

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"{LogPrefix}Plugin loaded!");
        
        // This is commented because it is used for debugging.
        // RegisterEventHandler<EventBombBeep>(OnBombBeep);
    }
    
    // private HookResult OnBombBeep(EventBombBeep @event, GameEventInfo info)
    // {
    //     var plantedBomb = FindPlantedBomb();
    //     if (plantedBomb == null)
    //     {
    //         Console.WriteLine("Planted bomb is null!");
    //         return HookResult.Continue;
    //     }
    //
    //     Server.PrintToChatAll($"{plantedBomb.TimerLength - (Server.CurrentTime - _bombPlantedTime)}");
    //     return HookResult.Continue;
    // }

    [GameEventHandler]
    public HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnGrenadeThrown: {@event.Weapon} - isBot: {@event.Userid?.IsBot}"); 

        if (@event.Weapon == "smokegrenade" || @event.Weapon == "flashbang" || @event.Weapon == "decoy")
        {
            return HookResult.Continue;
        }

        if (@event.Weapon == "hegrenade")
        {
            _heThreat++;
        }

        if (@event.Weapon == "incgrenade" || @event.Weapon == "molotov")
        {
            _molotovThreat++;
        }

        PrintThreatLevel();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnInfernoStartBurn(EventInfernoStartburn @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnInfernoStartBurn");
        
        var infernoPosVector = new Vector3(@event.X, @event.Y, @event.Z);

        var plantedBomb = FindPlantedBomb();
        if (plantedBomb == null)
        {
            return HookResult.Continue;
        }

        var plantedBombVector = plantedBomb.CBodyComponent?.SceneNode?.AbsOrigin ?? null;
        if (plantedBombVector == null)
        {
            return HookResult.Continue;
        }

        var plantedBombVector3 = new Vector3(plantedBombVector.X, plantedBombVector.Y, plantedBombVector.Z);

        var distance = Vector3.Distance(infernoPosVector, plantedBombVector3);

        Console.WriteLine($"Inferno Distance to bomb: {distance}");

        if (distance > 250) 
        {
            return HookResult.Continue;
        }

        _infernoThreat.Add(@event.Entityid);

        PrintThreatLevel();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnInfernoExtinguish(EventInfernoExtinguish @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnInfernoExtinguish");
        
        _infernoThreat.Remove(@event.Entityid);

        PrintThreatLevel();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnInfernoExpire(EventInfernoExpire @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnInfernoExpire");
        
        _infernoThreat.Remove(@event.Entityid);

        PrintThreatLevel();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnHeGrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnHeGrenadeDetonate");
        
        if (_heThreat > 0)
        {
            _heThreat--;
        }

        PrintThreatLevel();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnMolotovDetonate(EventMolotovDetonate @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnMolotovDetonate");
        
        if (_molotovThreat > 0)
        {
            _molotovThreat--;
        }

        PrintThreatLevel();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnRoundStart");
        
        _bombPlantedTime = float.NaN;
        _bombTicking = false;

        _heThreat = 0;
        _molotovThreat = 0;
        _infernoThreat = new List<int>();

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnBombPlanted");
        
        _bombPlantedTime = Server.CurrentTime;
        _bombTicking = true;

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombBeginDefuse(EventBombBegindefuse @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnBombBeginDefuse");

        if (@event.Userid == null)
        {
            return HookResult.Continue;
        }

        if (!@event.Userid.IsValid)
        {
            return HookResult.Continue;
        }

        
        AttemptInstadefuse(@event.Userid);

        return HookResult.Continue;
    }

    private void AttemptInstadefuse(CCSPlayerController player)
    {
        Console.WriteLine($"{LogPrefix}Attempting instadefuse...");

        if (!_bombTicking)
        {
            Console.WriteLine($"{LogPrefix}Bomb is not planted!");
            return;
        }

        PrintThreatLevel();

        if (_heThreat > 0 || _molotovThreat > 0 || _infernoThreat.Any())
        {
            Server.PrintToChatAll($"{MessagePrefix}Instant Defuse not possible because a grenade threat is active!");
            Console.WriteLine($"{LogPrefix}Instant Defuse not possible because a grenade threat is active!");
            return;
        }

        var plantedBomb = FindPlantedBomb();
        if (plantedBomb == null)
        {
            Console.WriteLine($"{LogPrefix}Planted bomb is null!");
            return;
        }

        if (plantedBomb.CannotBeDefused)
        {
            Console.WriteLine($"{LogPrefix}Planted bomb can not be defused!");
            return;
        }

        if (TeamHasAlivePlayers(CsTeam.Terrorist))
        {
            Console.WriteLine($"{LogPrefix}Terrorists are still alive");
            return;
        }

        var bombTimeUntilDetonation = plantedBomb.TimerLength - (Server.CurrentTime - _bombPlantedTime);

        var defuseLength = plantedBomb.DefuseLength;
        if (defuseLength != 5 && defuseLength != 10)
        {
            defuseLength = player.PawnHasDefuser ? 5 : 10;
        }
        Console.WriteLine($"{LogPrefix}DefuseLength: {defuseLength}");

        var bombCanBeDefusedInTime = bombTimeUntilDetonation - defuseLength >= 0.0f;

        if (!bombCanBeDefusedInTime)
        {
            Server.PrintToChatAll($"{MessagePrefix}Defuse started with {ChatColors.Darkred}{bombTimeUntilDetonation.ToString("n3")} seconds{ChatColors.White} left on the bomb. Not enough time left!");
            Console.WriteLine($"{LogPrefix}Defuse started with {bombTimeUntilDetonation.ToString("n3")} seconds left on the bomb. Not enough time left!");
            
            Server.NextFrame(() =>
            {
                plantedBomb.C4Blow = 1.0f;
            });

            return;
        }

        Server.NextFrame(() =>
        {
            plantedBomb.DefuseCountDown = 0;

            Server.PrintToChatAll(
                $"{MessagePrefix}Instant Defuse was successful! Defuse started with {ChatColors.Green}{bombTimeUntilDetonation.ToString("n3")} seconds{ChatColors.White} left on the bomb.");
            Console.WriteLine($"{LogPrefix}Instant Defuse was successful! [{bombTimeUntilDetonation.ToString("n3")}s left]");
        });
    }

    private static bool TeamHasAlivePlayers(CsTeam team)
    {
        var players = Utilities.GetPlayers();

        if (!players.Any())
        {
            Console.WriteLine($"{LogPrefix}No players found!");
            throw new Exception("No players found!");
        }

        return players.Any(player => player.IsValid && player.TeamNum == (byte)team && player.PawnIsAlive);
    }

    private static CPlantedC4? FindPlantedBomb()
    {
        var plantedBombList = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4").ToList();

        if (!plantedBombList.Any())
        {
            Console.WriteLine($"{LogPrefix}No planted bomb entities have been found!");
            return null;
        }

        return plantedBombList.FirstOrDefault();
    }

    private void PrintThreatLevel()
    {
        Console.WriteLine($"{LogPrefix}Threat-Levels: HE [{_heThreat}], Molotov [{_molotovThreat}], Inferno [{_infernoThreat.Count}]");
    }
}