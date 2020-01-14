using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods
{
    public class FollowSurfaceDiscGenerator : DiscDepositGenerator
    {
        [JsonProperty]
        public NatFloat YPosRel;

        float step;

        public FollowSurfaceDiscGenerator(ICoreServerAPI api, DepositVariant variant, LCGRandom depositRand, NormalizedSimplexNoise noiseGen) : base(api, variant, depositRand, noiseGen)
        {
        }

        protected override void beforeGenDeposit(IMapChunk mapChunk, BlockPos pos)
        {
            ypos = YPosRel.nextFloat(1, DepositRand);

            pos.Y = (int)ypos;

            step = (float)mapChunk.MapRegion.OreMapVerticalDistortTop.InnerSize / regionChunkSize;
        }


        public override void GetYMinMax(BlockPos pos, out double miny, out double maxy)
        {
            float yrel = 9999;
            for (int i = 0; i < 100; i++)
            {
                yrel = Math.Min(yrel, YPosRel.nextFloat(1, DepositRand));
            }

            miny = yrel * pos.Y;
            maxy = (float)pos.Y;
        }


        protected override void loadYPosAndThickness(IMapChunk heremapchunk, int lx, int lz, BlockPos pos, double distanceToEdge)
        {
            hereThickness = depoitThickness;

            pos.Y = (int)(ypos * heremapchunk.WorldGenTerrainHeightMap[lz * chunksize + lx]);
            pos.Y -= (int)getDepositYDistort(pos, lx, lz, step, heremapchunk);
            
            double curTh = depoitThickness * GameMath.Clamp(distanceToEdge * 2 - 0.2, 0, 1);
            hereThickness = (int)curTh + ((DepositRand.NextDouble() < (curTh - (int)curTh)) ? 1 : 0);
        }
    }
    
}
