using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// An invisible "fake" block used to fill the space taken by multi-block structures such as the LargeGear3 - provides collision boxes and prevents other blocks from being placed here
    /// </summary>
    public class BlockMPMultiblockGear : Block
    {
        public override bool IsReplacableBy(Block block)
        {
            if (block is BlockAngledGears) return true;
            return base.IsReplacableBy(block);
        }

        public bool IsReplacableByGear(IWorldAccessor world, BlockPos pos)
        {
            BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Principal == null) return true;
            IGearAcceptor beg = world.BlockAccessor.GetBlockEntity(be.Principal) as IGearAcceptor;
            return beg == null ? true : beg.CanAcceptGear(pos);
        }

        public BlockEntity GearPlaced(IWorldAccessor world, BlockPos pos)
        {
            BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Principal == null)
            {
                return null;
            }

            IGearAcceptor beg = world.BlockAccessor.GetBlockEntity(be.Principal) as IGearAcceptor;
            if (beg == null) world.Logger.Notification("no gear acceptor");
            beg?.AddGear(pos);
            return beg as BlockEntity;
        }

        public static void OnGearDestroyed(IWorldAccessor world, BlockPos pos, char orient)
        {
            BlockPos posCenter;
            switch (orient)
            {
                case 's':
                    posCenter = pos.NorthCopy();
                    break;
                case 'w':
                    posCenter = pos.EastCopy();
                    break;
                case 'e':
                    posCenter = pos.WestCopy();
                    break;
                case 'n':
                default:
                    posCenter = pos.SouthCopy();
                    break;
            }
            if (world.BlockAccessor.GetBlockEntity(posCenter) is IGearAcceptor beg)
            {
                beg.RemoveGearAt(pos);
                Block toPlaceBlock = world.GetBlock(new AssetLocation("mpmultiblockwood"));
                world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, pos);
                if (world.BlockAccessor.GetBlockEntity(pos) is BEMPMultiblock be) be.Principal = posCenter;
            }
            else world.Logger.Notification("no LG found at " + posCenter + " from " + pos);
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            IWorldAccessor world = player?.Entity?.World;
            if (world == null) world = api.World;
            BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEMPMultiblock;
            if (be == null || be.Principal == null) return 1f;  //never break
            Block centerBlock = world.BlockAccessor.GetBlock(be.Principal);
            
            BlockSelection bs = blockSel.Clone();
            bs.Position = be.Principal;
            return centerBlock.OnGettingBroken(player, bs, itemslot, remainingResistance, dt, counter);
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
                // being broken by other game code (including on breaking the center large gear): standard block breaking treatment
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
                return;
            }
            // being broken by player: break the center block instead
            BlockPos centerPos = be.Principal;
            Block centerBlock = world.BlockAccessor.GetBlock(centerPos);
            if (centerBlock.Id != 0)
            {
                centerBlock.OnBlockBroken(world, centerPos, byPlayer, dropQuantityMultiplier);
            }
            else
            {
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            }

            // Need to trigger neighbourchange on client side only (because it's normally in the player block breaking code)
            if (api.Side == EnumAppSide.Client)
            {
                foreach (BlockFacing facing in BlockFacing.VERTICALS)
                {
                    BlockPos npos = centerPos.AddCopy(facing);
                    world.BlockAccessor.GetBlock(npos).OnNeighbourBlockChange(world, npos, centerPos);
                }
            }
        }

        public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
        {
            BEMPMultiblock be = blockAccess.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Principal == null)
            {
                return base.GetParticleBreakBox(blockAccess, pos, facing);
            }
            // being broken by player: break the center block instead
            Block centerBlock = blockAccess.GetBlock(be.Principal);
            return centerBlock.GetParticleBreakBox(blockAccess, be.Principal, facing);
        }

        // Need to override because this fake block has no texture of its own (no texture gives black breaking particles)
        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            IBlockAccessor blockAccess = capi.World.BlockAccessor;
            BEMPMultiblock be = blockAccess.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Principal == null)
            {
                return 0;
            }
            Block centerBlock = blockAccess.GetBlock(be.Principal);
            return centerBlock.GetRandomColor(capi, be.Principal, facing, rndIndex);
        }


    }
}
