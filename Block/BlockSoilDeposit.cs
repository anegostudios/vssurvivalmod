using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSoilDeposit : BlockSoil
    {
        int soilBlockId;

        protected override int MaxStage => 1;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            string placeBelowBlockCode = Attributes?["placeBelowBlockCode"].AsString(null);

            if (placeBelowBlockCode != null)
            {
                Block block = api.World.GetBlock(new AssetLocation(placeBelowBlockCode));
                if (block != null)
                {
                    soilBlockId = block.BlockId;
                }
            }
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            blockAccessor.SetBlock(BlockId, pos);

            if (soilBlockId > 0 && blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z).BlockMaterial == EnumBlockMaterial.Stone)
            {
                blockAccessor.SetBlock(soilBlockId, pos.DownCopy());
            }

            return true;
        }


        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            base.OnServerGameTick(world, pos, extra);

            GrassTick tick = extra as GrassTick;
            world.BlockAccessor.SetBlock(tick.Grass.BlockId, pos);
            if (tick.TallGrass != null && world.BlockAccessor.GetBlock(pos.UpCopy()).BlockId == 0)
            {
                world.BlockAccessor.SetBlock(tick.TallGrass.BlockId, pos.UpCopy());
            }
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;

            bool isGrowing = false;

            Block grass;
            BlockPos upPos = pos.UpCopy();
            
            bool lowLightLevel = world.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.MaxLight) < growthLightLevel;
            if (lowLightLevel || isSmotheringBlock(world, upPos))
            {
                grass = tryGetBlockForDying(world);
            }
            else
            {
                isGrowing = true;
                grass = tryGetBlockForGrowing(world, pos);
            }

            if (grass != null)
            {
                extra = new GrassTick()
                {
                    Grass = grass,
                    TallGrass = isGrowing ? getTallGrassBlock(world, upPos, offThreadRandom) : null
                };
            }
            return extra != null;
        }
    }
}
