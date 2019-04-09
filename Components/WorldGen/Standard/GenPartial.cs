using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public abstract class GenPartial : ModStdWorldGen
    {
        internal ICoreServerAPI api;
        internal int worldheight;
        public ushort airBlockId = 0;

        internal abstract int chunkRange { get; }

        internal LCGRandom chunkRand;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            chunksize = api.WorldManager.ChunkSize;

            api.Event.InitWorldGenerator(initWorldGen, "standard");
        }

        public virtual void initWorldGen()
        {
            LoadGlobalConfig(api);

            worldheight = api.WorldManager.MapSizeY;
            chunkRand = new LCGRandom(api.WorldManager.Seed);
        }


        protected virtual void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            for (int dx = -chunkRange; dx <= chunkRange; dx++)
            {
                for (int dz = -chunkRange; dz <= chunkRange; dz++)
                {
                    chunkRand.InitPositionSeed(chunkX + dx, chunkZ + dz);
                    GeneratePartial(chunks, chunkX, chunkZ, dx, dz);
                }
            }
        }



        public virtual void GeneratePartial(IServerChunk[] chunks, int chunkX, int chunkZ, int basePosX, int basePosZ)
        {

        }

    }
}
