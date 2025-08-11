using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockRainAmbient : Block
    {
        ICoreClientAPI capi;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;
        }

        public override float GetAmbientSoundStrength(IWorldAccessor world, BlockPos pos)
        {
            var conds = capi.World.Player.Entity.selfClimateCond;
            if (conds != null && conds.Rainfall > 0.1f && conds.Temperature > 3f && (world.BlockAccessor.GetRainMapHeightAt(pos) <= pos.Y || world.BlockAccessor.GetDistanceToRainFall(pos, 3, 1) <= 2))
            {
                return conds.Rainfall;
            }

            return 0;
        }
    }

    public class BlockGlassPane : BlockRainAmbient
    {
        /// <summary>
        /// This is the light pass-through direction for the glass pane
        /// </summary>
        public BlockFacing Orientation { get; set; }
        public string Frame { get; set; }
        public string GlassType { get; set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.Orientation = BlockFacing.FromFirstLetter(Variant["type"].Substring(0, 1));
            this.Frame = Variant["wood"] is string w ? string.Intern(w) : null;
            this.GlassType = Variant["glass"] is string g ? string.Intern(g) : null;
        }

        protected AssetLocation OrientedAsset(string orientation)
        {
            return CodeWithVariants(new string[] { "glass", "wood", "type" }, new string[] { GlassType, Frame, orientation });
        }

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
            BlockPos pos = bs.Position;

            // Logic waterfall for smart placement:
            // 1. if adjacent pane vertically, snap to it (if there are two orthogonally, no decision)
            // 2. if adjacent pane horizontally, snap to it (if there are two orthogonally, no decision; if three, take the majority)
            // 3. If any blocks horizontally, orient towards clear-through direction
            // 4. Respect SuggestedHV

            //1
            Block upBlock = world.GetBlock(pos.UpCopy());    //##TODO maybe an IBlockAccessor.getNeighbours() method?  if going to be coding like this a lot?
            Block downBlock = world.GetBlock(pos.DownCopy());
            int upConnect = (upBlock is BlockGlassPane ub) ? (ub.Orientation == BlockFacing.EAST ? 1 : -1) : 0;
            int downConnect = (downBlock is BlockGlassPane db) ? (db.Orientation == BlockFacing.EAST ? 1 : -1) : 0;
            int vertConnect = upConnect + downConnect;
            if (vertConnect > 0) return BlockFacing.EAST;
            if (vertConnect < 0) return BlockFacing.NORTH;

            //2
            Block westBlock = world.GetBlock(pos.WestCopy());
            Block eastBlock = world.GetBlock(pos.EastCopy());
            Block northBlock = world.GetBlock(pos.NorthCopy());
            Block southBlock = world.GetBlock(pos.SouthCopy());
            int westConnect = (westBlock is BlockGlassPane wb) && wb.Orientation == BlockFacing.NORTH ? 1 : 0;
            int eastConnect = (eastBlock is BlockGlassPane eb) && eb.Orientation == BlockFacing.NORTH ? 1 : 0;
            int northConnect = (northBlock is BlockGlassPane nb) && nb.Orientation == BlockFacing.EAST ? 1 : 0;
            int southConnect = (southBlock is BlockGlassPane sb) && sb.Orientation == BlockFacing.EAST ? 1 : 0;

            if (westConnect + eastConnect - northConnect - southConnect > 0) return BlockFacing.NORTH;
            if (northConnect + southConnect - westConnect - eastConnect > 0) return BlockFacing.EAST;

            //3
            int westLight = westBlock.GetLightAbsorption(world, pos.WestCopy()) + eastBlock.GetLightAbsorption(world, pos.EastCopy());
            int northLight = northBlock.GetLightAbsorption(world, pos.NorthCopy()) + southBlock.GetLightAbsorption(world, pos.SouthCopy());
            if (westLight < northLight) return BlockFacing.EAST;
            if (westLight > northLight) return BlockFacing.NORTH;

            return suggested;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.BlockAccessor.GetBlock(OrientedAsset("ew")));
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing nowFacing = BlockFacing.FromFirstLetter(Variant["type"][0] + "");
            BlockFacing rotatedFacing = BlockFacing.HORIZONTALS_ANGLEORDER[(nowFacing.HorizontalAngleIndex + angle / 90) % 4];

            string type = Variant["type"];

            if (nowFacing.Axis != rotatedFacing.Axis)
            {
                type = type == "ns" ? "ew" : "ns";
            }

            return CodeWithVariant("type", type);
        }

        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
        {
            string orient = Variant["type"];

            if (orient == "ns" && (facing == BlockFacing.NORTH || facing == BlockFacing.SOUTH)) return 1;
            if (orient == "ew" && (facing == BlockFacing.EAST || facing == BlockFacing.WEST)) return 1;

            return 0;
        }
    }
}
