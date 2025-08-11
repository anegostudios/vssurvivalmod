using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// An invisible "fake" block used to fill the space taken by multi-block structures such as the Pulverizer - provides collision boxes and prevents other blocks from being placed here
    /// </summary>
    public class BlockMPMultiblockPulverizer : Block
    {
        public override bool IsReplacableBy(Block block)
        {
            return base.IsReplacableBy(block);
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            IWorldAccessor world = player?.Entity?.World;
            if (world == null) world = api.World;
            BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEMPMultiblock;
            if (be == null || be.Principal == null) return 1f;  //never break
            Block principalBlock = world.BlockAccessor.GetBlock(be.Principal);
            if (api.Side == EnumAppSide.Client)
            {
                //Vintagestory.Client.SystemMouseInWorldInteractions mouse;
                //mouse.loadOrCreateBlockDamage(bs, principalBlock);
                //mouse.curBlockDmg.LastBreakEllapsedMs = game.ElapsedMilliseconds;
            }
            BlockSelection bs = blockSel.Clone();
            bs.Position = be.Principal;
            return principalBlock.OnGettingBroken(player, bs, itemslot, remainingResistance, dt, counter);
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Principal == null)
            {
                // being broken by other game code (including on breaking the pulverizer base block): standard block breaking treatment
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
                return;
            }
            // being broken by player: break the main block instead
            BlockPos principalPos = be.Principal;
            Block principalBlock = world.BlockAccessor.GetBlock(principalPos);
            principalBlock.OnBlockBroken(world, principalPos, byPlayer, dropQuantityMultiplier);

            // Need to trigger neighbourchange on client side only (because it's normally in the player block breaking code)
            if (api.Side == EnumAppSide.Client)
            {
                foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                {
                    BlockPos npos = principalPos.AddCopy(facing);
                    world.BlockAccessor.GetBlock(npos).OnNeighbourBlockChange(world, npos, principalPos);
                }
            }

            // Do not call base.OnBlockBroken if the principal block was broken (and it causes an issue when attempting to spawn particles for the top block)
        }

        public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
        {
            BEMPMultiblock be = blockAccess.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Principal == null)
            {
                return base.GetParticleBreakBox(blockAccess, pos, facing);
            }
            // being broken by player: break the main block instead
            Block principalBlock = blockAccess.GetBlock(be.Principal);
            return principalBlock.GetParticleBreakBox(blockAccess, be.Principal, facing);
        }

        //Need to override because this fake block has no texture of its own (no texture gives black breaking particles)
        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            IBlockAccessor blockAccess = capi.World.BlockAccessor;
            BEMPMultiblock be = blockAccess.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Principal == null)
            {
                return 0;
            }
            Block principalBlock = blockAccess.GetBlock(be.Principal);
            return principalBlock.GetRandomColor(capi, be.Principal, facing, rndIndex);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEMPMultiblock bem = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEMPMultiblock;
            if (bem != null)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(bem.Principal);
                if (be is BEPulverizer bep)
                    return bep.OnInteract(byPlayer, blockSel);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            IBlockAccessor blockAccess = world.BlockAccessor;
            BEMPMultiblock be = blockAccess.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Principal == null)
            {
                return new ItemStack(world.GetBlock(new AssetLocation("pulverizerframe")));   // hard-coded, better than returning an invalid itemstack
            }
            Block principalBlock = blockAccess.GetBlock(be.Principal);
            return principalBlock.OnPickBlock(world, be.Principal);
        }


        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(base.OnPickBlock(world, pos)?.GetName());    // base.OnPickBlock() is important here, to avoid calling the above OnPickBlock() which returns a different block

            foreach (BlockBehavior bh in BlockBehaviors)
            {
                bh.GetPlacedBlockName(sb, world, pos);
            }

            return sb.ToString().TrimEnd();
        }


    }
}
