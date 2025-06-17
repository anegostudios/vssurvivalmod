using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockTermiteMound : BlockRequireSolidGround
    {
        static Dictionary<int, Block> mediumTermiteBlockCodeByRockid = new Dictionary<int, Block>();
        static Dictionary<int, Block> largeTermiteBlockCodeByRockid = new Dictionary<int, Block>();

        bool islarge;

        public override void OnUnloaded(ICoreAPI api)
        {
            mediumTermiteBlockCodeByRockid.Clear();
            largeTermiteBlockCodeByRockid.Clear();
        }

        public override void OnLoaded(ICoreAPI api)
        {
            islarge = Variant["size"] == "large";

            var rockBlock = api.World.GetBlock(new AssetLocation("rock-"+ Variant["rock"]));

            var dict = islarge ? largeTermiteBlockCodeByRockid : mediumTermiteBlockCodeByRockid;
            dict[rockBlock.Id] = api.World.GetBlock(CodeWithVariant("rock", Variant["rock"]));

            base.OnLoaded(api);
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            if (!HasSolidGround(blockAccessor, pos)) return false;

            // This needs a 3x3 flat area, looks weird otherwise
            if (islarge)
            {
                if (!blockAccessor.GetBlockRaw(pos.X - 1, pos.InternalY - 1, pos.Z - 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index]) return false;
                if (!blockAccessor.GetBlockRaw(pos.X + 0, pos.InternalY - 1, pos.Z - 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index]) return false;
                if (!blockAccessor.GetBlockRaw(pos.X + 1, pos.InternalY - 1, pos.Z - 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index]) return false;
                if (!blockAccessor.GetBlockRaw(pos.X + 1, pos.InternalY - 1, pos.Z + 0, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index]) return false;
                if (!blockAccessor.GetBlockRaw(pos.X + 1, pos.InternalY - 1, pos.Z + 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index]) return false;
                if (!blockAccessor.GetBlockRaw(pos.X + 0, pos.InternalY - 1, pos.Z + 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index]) return false;
                if (!blockAccessor.GetBlockRaw(pos.X - 1, pos.InternalY - 1, pos.Z + 1, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index]) return false;
                if (!blockAccessor.GetBlockRaw(pos.X - 1, pos.InternalY - 1, pos.Z - 0, BlockLayersAccess.Solid).SideSolid[BlockFacing.UP.Index]) return false;
            }

            var ch = GlobalConstants.ChunkSize;
            int rockId = blockAccessor.GetMapChunkAtBlockPos(pos).TopRockIdMap[(pos.Z % ch) * ch + (pos.X % ch)];

            Block tblock = null;
            if (islarge) largeTermiteBlockCodeByRockid.TryGetValue(rockId, out tblock);
            else mediumTermiteBlockCodeByRockid.TryGetValue(rockId, out tblock);

            if (tblock != null)
            {
                blockAccessor.SetBlock(tblock.Id, pos);
            }


            return true;
        }

    }
}
