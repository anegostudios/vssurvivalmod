using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class GenLight : ModSystem
    {
        private ICoreServerAPI api;

        IWorldGenBlockAccessor blockAccessor;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            this.api.Event.ChunkColumnGeneration(this.OnChunkColumnGeneration, EnumWorldGenPass.Vegetation, EnumPlayStyleFlag.All);
            this.api.Event.ChunkColumnGeneration(this.OnChunkColumnGenerationFlood, EnumWorldGenPass.NeighbourSunLightFlood, EnumPlayStyleFlag.All);

            this.api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(false);
        }

        public override double ExecuteOrder()
        {
            return 0.95;
        }


        private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            api.WorldManager.SunFloodChunkColumnForWorldGen(chunks, chunkX, chunkZ);
            blockAccessor.RunScheduledBlockLightUpdates();
        }

        private void OnChunkColumnGenerationFlood(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            api.WorldManager.SunFloodChunkColumnNeighboursForWorldGen(chunks, chunkX, chunkZ);
        }

     
    }
}
