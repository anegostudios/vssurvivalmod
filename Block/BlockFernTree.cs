using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockFernTree : Block, ITreeGenerator
    {
        public Block trunk;
        public Block trunkTopYoung;
        public Block trunkTopMedium;
        public Block trunkTopOld;
        public Block foliage;

        static Random rand = new Random();


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            ICoreServerAPI sapi = api as ICoreServerAPI;
            if (sapi != null)
            {
                if (Code.Path.Equals("ferntree-normal-trunk"))
                {
                    sapi.RegisterTreeGenerator(new AssetLocation("ferntree-normal-trunk"), this);
                }
            }

            if (trunk == null)
            {
                IBlockAccessor blockAccess = api.World.BlockAccessor;

                trunk = blockAccess.GetBlock(new AssetLocation("ferntree-normal-trunk"));
                trunkTopYoung = blockAccess.GetBlock(new AssetLocation("ferntree-normal-trunk-top-young"));
                trunkTopMedium = blockAccess.GetBlock(new AssetLocation("ferntree-normal-trunk-top-medium"));
                trunkTopOld = blockAccess.GetBlock(new AssetLocation("ferntree-normal-trunk-top-old"));
                foliage = blockAccess.GetBlock(new AssetLocation("ferntree-normal-foliage"));
            }
        }

        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos)
        {
            base.OnDecalTesselation(world, decalMesh, pos);
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);

            if (this == foliage)
            {
                for (int i = 0; i < sourceMesh.FlagsCount; i++)
                {
                    sourceMesh.Flags[i] = (sourceMesh.Flags[i] & ~VertexFlags.NormalBitMask) | BlockFacing.UP.NormalPackedFlags;
                }
            }
        }


        public string Type()
        {
            return Variant["type"];
        }


        public void GrowTree(IBlockAccessor blockAccessor, BlockPos pos, bool skipForestFloor, float sizeModifier = 1, float vineGrowthChance = 0, float otherBlockChance = 1, int treesInChunkGenerated = 0)
        {
            float f = otherBlockChance == 0 ? 1 + (float)rand.NextDouble() * 2.5f : 1.5f + (float)rand.NextDouble() * 4;
            int quantity = GameMath.RoundRandom(rand, f);

            while (quantity-- > 0)
            {
                GrowOneFern(blockAccessor, pos.UpCopy(), sizeModifier, vineGrowthChance);

                // Potentially grow another one nearby
                pos.X += rand.Next(8) - 4;
                pos.Z += rand.Next(8) - 4;

                // Test up to 2 blocks up and down.
                bool foundSuitableBlock = false;
                for (int y = 2; y >= -2; y--)
                {
                    Block block = blockAccessor.GetBlock(pos.X, pos.Y + y, pos.Z);
                    if (block.Fertility > 0 && !blockAccessor.GetBlock(pos.X, pos.Y + y + 1, pos.Z).IsLiquid())
                    {
                        pos.Y = pos.Y + y;
                        foundSuitableBlock = true;
                        break;
                    }
                }
                if (!foundSuitableBlock) break;
            }

            
        }

        private void GrowOneFern(IBlockAccessor blockAccessor, BlockPos upos, float sizeModifier, float vineGrowthChance)
        {
            int height = GameMath.Clamp((int)(sizeModifier * (2 + rand.Next(6))), 2, 6);

            Block trunkTop = height > 2 ? trunkTopOld : trunkTopMedium;
            if (height == 1) trunkTop = trunkTopYoung;

            for (int i = 0; i < height; i++)
            {
                Block toplaceblock = trunk;
                if (i == height - 1) toplaceblock = foliage;
                if (i == height - 2) toplaceblock = trunkTop;

                if (!blockAccessor.GetBlock(upos.X, upos.Y + i, upos.Z).IsReplacableBy(toplaceblock)) return;
            }

            for (int i = 0; i < height; i++)
            {
                Block toplaceblock = trunk;
                if (i == height - 1) toplaceblock = foliage;
                if (i == height - 2) toplaceblock = trunkTop;

                blockAccessor.SetBlock(toplaceblock.BlockId, upos);
                upos.Up();
            }
        }

    }
}
