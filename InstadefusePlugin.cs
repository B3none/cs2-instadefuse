using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using System.Numerics;
using CounterStrikeSharp.API.Core.Attributes.Registration;

namespace InstadefusePlugin;

[MinimumApiVersion(147)]
public class InstadefusePlugin : BasePlugin
{
    private const string Version = "1.3.3";
    
    public override string ModuleName => "Instadefuse Plugin";
    public override string ModuleVersion => Version;
    public override string ModuleAuthor => "B3none";
    public override string ModuleDescription => "Allows a CT to instantly defuse the bomb when nothing can prevent defusal.";

    private static readonly string LogPrefix = $"[Instadefuse {Version}] ";
    private static readonly string MessagePrefix = $"[{ChatColors.Green}Retakes{ChatColors.White}] ";

    private float _bombPlantedTime = float.NaN;
    private bool _bombTicking;
    private int _molotovThreat;
    private int _heThreat;

    private List<int> _infernoThreat = new();

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"{LogPrefix}Plugin loaded!");
    }

    [GameEventHandler]
    public HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        Console.WriteLine($"{LogPrefix}OnGrenadeThrown: {@event.Weapon} - isBot: {@event.Userid?.IsBot}");

        var weapon = @event.Weapon;

        if (weapon == "hegrenade")
        {
            _heThreat++;
        }
        else if (weapon == "incgrenade" || @event.Weapon == "molotov")
        {
            _molotovThreat++;
        }
        else
        {
            return HookResult.Continue;
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
        
        var player = @event.Userid;

        if (player == null || !player.IsValid)
        {
            return HookResult.Continue;
        }
        
        AttemptInstadefuse(player);

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
        
        PrintThreatLevel();

        if (_heThreat > 0 || _molotovThreat > 0 || _infernoThreat.Any())
        {
            Console.WriteLine($"{LogPrefix}Instant Defuse not possible because a grenade threat is active!");
            Server.PrintToChatAll($"{MessagePrefix}Instant Defuse not possible because a grenade threat is active!");
            return;
        }

        var bombTimeUntilDetonation = plantedBomb.TimerLength - (Server.CurrentTime - _bombPlantedTime);

        var defuseLength = plantedBomb.DefuseLength;
        if (defuseLength != 5 && defuseLength != 10)
        {
            defuseLength = player.PawnHasDefuser ? 5.0f : 10.0f;
        }
        Console.WriteLine($"{LogPrefix}DefuseLength: {defuseLength}");

        var timeLeftAfterDefuse = bombTimeUntilDetonation - defuseLength;
        var bombCanBeDefusedInTime = timeLeftAfterDefuse >= 0.0f;

        if (!bombCanBeDefusedInTime)
        {
            var outputText = $"{player.PlayerName} was {ChatColors.DarkRed}{Math.Abs(timeLeftAfterDefuse):n3} seconds{ChatColors.White} away from defusing.";
            Console.WriteLine($"{LogPrefix}{outputText}");
            Server.PrintToChatAll($"{MessagePrefix}{outputText}");

            Server.NextFrame(() =>
            {
                plantedBomb.C4Blow = 1.0f;
            });

            return;
        }

        Server.NextFrame(() =>
        {
            plantedBomb.DefuseCountDown = 0;

            var outputText = $"{player.PlayerName} defused with {ChatColors.Green}{bombTimeUntilDetonation:n3} seconds{ChatColors.White} left on the bomb.";
            Console.WriteLine($"{LogPrefix}{outputText}");
            Server.PrintToChatAll($"{MessagePrefix}{outputText}");
        });
    }

    private static bool TeamHasAlivePlayers(CsTeam team)
    {
        var players = Utilities.GetPlayers();

        if (players.Any())
        {
            return players.Any(player => player.IsValid && player.Team == team && player.PawnIsAlive);
        }

        Console.WriteLine($"{LogPrefix}No players found!");
        throw new Exception("No players found!");
    }

    private static CPlantedC4? FindPlantedBomb()
    {
        var plantedBombList = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4").ToList();

        if (plantedBombList.Any())
        {
            return plantedBombList.FirstOrDefault();
        }
        
        Console.WriteLine($"{LogPrefix}No planted bomb entities have been found!");
        return null;
    }

    private void PrintThreatLevel()
    {
        Console.WriteLine($"{LogPrefix}Threat-Levels: HE [{_heThreat}], Molotov [{_molotovThreat}], Inferno [{_infernoThreat.Count}]");
    }
}
