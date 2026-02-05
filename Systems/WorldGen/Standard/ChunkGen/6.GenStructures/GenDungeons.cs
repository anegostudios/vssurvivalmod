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
        ModSystemTiledDungeons tiledDungeonsSys = null!;
        IWorldGenBlockAccessor worldgenBlockAccessor = null!;
        ICoreServerAPI sapi = null!;
        LCGRandom rand = null!;

        Dictionary<long, List<DungeonPlaceTask>> dungeonPlaceTasksByRegion = new Dictionary<long, List<DungeonPlaceTask>>();

        int regionSize;
        bool genDungeons;

        public override double ExecuteOrder() { return 0.12; }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(api);

            tiledDungeonsSys = api.ModLoader.GetModSystem<ModSystemTiledDungeons>();

            // Disabled because unfinished
            // if (TerraGenConfig.DoDecorationPass)
            // {
            //     api.Event.MapRegionGeneration(onMapRegionGen, "standard");
            //     api.Event.ChunkColumnGeneration(onChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
            //     api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            //
            //     api.Event.MapRegionUnloaded += Event_MapRegionUnloaded;
            //     api.Event.MapRegionLoaded += Event_MapRegionLoaded;
            //     api.Event.InitWorldGenerator(OnInitWorldGenerator, "standard");
            // }
        }

        private void OnInitWorldGenerator()
        {
            tiledDungeonsSys.Init();
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            genDungeons = sapi.World.Config.GetAsString("loreContent", "true").ToBool(true);
            if (!genDungeons) return;

            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
            rand = new LCGRandom(sapi.WorldManager.Seed ^ 8991827198);
            regionSize = sapi.World.BlockAccessor.RegionSize;
        }

        private void Event_MapRegionLoaded(Vec2i mapCoord, IMapRegion region)
        {
            try
            {
                var tasks = region.GetModdata<List<DungeonPlaceTask>>("dungeonPlaceTasks");
                if (tasks != null)
                {
                    dungeonPlaceTasksByRegion[MapRegionIndex2D(mapCoord.X, mapCoord.Y)] = tasks;
                }
            }
            catch
            {

            }
        }

        private void Event_MapRegionUnloaded(Vec2i mapCoord, IMapRegion region)
        {
            if (dungeonPlaceTasksByRegion.TryGetValue(MapRegionIndex2D(mapCoord.X, mapCoord.Y), out var list))
            {
                region.SetModdata("dungeonPlaceTasks", list);
            }
        }

        private void onMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute? chunkGenParams = null)
        {
            int size = sapi.WorldManager.RegionSize;
            var rand2 = new LCGRandom(sapi.WorldManager.Seed ^ 8991827198);
            rand2.InitPositionSeed(regionX * size, regionZ * size);

            long mapRegionIndex = MapRegionIndex2D(regionX, regionZ);
            dungeonPlaceTasksByRegion[mapRegionIndex] = new List<DungeonPlaceTask>();

            var seaLevel = sapi.World.SeaLevel;
            // sapi.Logger.Notification($"Dungeon region: {regionX} {regionZ}");
            for (int i = 0; i < 12; i++)
            {
                int posx = regionX * size + rand2.NextInt(size);
                int posz = regionZ * size + rand2.NextInt(size);
                int posy = seaLevel/2 + rand2.NextInt(seaLevel/2 - 20);

                if(!SatisfiesMinDistance(mapRegionIndex, posx, posy, posz)) continue;

                if (IsInOcean(mapRegion, posx, posz)) continue;

                var dungeons = tiledDungeonsSys.Tcfg.Dungeons;
                // var dungeon = dungeons[rand2.NextInt(dungeons.Length)].Copy();
                // need copy else shuffle breaks determinism
                var dungeon = dungeons[0].Copy();

                if (!HasSuitableLandforms(dungeon, mapRegion, posx, posz))
                {
                    continue;
                }

                var dungeonStartPos = new BlockPos(posx, posy, posz);
                var placeTask = tiledDungeonsSys.TryPregenerateTiledDungeon(rand2, dungeon, mapRegion.GeneratedStructures, dungeonStartPos, 5, 50);

                if (placeTask != null)
                {
                    sapi.Logger.Notification($"Dungeon @: /tp ={posx} {posy} ={posz}");
                    dungeonPlaceTasksByRegion[mapRegionIndex].Add(placeTask);
                    mapRegion.AddGeneratedStructures(placeTask.GeneratedStructures);
                    break;
                }
            }
        }

        private bool SatisfiesMinDistance(long mapRegionIndex, int posx, int posy, int posz)
        {
            bool skip = false;
            foreach (var regionPlaceTask in dungeonPlaceTasksByRegion[mapRegionIndex])
            {
                if (regionPlaceTask.DungeonBoundaries.FastCenter.DistanceSq(posx, posy, posz) < tiledDungeonsSys.Tcfg.MinDistanceSq)
                {
                    skip = true;
                    break;
                }
            }

            return skip;
        }

        private bool IsInOcean(IMapRegion mapRegion, int posx, int posz)
        {
            if (mapRegion.OceanMap != null && mapRegion.OceanMap.Data.Length > 0)
            {
                var regionChunkSize = regionSize / chunksize;
                var chunkSize = sapi.WorldManager.ChunkSize;
                var rlX = (posx % chunkSize) % regionChunkSize;
                var rlZ = (posz % chunkSize) % regionChunkSize;
                var oFac = (float)mapRegion.OceanMap.InnerSize / regionChunkSize;
                var oceanUpLeft = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac), (int)(rlZ * oFac));
                var oceanUpRight = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac + oFac), (int)(rlZ * oFac));
                var oceanBotLeft = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac), (int)(rlZ * oFac + oFac));
                var oceanBotRight = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac + oFac), (int)(rlZ * oFac + oFac));
                float oceanicity = GameMath.BiLerp(oceanUpLeft, oceanUpRight, oceanBotLeft, oceanBotRight, (float)(posx % chunkSize) / chunkSize, (float)(posz % chunkSize) / chunkSize);
                if (oceanicity > 0)
                {
                    // skip ocean chunks/regions
                    return true;
                }
            }

            return false;
        }

        private bool HasSuitableLandforms(TiledDungeon dungeon, IMapRegion mapRegion, int posx, int posz)
        {
            if(dungeon.RequiredLandform == null) return true;

            int noiseSizeLandform = mapRegion.LandformMap.InnerSize;
            float posXInRegion = ((float)posx / regionSize - posx / regionSize) * noiseSizeLandform;
            float posZInRegion = ((float)posz / regionSize - posz / regionSize) * noiseSizeLandform;

            var landforms = NoiseLandforms.landforms.LandFormsByIndex;
            var landformMap = mapRegion.LandformMap;
            var map = new LerpedWeightedIndex2DMap(landformMap.Data, mapRegion.LandformMap.Size, TerraGenConfig.landFormSmoothingRadius, landformMap.TopLeftPadding, landformMap.BottomRightPadding);

            var weightedIndices = new Dictionary<string, WeightedIndex>();
            foreach (var index in map[posXInRegion, posZInRegion])
            {
                weightedIndices.Add(landforms[index.Index].Code.ToString(), index);
            }

            bool isSuitable = true;
            foreach (var landform in dungeon.RequiredLandform)
            {
                if (landform.Type == "greater")
                {
                    if (!(weightedIndices.TryGetValue(landform.Code, out WeightedIndex index) && index.Weight > landform.Value))
                    {
                        isSuitable = false;
                    }
                }
                else if (landform.Type == "less")
                {
                    if (weightedIndices.TryGetValue(landform.Code, out WeightedIndex index) && index.Weight > landform.Value)
                    {
                        isSuitable = false;
                    }
                }
            }

            return isSuitable;
        }

        public long MapRegionIndex2D(int regionX, int regionZ)
        {
            return ((long)regionZ) * (sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize) + regionX;
        }

        private void onChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            int regionx = (request.ChunkX * chunksize) / regionSize;
            int regionz = (request.ChunkZ * chunksize) / regionSize;

            int posX = request.ChunkX * chunksize;
            int posZ = request.ChunkZ * chunksize;

            Cuboidi cuboid = new Cuboidi(posX, 0, posZ, posX + chunksize, sapi.World.BlockAccessor.MapSizeY, posZ + chunksize);
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
                        if (!tiledDungeonsSys.Tcfg.DungeonsByCode.TryGetValue(placetask.Code, out var dungeon)) return; // Ignore dungeons that no longer exist in assets

                        // check stairs gen to surface only for first "main" room
                        var tileTask = placetask.TilePlaceTasks[0];
                        baseRoom.Set(tileTask.Pos, tileTask.Pos.AddCopy(tileTask.SizeX, tileTask.SizeY, tileTask.SizeZ));
                        if (cuboid.IntersectsOrTouches(baseRoom) && placetask.StairsIndex >= 0)
                        {
                            // gen stairway
                            var height = sapi.World.BlockAccessor.GetTerrainMapheightAt(tileTask.Pos);

                            var stairs = dungeon.Stairs[placetask.StairsIndex];
                            var num = (height - tileTask.Pos.Y) / stairs.SizeY;
                            var stairPos = tileTask.Pos.AddCopy(tileTask.SizeX/2-stairs.SizeX/2, tileTask.SizeY, tileTask.SizeZ/2-stairs.SizeZ/2);
                            for (var i = 0; i < num; i++)
                            {
                                stairs.Place(worldgenBlockAccessor, sapi.World, stairPos);
                                stairPos.Y += stairs.SizeY;
                            }

                            var location = new Cuboidi(tileTask.Pos.AddCopy(0, tileTask.SizeY, 0), stairPos.AddCopy(stairs.SizeX,0,stairs.SizeZ));
                            region.AddGeneratedStructure(new GeneratedStructure()
                            {
                                Code = "dungeon/"+dungeon.Code + "/"+ stairs.FromFileName,
                                Group = placetask.Code,
                                Location = location,
                                SuppressTreesAndShrubs = true,
                                SuppressRivulets = true
                            });


                            if (dungeon.BuildProtected)
                            {
                                sapi.World.Claims.Add(new LandClaim()
                                {
                                    Areas = new List<Cuboidi>() { location.Clone() },
                                    Description = dungeon.BuildProtectionDesc,
                                    ProtectionLevel = 10,
                                    LastKnownOwnerName = dungeon.BuildProtectionName,
                                    AllowUseEveryone = true
                                });
                            }
                        }
                        generateDungeonPartial(region, placetask, request.Chunks, request.ChunkX, request.ChunkZ);
                    }
                }
            }


        }

        private void generateDungeonPartial(IMapRegion region, DungeonPlaceTask dungeonPlaceTask, IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            if (!tiledDungeonsSys.Tcfg.DungeonsByCode.TryGetValue(dungeonPlaceTask.Code, out var dungeon)) return; // Ignore dungeons that no longer exist in assets
            var rand2 = new LCGRandom(sapi.WorldManager.Seed ^ 8991827198);

            int size = sapi.WorldManager.RegionSize;
            rand2.InitPositionSeed((chunkX / size) * size, (chunkZ / size) * size);

            foreach (var placeTask in dungeonPlaceTask.TilePlaceTasks)
            {
                if (!dungeon.TilesByCode.TryGetValue(placeTask.TileCode, out var tile)) continue; // Ignore dungeon tiles that no longer exist in assets

                var rndval = rand2.NextInt(tile.ResolvedSchematics.Length);
                var schematic = tile.ResolvedSchematics[rndval][placeTask.Rotation];
                schematic.PlacePartial(chunks, worldgenBlockAccessor, sapi.World, chunkX, chunkZ, placeTask.Pos, EnumReplaceMode.ReplaceAll, null, true, true);

                string code = "dungeon/" + tile.Code + "/" + schematic.FromFileName;

                var location = new Cuboidi(placeTask.Pos, placeTask.Pos.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ));
                region.AddGeneratedStructure(new GeneratedStructure()
                {
                    Code = code,
                    Group = dungeonPlaceTask.Code,
                    Location = location,
                    SuppressTreesAndShrubs = true,
                    SuppressRivulets = true
                });

                if (dungeon.BuildProtected)
                {
                    sapi.World.Claims.Add(new LandClaim()
                    {
                        Areas = new List<Cuboidi>() { location.Clone() },
                        Description = dungeon.BuildProtectionDesc,
                        ProtectionLevel = 10,
                        LastKnownOwnerName = dungeon.BuildProtectionName,
                        AllowUseEveryone = true
                    });
                }
            }
        }
    }
}
