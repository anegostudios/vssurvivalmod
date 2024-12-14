using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public class ModsystemElevator : ModSystem
{
    private ICoreServerAPI sapi;

    public Dictionary<string, ElevatorSystem> Networks = new Dictionary<string, ElevatorSystem>();

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        var parser = sapi.ChatCommands.Parsers;

        sapi.ChatCommands.GetOrCreate("dev")
            .BeginSubCommand("elevator")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand("set-entity-net")
            .WithAlias("sen")
            .WithDescription("Set Elevator network code")
            .WithArgs(parser.Entities("entity"), parser.Word("network code"))
            .HandleWith(OnEntityNetworkSet)
            .EndSubCommand()
            .BeginSubCommand("set-block-net")
            .WithAlias("sbn")
            .WithDescription("Set Elevator network code")
            .WithArgs(parser.Word("netowrk code"), parser.WorldPosition("pos"), parser.OptionalInt("offset"))
            .HandleWith(OnsetBlockNetwork)
            .EndSubCommand()
            .EndSubCommand();
    }

    private TextCommandResult OnsetBlockNetwork(TextCommandCallingArgs args)
    {
        var networkCode = args[0] as string;
        var pos = args.Parsers[1].GetValue() as Vec3d;
        var offset = args.Parsers[2].IsMissing ? -1 : (int)args[2];
        var block = sapi.World.BlockAccessor.GetBlock(pos.AsBlockPos);
        var beBehavior = block.GetBEBehavior<BEBehaviorElevatorControl>(pos.AsBlockPos);

        if (beBehavior == null) return TextCommandResult.Success("Target was not a ElevatorControl block");

        beBehavior.NetworkCode = networkCode;
        beBehavior.Offset = offset;
        var elevatorModSystem = sapi.ModLoader.GetModSystem<ModsystemElevator>();
        elevatorModSystem.RegisterControl(networkCode, pos.AsBlockPos, offset);
        return TextCommandResult.Success($"Network code set to {networkCode}");
    }

    private TextCommandResult OnEntityNetworkSet(TextCommandCallingArgs args) =>
        CmdUtil.EntityEach(args, e =>
        {
            var networkCode = args[1] as string;

            if (e is not EntityElevator elevator) return TextCommandResult.Success("Target was not a elevator");

            elevator.NetworkCode = networkCode;

            var elevatorModSystem = sapi.ModLoader.GetModSystem<ModsystemElevator>();
            var sys = elevatorModSystem.RegisterElevator(networkCode, elevator);
            elevator.ElevatorSys = sys;

            return TextCommandResult.Success($"Network code set to {networkCode}");
        });

    public void EnsureNetworkExists(string networkCode)
    {
        if (!string.IsNullOrEmpty(networkCode))
        {
            if (!Networks.ContainsKey(networkCode))
            {
                Networks.TryAdd(networkCode, new ElevatorSystem());
            }
        }
    }

    public ElevatorSystem GetElevator(string networkCode)
    {
        EnsureNetworkExists(networkCode);
        return Networks.GetValueOrDefault(networkCode);
    }

    public ElevatorSystem RegisterElevator(string networkCode, EntityElevator elevator)
    {
        if (Networks.TryGetValue(networkCode, out var network))
        {
            network.Entity = elevator;
            return network;
        }

        Networks.TryAdd(networkCode, new ElevatorSystem() { Entity = elevator });
        return Networks[networkCode];
    }

    public void CallElevator(string networkCode, BlockPos position, int offset)
    {
        var entityElevator = GetElevator(networkCode);
        entityElevator?.Entity.CallElevator(position, offset);
    }

    public void RegisterControl(string networkCode, BlockPos pos, int offset)
    {
        var entityElevator = GetElevator(networkCode);
        if (entityElevator?.ControlPositions.Contains(pos.Y + offset) == false)
        {
            entityElevator.ControlPositions.Add(pos.Y + offset);
            entityElevator.ControlPositions.Sort();
        }
    }

    public void ActivateElevator(string networkCode, BlockPos position, int offset)
    {
        var entityElevator = GetElevator(networkCode);
        entityElevator.Entity.ActivateElevator(position, offset);
    }

    public void DeActivateElevator(string networkCode)
    {
        var entityElevator = GetElevator(networkCode);
        entityElevator.Entity.DeActivateElevator();
    }
}
