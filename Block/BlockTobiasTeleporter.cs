using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

public class BlockTobiasTeleporter : Block
{
    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        var placed = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        if (placed)
        {
            var bett = world.BlockAccessor.GetBlockEntity<BlockEntityTobiasTeleporter>(blockSel.Position);
            var SystemTobiasTeleporter = api.ModLoader.GetModSystem<TobiasTeleporter>();
            if (bett != null && world.Api.Side == EnumAppSide.Server)
            {
                bett.OwnerPlayerUid = byPlayer.PlayerUID;
                bett.OwnerName = byPlayer.PlayerName;
                SystemTobiasTeleporter.AddPlayerLocation(byPlayer.PlayerUID, blockSel.Position);
                bett.MarkDirty();
            }
        }

        return placed;
    }

    public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
    {
        base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);

        var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityTobiasTeleporter;
        if (be == null) return;
        be.OnEntityCollide(entity);
    }

    public static Vec3d GetTeleportOffset(string side)
    {
        var blockFacing = BlockFacing.FromCode(side);
        var blockFacingNormald = blockFacing.Normald * -1;
        var vec3d = new Vec3d(0.5f, 1f, 0.5f).Add(blockFacingNormald);
        return vec3d;
    }
}
