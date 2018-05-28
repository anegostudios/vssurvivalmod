using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenStructures : ModStdWorldGen
    {
        ICoreServerAPI api;

        int worldheight;
        int chunkMapSizeY;
        int regionChunkSize;

        ushort[] heightmap;
        int forestUpLeft;
        int forestUpRight;
        int forestBotLeft;
        int forestBotRight;
        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;

        WorldGenStructuresConfig scfg;
        Random rnd;

        public override double ExecuteOrder() { return 0.3; }

        IWorldGenBlockAccessor worldgenBlockAccessor;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);
            api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures);
            api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            api.Event.SaveGameLoaded(OnGameWorldLoaded);

            // Call our loaded method manually if the server is already running (happens when mods are reloaded at runtime)
            if (api.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
            {
                OnGameWorldLoaded();
            }

        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }

       


        internal void OnGameWorldLoaded()
        {
            chunksize = api.WorldManager.ChunkSize;
            worldheight = api.WorldManager.MapSizeY;
            chunkMapSizeY = api.WorldManager.MapSizeY / chunksize;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;

            IAsset asset = api.Assets.Get("worldgen/terrain/standard/structures.json");
            scfg = asset.ToObject<WorldGenStructuresConfig>();
            scfg.Init(api);

            rnd = new Random(api.WorldManager.Seed + 2132121);
        }

        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            if (!TerraGenConfig.GenerateStructures) return;

            IMapRegion region = chunks[0].MapChunk.MapRegion;

            IntMap forestMap = region.ForestMap;
            IntMap climateMap = region.ClimateMap;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;


            // A region has 16 chunks
            // Size of the forest map is RegionSize / TerraGenConfig.forestMapScale  => 32*16 / 32  = 16 pixel
            // rlX, rlZ goes from 0..16 pixel
            // facF = 16/16 = 1
            // Get 4 pixels for chunkx, chunkz, chunkx+1 and chunkz+1 inside the map
            float facF = (float)forestMap.InnerSize / regionChunkSize;
            forestUpLeft = forestMap.GetUnpaddedInt((int)(rlX * facF), (int)(rlZ * facF));
            forestUpRight = forestMap.GetUnpaddedInt((int)(rlX * facF + facF), (int)(rlZ * facF));
            forestBotLeft = forestMap.GetUnpaddedInt((int)(rlX * facF), (int)(rlZ * facF + facF));
            forestBotRight = forestMap.GetUnpaddedInt((int)(rlX * facF + facF), (int)(rlZ * facF + facF));

            float facC = (float)climateMap.InnerSize / regionChunkSize;
            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));

            heightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

            BlockPos pos = new BlockPos();

            for (int i = 0; i < scfg.Structures.Length; i++)
            {
                WorldGenStructure struc = scfg.Structures[i];
                float chance = struc.Chance * scfg.ChanceMultiplier;

                while (chance-- > rnd.NextDouble())
                {
                    int dx = rnd.Next(chunksize);
                    int dz = rnd.Next(chunksize);
                    int ySurface = heightmap[dz * chunksize + dx];
                    if (ySurface <= 0 || ySurface >= worldheight - 15) continue;


                    if (struc.Placement == EnumStructurePlacement.Underground)
                    {
                        if (struc.Depth != null)
                        {
                            pos.Set(chunkX * chunksize + dx, ySurface - (int)struc.Depth.nextFloat(), chunkZ * chunksize + dz);
                        }
                        else
                        {
                            pos.Set(chunkX * chunksize + dx, 8 + rnd.Next(ySurface - 8 - 5), chunkZ * chunksize + dz);
                        }
                        
                    } else
                    {
                        pos.Set(chunkX * chunksize + dx, ySurface, chunkZ * chunksize + dz);
                    }

                    struc.TryGenerate(worldgenBlockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);                    
                }
            }
        }
    }
}
