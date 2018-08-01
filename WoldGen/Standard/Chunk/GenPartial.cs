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

        internal FastPositionalRandom chunkRand;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            chunksize = api.WorldManager.ChunkSize;

            // Call our loaded method manually if the server is already running (happens when mods are reloaded at runtime)
            if (api.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
            {
                OnGameWorldLoaded();
            }

            api.Event.SaveGameLoaded(OnGameWorldLoaded);

        }

        internal virtual void OnGameWorldLoaded()
        {
            LoadGlobalConfig(api);

            worldheight = api.WorldManager.MapSizeY;
            chunkRand = new FastPositionalRandom(api.WorldManager.Seed);
        }


        internal virtual void GenChunkColumn(IServerChunk[] chunks, int chunkX, int chunkZ)
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



        internal virtual void GeneratePartial(IServerChunk[] chunks, int chunkX, int chunkZ, int basePosX, int basePosZ)
        {

        }

    }
}
