using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods
{
    public abstract class DepositGeneratorBase
    {
        public ICoreServerAPI Api;
        protected const int chunksize = GlobalConstants.ChunkSize;
        public LCGRandom DepositRand;
        public NormalizedSimplexNoise DistortNoiseGen;
        protected DepositVariant variant;


        public bool blockCallBacks = true;

        public DepositGeneratorBase(ICoreServerAPI api, DepositVariant variant, LCGRandom depositRand, NormalizedSimplexNoise noiseGen)
        {
            this.variant = variant;
            this.Api = api;
            this.DepositRand = depositRand;
            this.DistortNoiseGen = noiseGen;

        }

        public abstract void GenDeposit(IBlockAccessor blockAccessor, IServerChunk[] chunks, int originChunkX, int originChunkZ, BlockPos pos, ref Dictionary<BlockPos, DepositVariant> subDepositsToPlace);

        public virtual void Init() { }

        public virtual DepositVariant[] Resolve(DepositVariant sourceVariant)
        {
            return new DepositVariant[] { sourceVariant };
        }

        //public abstract float GetAbsAvgQuantity();
        //public abstract int[] GetBearingBlocks();

        public abstract float GetMaxRadius();

        public abstract void GetPropickReading(BlockPos pos, int oreDist, int[] blockColumn, out double ppt, out double totalFactor);

        /// <summary>
        /// For pro pick readings, evaluate min max y-location where the ore can spawn in. Can be approximate
        /// </summary>
        /// <param name="miny"></param>
        /// <param name="maxy"></param>
        public abstract void GetYMinMax(BlockPos pos, out double miny, out double maxy);

    }
}
