using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// An invisible "fake" block used to fill the space taken by multi-block structures such as the LargeGear3 - provides collision boxes and prevents other blocks from being placed here
    /// </summary>
    public class BlockMPMultiblockWood : Block
    {
        public override bool IsReplacableBy(Block block)
        {
            if (block is BlockAngledGears) return true;
            return base.IsReplacableBy(block);
        }

        internal bool IsReplacableByGear(IWorldAccessor world, BlockPos pos)
        {
            BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Centre == null) return true;
            IGearAcceptor beg = world.BlockAccessor.GetBlockEntity(be.Centre) as IGearAcceptor;
            return beg == null ? true : beg.CanAcceptGear(pos);
        }

        internal void GearPlaced(IWorldAccessor world, BlockPos pos)
        {
            BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Centre == null)
            {
                return;
            }

            IGearAcceptor beg = world.BlockAccessor.GetBlockEntity(be.Centre) as IGearAcceptor;
            if (beg == null) world.Logger.Notification("no gear acceptor");
            beg?.AddGear(pos);
        }

        public static void OnGearDestroyed(IWorldAccessor world, BlockPos pos, char orient)
        {
            BlockPos posCentre;
            switch (orient)
            {
                case 's':
                    posCentre = pos.SouthCopy();
                    break;
                case 'w':
                    posCentre = pos.WestCopy();
                    break;
                case 'e':
                    posCentre = pos.EastCopy();
                    break;
                case 'n':
                default:
                    posCentre = pos.NorthCopy();
                    break;
            }
            IGearAcceptor beg = world.BlockAccessor.GetBlockEntity(posCentre) as IGearAcceptor;
            if (beg != null)
            {
                beg.RemoveGearAt(pos);
                Block toPlaceBlock = world.GetBlock(new AssetLocation("mpmultiblockwood"));
                world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, pos);
                BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(pos) as BEMPMultiblock;
                if (be != null) be.Centre = posCentre;
            }
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            IWorldAccessor world = player?.Entity?.World;
            if (world == null) world = api.World;
            BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEMPMultiblock;
            if (be == null || be.Centre == null) return 1f;  //never break
            Block centreBlock = world.BlockAccessor.GetBlock(be.Centre);
            if (api.Side == EnumAppSide.Client)
            {
                //Vintagestory.Client.SystemMouseInWorldInteractions mouse;
                //mouse.loadOrCreateBlockDamage(bs, centreBlock);
                //mouse.curBlockDmg.LastBreakEllapsedMs = game.ElapsedMilliseconds;
            }
            BlockSelection bs = blockSel.Clone();
            bs.Position = be.Centre;
            return centreBlock.OnGettingBroken(player, bs, itemslot, remainingResistance, dt, counter);
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Centre == null)
            {
                // being broken by other game code (including on breaking the centre large gear): standard block breaking treatment
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
                return;
            }
            // being broken by player: break the centre block instead
            Block centreBlock = world.BlockAccessor.GetBlock(be.Centre);
            centreBlock.OnBlockBroken(world, be.Centre, byPlayer, dropQuantityMultiplier);
        }

        public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
        {
            BEMPMultiblock be = blockAccess.GetBlockEntity(pos) as BEMPMultiblock;
            if (be == null || be.Centre == null)
            {
                return base.GetParticleBreakBox(blockAccess, pos, facing);
            }
            // being broken by player: break the centre block instead
            Block centreBlock = blockAccess.GetBlock(be.Centre);
            return centreBlock.GetParticleBreakBox(blockAccess, be.Centre, facing);
        }

    }
}
