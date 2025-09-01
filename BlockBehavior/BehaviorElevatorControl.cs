using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

/// <summary>
/// Used in combination with the "BEBehaviorElevatorControl" to create an elevator.
/// Uses the code "ElevatorControl", and has no properties.
/// </summary>
/// <example><code lang="json">
///"behaviors": [
///	{ "name": "ElevatorControl" }
///],
/// </code></example>
[DocumentAsJson]
public class BlockBehaviorElevatorControl : BlockBehavior
{
    private ICoreAPI api;
    private SimpleParticleProperties lorehintParticleProps;

    public BlockBehaviorElevatorControl(Block block) : base(block)
    {
    }

    public override void OnLoaded(ICoreAPI api)
    {
        if (api.Side == EnumAppSide.Client)
        {
            this.api = api;
            lorehintParticleProps = GuiStyle.LoreHintParticles.Clone(api.World);
        }
    }

    public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;
        return true;
    }

    public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
    {
        if (api.World.Rand.NextDouble() < 0.05)
        {
            lorehintParticleProps.MinPos = pos.ToVec3d().AddCopy(0.5, 0.1f, 0.5);
            manager.Spawn(lorehintParticleProps);
        }
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventSubsequent;
        if (byPlayer.Entity.Api.Side == EnumAppSide.Server)
        {
            var beBehaviorElevatorControl = block.GetBEBehavior<BEBehaviorElevatorControl>(blockSel.Position);
            if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative && byPlayer.Entity.Controls.Sneak)
            {
                beBehaviorElevatorControl.OnInteract(blockSel.Position, true);
            }
            else
            {
                beBehaviorElevatorControl.OnInteract(blockSel.Position);
            }
        }

        return true;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer,
        ref EnumHandling handling)
    {
        var isCreative = forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
        var interactions = new List<WorldInteraction>();
        if (isCreative)
        {
            interactions.Add(new WorldInteraction()
            {
                ActionLangCode = "elevator-reset",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "sneak"
            });
        }

        interactions.Add(new WorldInteraction()
        {
            ActionLangCode = "elevator-call",
            MouseButton = EnumMouseButton.Right
        });
        return interactions.ToArray();
    }
}
