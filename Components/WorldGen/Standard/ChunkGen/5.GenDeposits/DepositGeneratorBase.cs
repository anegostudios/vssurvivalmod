using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public abstract class DepositGeneratorBase
    {
        public ICoreServerAPI Api;
        public LCGRandom DepositRand;
        public NormalizedSimplexNoise DistortNoiseGen;
        protected DepositVariant variant;

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

        public abstract float GetAbsAvgQuantity();

        public abstract ushort[] GetBearingBlocks();

        public abstract float GetMaxRadius();
    }
}
