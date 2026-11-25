using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// An invisible "fake" block used to fill the space taken by multi-block machines - provides collision boxes and prevents other blocks from being placed here
    /// </summary>
    public class BlockMPMultiblockBase : Block
    {
        public override float OnGettingBroken(IPlayer? player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            var principalPos = api.World.BlockAccessor.GetBlockEntity<BEMPMultiblock>(blockSel.Position)?.Principal;
            if (principalPos == null) return 1f;  //never break

            BlockSelection bs = blockSel.Clone();
            bs.Position = principalPos;
            return api.World.BlockAccessor.GetBlock(principalPos).OnGettingBroken(player, bs, itemslot, remainingResistance, dt, counter);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, float dropQuantityMultiplier = 1f)
        {
            var principalPos = world.BlockAccessor.GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            if (principalPos != null && world.BlockAccessor.GetBlock(principalPos).Id != 0)
            {
                // being broken by player: break the main block instead if we can find one
                world.BlockAccessor.BreakBlock(principalPos, byPlayer, dropQuantityMultiplier);
                return;
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override T? GetInterface<T>(IWorldAccessor world, BlockPos? pos) where T: class
        {
            if (pos == null) return base.GetInterface<T>(world, pos);
            var principalPos = GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            if (principalPos == null) return base.GetInterface<T>(world, pos);
            return world.BlockAccessor.GetBlock(principalPos).GetInterface<T>(world, principalPos) ?? base.GetInterface<T>(world, pos);
        }

        public override T? GetInterface<T>(IWorldAccessor world, FastBlockPos pos) where T: class
        {
            var principalPos = GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            if (principalPos == null) return base.GetInterface<T>(world, pos);
            return world.BlockAccessor.GetBlock(principalPos).GetInterface<T>(world, principalPos) ?? base.GetInterface<T>(world, pos);
        }

        public override ItemStack? OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockPos? principalPos = world.BlockAccessor.GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            // The principal is gone, so fall back to the block this filler is made for.
            if (principalPos != null) return world.BlockAccessor.GetBlock(principalPos).OnPickBlock(world, principalPos);
            Block? fallbackBlock = world.GetBlock(Attributes?["principalBlockCode"].AsObject<AssetLocation>(null, Code.Domain));
            return fallbackBlock == null ? null : new ItemStack(fallbackBlock);
        }

        public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
        {
            var principalPos = blockAccess.GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            if (principalPos == null) return base.GetParticleBreakBox(blockAccess, pos, facing);
            // being broken by player: break the main block instead
            return blockAccess.GetBlock(principalPos).GetParticleBreakBox(blockAccess, principalPos, facing);
        }

        //Need to override because this fake block has no texture of its own (no texture gives black breaking particles)
        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            var blockAccess = capi.World.BlockAccessor;
            var principalPos = blockAccess.GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            return principalPos == null ? 0 : blockAccess.GetBlock(principalPos).GetRandomColor(capi, principalPos, facing, rndIndex);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(base.OnPickBlock(world, pos)?.GetName()); // base.OnPickBlock() is important here, to avoid calling the above OnPickBlock() which returns a different block

            foreach (BlockBehavior bh in BlockBehaviors)
            {
                bh.GetPlacedBlockName(sb, world, pos);
            }

            return sb.ToString().TrimEnd();
        }
    }
}
