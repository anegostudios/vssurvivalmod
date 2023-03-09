using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods
{
    public class GenDungeons : ModStdWorldGen
    {
        ModSystemTiledDungeonGenerator dungeonGen;
        IWorldGenBlockAccessor worldgenBlockAccessor;
        LCGRandom rand;
        ICoreServerAPI api;

        Dictionary<long, List<DungeonPlaceTask>> dungeonPlaceTasksByRegion = new Dictionary<long, List<DungeonPlaceTask>>();

        int regionSize;
        bool genDungeons;

        public override double ExecuteOrder() { return 0.12; }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);

            dungeonGen = api.ModLoader.GetModSystem<ModSystemTiledDungeonGenerator>();
            dungeonGen.init();

            // Disabled because unfinished
            /*if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.MapRegionGeneration(onMapRegionGen, "standard");
                api.Event.ChunkColumnGeneration(onChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

                api.Event.MapRegionUnloaded += Event_MapRegionUnloaded;
                api.Event.MapRegionLoaded += Event_MapRegionLoaded;
            }*/
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            genDungeons = api.World.Config.GetAsString("loreContent", "true").ToBool(true);
            if (!genDungeons) return;

            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
            rand = new LCGRandom(api.WorldManager.Seed ^ 8991827198);
            chunksize = api.World.BlockAccessor.ChunkSize;
            regionSize = api.World.BlockAccessor.RegionSize;
        }

        private void Event_MapRegionLoaded(Vec2i mapCoord, IMapRegion region)
        {
            var tasks = region.GetModdata<List<DungeonPlaceTask>>("dungeonPlaceTasks");
            if (tasks != null)
            {
                dungeonPlaceTasksByRegion[MapRegionIndex2D(mapCoord.X, mapCoord.Y)] = tasks;
            }
        }

        private void Event_MapRegionUnloaded(Vec2i mapCoord, IMapRegion region)
        {
            if (dungeonPlaceTasksByRegion.TryGetValue(MapRegionIndex2D(mapCoord.X, mapCoord.Y), out var list))
            {
                if (list == null) return;
                region.SetModdata("dungeonPlaceTasks", list);
            }
        }

        private void onMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
        {
            int size = api.WorldManager.RegionSize;

            rand.InitPositionSeed(regionX * size, regionZ * size);
            
            long index = MapRegionIndex2D(regionX, regionZ);
            dungeonPlaceTasksByRegion[index] = new List<DungeonPlaceTask>();

            for (int i = 0; i < 3; i++)
            {
                int posx = regionX * size + rand.NextInt(size);
                int posz = regionZ * size + rand.NextInt(size);
                int posy = rand.NextInt(api.World.SeaLevel - 10);

                var dungeons = dungeonGen.Tcfg.Dungeons;
                var dungeon = dungeons[rand.NextInt(dungeons.Length)];
                var placeTask = dungeonGen.TryPregenerateTiledDungeon(rand, dungeon, new BlockPos(posx, posy, posz), 5, 20);

                if (placeTask != null)
                {   
                    dungeonPlaceTasksByRegion[index].Add(placeTask);
                }
            }
        }

        public long MapRegionIndex2D(int regionX, int regionZ)
        {
            return ((long)regionZ) * (api.WorldManager.MapSizeX / api.WorldManager.RegionSize) + regionX;
        }


        private void onChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams)
        {
            int regionx = (chunkX * chunksize) / regionSize;
            int regionz = (chunkZ * chunksize) / regionSize;

            int posX = chunkX * chunksize;
            int posZ = chunkZ * chunksize;

            Cuboidi cuboid = new Cuboidi(posX, 0, posZ, posX + chunksize, api.World.BlockAccessor.MapSizeY, posZ + chunksize);

            IMapRegion region = chunks[0].MapChunk.MapRegion;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dungeonPlaceTasksByRegion.TryGetValue(MapRegionIndex2D(regionx + dx, regionz + dz), out var dungePlaceTasks))
                    {
                        foreach (var placetask in dungePlaceTasks)
                        {
                            if (placetask.DungeonBoundaries.IntersectsOrTouches(cuboid))
                            {
                                generateDungeonPartial(region, placetask, chunks, chunkX, chunkZ);
                            }
                        }
                    }
                }
            }

            
        }

        private void generateDungeonPartial(IMapRegion region, DungeonPlaceTask dungeonPlaceTask, IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            if (!dungeonGen.Tcfg.DungeonsByCode.TryGetValue(dungeonPlaceTask.Code, out var dungeon)) return; // Ignore dungeons that no longer exist in assets

            foreach (var placeTask in dungeonPlaceTask.TilePlaceTasks)
            {
                if (!dungeon.TilesByCode.TryGetValue(placeTask.TileCode, out var tile)) continue; // Ignore dungeon tiles that no longer exist in assets
                
                var rndval = rand.NextInt(tile.ResolvedSchematic.Length);
                var schematic = tile.ResolvedSchematic[rndval][placeTask.Rotation];
                schematic.PlacePartial(chunks, worldgenBlockAccessor, api.World, chunkX, chunkZ, placeTask.Pos, EnumReplaceMode.ReplaceAll, true);

                string code = "dungeon/" + tile.Code + (schematic == null ? "" : "/" + schematic.FromFileName);

                region.GeneratedStructures.Add(new GeneratedStructure() { Code = code, Group = dungeonPlaceTask.Code, Location = new Cuboidi(placeTask.Pos, placeTask.Pos.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ)) });
                region.DirtyForSaving = true;

                /*if (placeTask.BuildProtected)
                {
                    api.World.Claims.Add(new LandClaim()
                    {
                        Areas = new List<Cuboidi>() { loc.Clone() },
                        Description = struc.BuildProtectionDesc,
                        ProtectionLevel = 10,
                        LastKnownOwnerName = struc.BuildProtectionName,
                        AllowUseEveryone = true
                    });
                }*/
            }
        }
    }
}
