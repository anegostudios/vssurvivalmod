using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class GenLightSurvival : ModSystem
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

            this.api.Event.ChunkColumnGeneration(this.OnChunkColumnGeneration, EnumWorldGenPass.Vegetation, "standard");
            this.api.Event.ChunkColumnGeneration(this.OnChunkColumnGenerationFlood, EnumWorldGenPass.NeighbourSunLightFlood, "standard");

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

        private void OnChunkColumnGeneration(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            api.WorldManager.SunFloodChunkColumnForWorldGen(chunks, chunkX, chunkZ);
            blockAccessor.RunScheduledBlockLightUpdates();
        }

        private void OnChunkColumnGenerationFlood(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            api.WorldManager.SunFloodChunkColumnNeighboursForWorldGen(chunks, chunkX, chunkZ);
        }

     
    }
}
