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
            var rand2 = new LCGRandom(api.WorldManager.Seed ^ 8991827198);
            rand2.InitPositionSeed(regionX * size, regionZ * size);

            long index = MapRegionIndex2D(regionX, regionZ);
            dungeonPlaceTasksByRegion[index] = new List<DungeonPlaceTask>();

            for (int i = 0; i < 3; i++)
            {
                int posx = regionX * size + rand2.NextInt(size);
                int posz = regionZ * size + rand2.NextInt(size);
                int posy = rand2.NextInt(api.World.SeaLevel - 10);
                api.Logger.Event($"Dungeon @: /tp ={posx} {posy} ={posz}");

                var dungeons = dungeonGen.Tcfg.Dungeons;
                // var dungeon = dungeons[rand2.NextInt(dungeons.Length)].Copy();
                // need copy else shuffle breaks determinism
                var dungeon = dungeons[0].Copy();
                var placeTask = dungeonGen.TryPregenerateTiledDungeon(rand2, dungeon, new BlockPos(posx, posy, posz), 5, 50);

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


        private void onChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            int regionx = (request.ChunkX * chunksize) / regionSize;
            int regionz = (request.ChunkZ * chunksize) / regionSize;

            int posX = request.ChunkX * chunksize;
            int posZ = request.ChunkZ * chunksize;

            Cuboidi cuboid = new Cuboidi(posX, 0, posZ, posX + chunksize, api.World.BlockAccessor.MapSizeY, posZ + chunksize);
            var baseRoom = new Cuboidi();
            IMapRegion region = request.Chunks[0].MapChunk.MapRegion;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!dungeonPlaceTasksByRegion.TryGetValue(MapRegionIndex2D(regionx + dx, regionz + dz), out var dungePlaceTasks)) continue;

                    foreach (var placetask in dungePlaceTasks)
                    {
                        if (!placetask.DungeonBoundaries.IntersectsOrTouches(cuboid)) continue;

                        // check stairs gen to surface only for first "main" room
                        var tileTask = placetask.TilePlaceTasks[0];
                        baseRoom.Set(tileTask.Pos, tileTask.Pos.AddCopy(tileTask.SizeX, tileTask.SizeY, tileTask.SizeZ));
                        if (cuboid.IntersectsOrTouches(baseRoom))
                        {
                            // gen stairway
                            if (!dungeonGen.Tcfg.DungeonsByCode.TryGetValue(placetask.Code, out var dungeon)) return; // Ignore dungeons that no longer exist in assets
                            var height = api.World.BlockAccessor.GetTerrainMapheightAt(tileTask.Pos);
                            var num = (height - tileTask.Pos.Y) / dungeon.Stairs[0].SizeY;
                            var stairPos = tileTask.Pos.AddCopy(tileTask.SizeX/2-dungeon.Stairs[0].SizeX/2, tileTask.SizeY, tileTask.SizeZ/2-dungeon.Stairs[0].SizeZ/2);
                            for (var i = 0; i < num; i++)
                            {
                                dungeon.Stairs[0].Place(worldgenBlockAccessor, api.World, stairPos);
                                stairPos.Y += dungeon.Stairs[0].SizeY;
                            }
                            region.AddGeneratedStructure(new GeneratedStructure()
                            {
                                Code = "dungeon/"+dungeon.Code + "/"+ dungeon.Stairs[0].FromFileName,
                                Group = placetask.Code,
                                Location = new Cuboidi(tileTask.Pos.AddCopy(0, tileTask.SizeY, 0), stairPos.AddCopy(dungeon.Stairs[0].SizeX,0,dungeon.Stairs[0].SizeZ)),
                                SuppressTreesAndShrubs = true,
                                SuppressRivulets = true
                            });
                        }
                        generateDungeonPartial(region, placetask, request.Chunks, request.ChunkX, request.ChunkZ);
                    }
                }
            }


        }

        private void generateDungeonPartial(IMapRegion region, DungeonPlaceTask dungeonPlaceTask, IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            if (!dungeonGen.Tcfg.DungeonsByCode.TryGetValue(dungeonPlaceTask.Code, out var dungeon)) return; // Ignore dungeons that no longer exist in assets
            var rand2 = new LCGRandom(api.WorldManager.Seed ^ 8991827198);

            int size = api.WorldManager.RegionSize;
            rand2.InitPositionSeed((chunkX / size) * size, (chunkZ / size) * size);

            foreach (var placeTask in dungeonPlaceTask.TilePlaceTasks)
            {
                if (!dungeon.TilesByCode.TryGetValue(placeTask.TileCode, out var tile)) continue; // Ignore dungeon tiles that no longer exist in assets

                var rndval = rand2.NextInt(tile.ResolvedSchematic.Length);
                var schematic = tile.ResolvedSchematic[rndval][placeTask.Rotation];
                schematic.PlacePartial(chunks, worldgenBlockAccessor, api.World, chunkX, chunkZ, placeTask.Pos, EnumReplaceMode.ReplaceAll, null, true, true);

                string code = "dungeon/" + tile.Code + (schematic == null ? "" : "/" + schematic.FromFileName);

                region.AddGeneratedStructure(new GeneratedStructure()
                {
                    Code = code,
                    Group = dungeonPlaceTask.Code,
                    Location = new Cuboidi(placeTask.Pos, placeTask.Pos.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ)),
                    SuppressTreesAndShrubs = true,
                    SuppressRivulets = true
                });

                // if (placeTask.BuildProtected)
                // {
                    // api.World.Claims.Add(new LandClaim()
                    // {
                    //     Areas = new List<Cuboidi>() { loc.Clone() },
                    //     Description = struc.BuildProtectionDesc,
                    //     ProtectionLevel = 10,
                    //     LastKnownOwnerName = struc.BuildProtectionName,
                    //     AllowUseEveryone = true
                    // });
                // }
            }
        }
    }
}
