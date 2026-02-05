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

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // if broken by a player, let the TransformBreak BH handle it
            if (byPlayer != null)
            {
                base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            }
            // if broken due to BreakIfFloating then do not spawn the mount itself, only the termites and delete the block
            else if (Drops != null)
            {
                for (int i = 0; i < Drops.Length; i++)
                {
                    var drop = Drops[i].ToRandomItemstackForPlayer(null, world, dropQuantityMultiplier);
                    if (drop == null) continue;
                    if (SplitDropStacks)
                    {
                        for (int k = 0; k < drop.StackSize; k++)
                        {
                            ItemStack stack = drop.Clone();
                            stack.StackSize = 1;
                            world.SpawnItemEntity(stack, pos);
                        }
                    }
                    else
                    {
                        world.SpawnItemEntity(drop.Clone(), pos);
                    }
                }

                world.BlockAccessor.SetBlock(0, pos);
            }
        }
    }
}
