using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public class BEBehaviorElevatorControl: BlockEntityBehavior
{
    private ModsystemElevator elevatorModSystem;
    public string NetworkCode { get; set; }

    public int Offset { get; set; } = -1;

    public bool EnableElevator;

    public BEBehaviorElevatorControl(BlockEntity blockentity) : base(blockentity)
    {
    }

    public void OnInteract(BlockPos position, bool deactivate = false)
    {
        if(NetworkCode == null) return;
        if (EnableElevator)
        {
            if (deactivate)
            {
                elevatorModSystem.DeActivateElevator(NetworkCode);
            }
            else
            {
                elevatorModSystem.ActivateElevator(NetworkCode, position, Offset);
            }
        }
        else
        {
            elevatorModSystem.CallElevator(NetworkCode, position, Offset);
        }
        Api.World.PlaySoundAt("sounds/effect/latch.ogg", Pos, 0);
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        EnableElevator = properties["canEnableElevator"].AsBool();

        if (api.Side == EnumAppSide.Server)
        {
            elevatorModSystem = Api.ModLoader.GetModSystem<ModsystemElevator>();

            if(string.IsNullOrEmpty(NetworkCode)) return;
            elevatorModSystem.RegisterControl(NetworkCode, Pos, Offset);
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        NetworkCode = tree.GetString("networkCode");
        Offset = tree.GetInt("offset", -1);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetString("networkCode", NetworkCode );
        tree.SetInt("offset", Offset);
    }

    public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid, Block layerBlock, bool resolveImports)
    {
        base.OnPlacementBySchematic(api, blockAccessor, pos, replaceBlocks, centerrockblockid, layerBlock, resolveImports);
        if (api.Side == EnumAppSide.Server)
        {
            if(string.IsNullOrEmpty(NetworkCode)) return;
            elevatorModSystem.RegisterControl(NetworkCode, Pos, Offset);
        }
    }
}
