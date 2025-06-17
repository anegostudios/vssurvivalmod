using Vintagestory.API.Common;
using Vintagestory.API.Server;

#nullable disable

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

        private void OnChunkColumnGeneration(IChunkColumnGenerateRequest request)
        {
            blockAccessor.BeginColumn();
            api.WorldManager.SunFloodChunkColumnForWorldGen(request.Chunks, request.ChunkX, request.ChunkZ);
            blockAccessor.RunScheduledBlockLightUpdates(request.ChunkX, request.ChunkZ);
        }

        private void OnChunkColumnGenerationFlood(IChunkColumnGenerateRequest request)
        {
            api.WorldManager.SunFloodChunkColumnNeighboursForWorldGen(request.Chunks, request.ChunkX, request.ChunkZ);
        }

     
    }
}
