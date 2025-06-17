using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockSticksLayer : Block
    {
        public BlockFacing Orientation { get; set; }
        

        static BlockSticksLayer()
        {

        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.Orientation = BlockFacing.FromFirstLetter(Variant["facing"][0]);
        }

        protected AssetLocation OrientedAsset(string orientation)
        {
            return CodeWithVariants(new string[] { "type", "facing" }, new string[] { "wooden", orientation });
        }

        //public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        //{
        //    return (base.ShouldReceiveServerGameTicks(world, pos, offThreadRandom, out extra));
        //    //TODO: if extra is melt, add a lot more drips!
        //}

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                BlockFacing horVer = OrientForPlacement(world.BlockAccessor, byPlayer, blockSel);

                string orientation = (horVer == BlockFacing.NORTH || horVer == BlockFacing.SOUTH) ? "ns" : "ew";
                AssetLocation newCode = OrientedAsset(orientation);

                world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(newCode).BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        protected virtual BlockFacing OrientForPlacement(IBlockAccessor world, IPlayer player, BlockSelection bs)
        {
            BlockFacing[] facings = SuggestedHVOrientation(player, bs);
            BlockFacing suggested = facings.Length > 0 ? facings[0] : null;
            if (suggested != null && player.Entity.Controls.ShiftKey) return suggested;

            BlockPos pos = bs.Position;

            // Logic waterfall for smart placement:
            // 1. if adjacent Sticks Layer horizontally, snap to it (if there are two orthogonally, no decision; if three, take the majority)
            // 2. If air (or similar) below and the block below has any supporting blocks horizontally, orient to span the gap
            // 3. Respect SuggestedHV

            // 1
            Block westBlock = world.GetBlock(pos.WestCopy());
            Block eastBlock = world.GetBlock(pos.EastCopy());
            Block northBlock = world.GetBlock(pos.NorthCopy());
            Block southBlock = world.GetBlock(pos.SouthCopy());
            int westConnect = (westBlock is BlockSticksLayer wb) && wb.Orientation == BlockFacing.EAST ? 1 : 0;
            int eastConnect = (eastBlock is BlockSticksLayer eb) && eb.Orientation == BlockFacing.EAST ? 1 : 0;
            int northConnect = (northBlock is BlockSticksLayer nb) && nb.Orientation == BlockFacing.NORTH ? 1 : 0;
            int southConnect = (southBlock is BlockSticksLayer sb) && sb.Orientation == BlockFacing.NORTH ? 1 : 0;

            if (westConnect + eastConnect - northConnect - southConnect > 0) return BlockFacing.EAST;
            if (northConnect + southConnect - westConnect - eastConnect > 0) return BlockFacing.NORTH;

            // 2
            BlockPos down = pos.DownCopy();
            if (!CanSupportThis(world, down, null))
            {
                int westSolid = CanSupportThis(world, down.WestCopy(), BlockFacing.EAST) ? 1 : 0;
                int eastSolid = CanSupportThis(world, down.EastCopy(), BlockFacing.WEST) ? 1 : 0;
                int northSolid = CanSupportThis(world, down.NorthCopy(), BlockFacing.SOUTH) ? 1 : 0;
                int southSolid = CanSupportThis(world, down.SouthCopy(), BlockFacing.NORTH) ? 1 : 0;
                if (westSolid + eastSolid == 2 && northSolid + southSolid < 2) return BlockFacing.EAST;
                if (westSolid + eastSolid < 2 && northSolid + southSolid == 2) return BlockFacing.NORTH;
            }

            return suggested;
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection bs, ref string failureCode)
        {
            BlockPos pos = bs.Position;
            BlockPos down = pos.DownCopy();
            IBlockAccessor blockAccess = world.BlockAccessor;

            //TODO: in future wattle walls directly below can also support this
            if (!CanSupportThis(blockAccess, down, null))
            {
                // Can always place against other Layers of sticks
                bool oneSolid = blockAccess.GetBlock(pos.WestCopy()) is BlockSticksLayer;
                if (!oneSolid) oneSolid = blockAccess.GetBlock(pos.EastCopy()) is BlockSticksLayer;
                if (!oneSolid) oneSolid = blockAccess.GetBlock(pos.NorthCopy()) is BlockSticksLayer;
                if (!oneSolid) oneSolid = blockAccess.GetBlock(pos.SouthCopy()) is BlockSticksLayer;

                // Can place if any of the 4 surrounding supporting blocks has a solid top face or a shape which supports this
                if (!oneSolid) oneSolid = CanSupportThis(blockAccess, down.WestCopy(), BlockFacing.EAST);
                if (!oneSolid) oneSolid = CanSupportThis(blockAccess, down.EastCopy(), BlockFacing.WEST);
                if (!oneSolid) oneSolid = CanSupportThis(blockAccess, down.NorthCopy(), BlockFacing.SOUTH);
                if (!oneSolid) oneSolid = CanSupportThis(blockAccess, down.SouthCopy(), BlockFacing.NORTH);
                if (!oneSolid)
                {
                    failureCode = "requiresolidground";
                    return false;
                }
            }
            return base.CanPlaceBlock(world, byPlayer, bs, ref failureCode);
        }

        private bool CanSupportThis(IBlockAccessor blockAccess, BlockPos pos, BlockFacing sideToTest)
        {
            Block block = blockAccess.GetBlock(pos);
            if (block.SideSolid[BlockFacing.UP.Index]) return true;
            if (sideToTest == null && block.FirstCodePart() == "roughhewnfence") return true;
            Cuboidf[] boxes = block.CollisionBoxes;
            if (boxes != null)
            {
                for (int i = 0; i < boxes.Length; i++)
                {
                    if (boxes[i].Y2 == 1.0f)
                    {
                        if (sideToTest == null) return true;   //any partial block below, with full height can support from beneath
                        if (sideToTest == BlockFacing.WEST && boxes[i].X1 != 0.0f) continue;
                        if (sideToTest == BlockFacing.EAST && boxes[i].X2 != 1.0f) continue;
                        if (sideToTest == BlockFacing.NORTH && boxes[i].Z1 != 0.0f) continue;
                        if (sideToTest == BlockFacing.SOUTH && boxes[i].Z2 != 1.0f) continue;
                        return true;
                    }
                }
            }
            return false;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.BlockAccessor.GetBlock(OrientedAsset("ew")));
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            return OrientedAsset(Orientation == BlockFacing.NORTH ? "ew" : "ns");
        }

    }
}
