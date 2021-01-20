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

        public override void OnJsonTesselation(ref MeshData sourceMesh, BlockPos pos, int[] chunkExtIds, ushort[] chunkLightExt, int extIndex3d)
        {
            base.OnJsonTesselation(ref sourceMesh, pos, chunkExtIds, chunkLightExt, extIndex3d);

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


        public void GrowTree(IBlockAccessor blockAccessor, BlockPos pos, float sizeModifier = 1, float vineGrowthChance = 0, float forestDensity = 0)
        {
            GrowOneFern(blockAccessor, pos.UpCopy(), sizeModifier, vineGrowthChance);
        }

        private void GrowOneFern(IBlockAccessor blockAccessor, BlockPos upos, float sizeModifier, float vineGrowthChance)
        {
            int height = GameMath.Clamp((int)(sizeModifier * (2 + rand.Next(5))), 2, 6);

            Block trunkTop = height > 2 ? trunkTopOld : trunkTopMedium;
            if (height == 1) trunkTop = trunkTopYoung;

            for (int i = 0; i < height; i++)
            {
                Block toplaceblock = trunk;
                if (i == height - 1) toplaceblock = foliage;
                if (i == height - 2) toplaceblock = trunkTop;

                if (!blockAccessor.GetBlock(upos).IsReplacableBy(toplaceblock)) return;
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
