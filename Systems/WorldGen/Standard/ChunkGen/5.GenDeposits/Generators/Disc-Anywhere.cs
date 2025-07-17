using Newtonsoft.Json;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods
{
    /// <summary>
    /// YPosRel<br/>
    /// 0 => y=0<br/>
    /// 1 => y=map height<br/>
    /// </summary>
    public class AnywhereDiscGenerator : DiscDepositGenerator
    {
        [JsonProperty]
        public NatFloat YPosRel;

        float step;

        public AnywhereDiscGenerator(ICoreServerAPI api, DepositVariant variant, LCGRandom depositRand, NormalizedSimplexNoise noiseGen) : base(api, variant, depositRand, noiseGen)
        {
        }

        public override void Init()
        {
            base.Init();

            YPosRel.avg *= Api.WorldManager.MapSizeY;
            YPosRel.var *= Api.WorldManager.MapSizeY;
        }

        protected override void beforeGenDeposit(IMapChunk mapChunk, BlockPos targetPos)
        {
            ypos = YPosRel.nextFloat(1, DepositRand);
            posyi = (int)ypos;

            targetPos.Y = posyi;

            currentRelativeDepth = ypos / Api.WorldManager.MapSizeY;

            step = (float)mapChunk.MapRegion.OreMapVerticalDistortTop.InnerSize / regionChunkSize;
            
        }

        public override void GetYMinMax(BlockPos pos, out double miny, out double maxy)
        {
            miny = 99999;
            maxy = -99999;
            for (int i = 0; i < 100; i++)
            {
                double y = YPosRel.nextFloat(1, DepositRand);
                miny = Math.Min(miny, y);
                maxy = Math.Max(maxy, y);
            }
        }

        protected override void loadYPosAndThickness(IMapChunk heremapchunk, int lx, int lz, BlockPos targetPos, double distanceToEdge)
        {
            double curTh = depoitThickness * GameMath.Clamp(distanceToEdge * 2 - 0.2, 0, 1);
            hereThickness = (int)curTh + ((DepositRand.NextDouble() < (curTh - (int)curTh)) ? 1 : 0);

            int yOff = (int)getDepositYDistort(targetPos, lx, lz, step, heremapchunk);
            
            targetPos.Y = posyi + yOff;
        }
    }
    }
