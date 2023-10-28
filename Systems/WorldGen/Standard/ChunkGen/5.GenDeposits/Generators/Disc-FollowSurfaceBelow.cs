using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{

    public class FollowSurfaceBelowDiscGenerator : DiscDepositGenerator
    {
        
        public FollowSurfaceBelowDiscGenerator(ICoreServerAPI api, DepositVariant variant, LCGRandom depositRand, NormalizedSimplexNoise noiseGen) : base(api, variant, depositRand, noiseGen)
        {
        }

        protected override void beforeGenDeposit(IMapChunk mapChunk, BlockPos pos)
        {
            ypos = Depth.nextFloat(1, DepositRand);
            posyi = (int)ypos;

            int lx = pos.X % chunksize;
            int lz = pos.Z % chunksize;
            if (lx < 0 || lz < 0) return;
                 
            currentRelativeDepth = ypos / mapChunk.WorldGenTerrainHeightMap[lz * chunksize + lx];
        }

        public override void GetYMinMax(BlockPos pos, out double miny, out double maxy)
        {
            float maxyf = 0;
            for (int i = 0; i < 100; i++)
            {
                maxyf = Math.Max(maxyf, Depth.nextFloat(1, DepositRand));
            }

            miny = (pos.Y - maxyf);
            maxy = (float)pos.Y;
        }


        protected override void loadYPosAndThickness(IMapChunk heremapchunk, int lx, int lz, BlockPos targetPos, double distanceToEdge)
        {
            double curTh = depoitThickness * GameMath.Clamp(distanceToEdge * 2 - 0.2, 0, 1);
            hereThickness = (int)curTh + ((DepositRand.NextDouble() < (curTh - (int)curTh)) ? 1 : 0);

            targetPos.Y = heremapchunk.WorldGenTerrainHeightMap[lz * chunksize + lx] - posyi;
        }
    }


    

}
