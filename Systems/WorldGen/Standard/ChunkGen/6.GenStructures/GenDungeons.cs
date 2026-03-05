using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Common.Collectible.Block;
using Vintagestory.ServerMods.NoObf;

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
            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.MapRegionGeneration(onMapRegionGen, "standard");
                api.Event.ChunkColumnGeneration(onChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

                api.Event.MapRegionUnloaded += Event_MapRegionUnloaded;
                api.Event.MapRegionLoaded += Event_MapRegionLoaded;
                api.Event.InitWorldGenerator(OnInitWorldGenerator, "standard");
            }
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
            if (dungeonPlaceTasksByRegion.TryGetValue(MapRegionIndex2D(mapCoord.X, mapCoord.Y), out var list) && list.Any(d => d.IsDirty))
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
                int posy = seaLevel-40 + rand2.NextInt(20);

                if(!SatisfiesMinDistance(mapRegionIndex, posx, posy, posz)) continue;

                if (IsInOcean(mapRegion, posx, posz)) continue;

                var dungeons = tiledDungeonsSys.Tcfg.Dungeons.Where(d => d.Worldgen).ToList();
                var dungeon = dungeons[rand2.NextInt(dungeons.Count)];

                if (!HasSuitableLandforms(dungeon, mapRegion, posx, posz))
                {
                    continue;
                }

                var dungeonStartPos = new BlockPos(posx, posy, posz);
                var didGen = false;
                for (int j = 0; j < 5; j++)
                {
                    var placeTask = tiledDungeonsSys.TryPregenerateTiledDungeon(rand2, dungeon, mapRegion.GeneratedStructures, dungeonStartPos, dungeon.MinTiles, dungeon.MaxTiles);

                    if (placeTask != null)
                    {
                        sapi.Logger.Debug($"Dungeon @: /tp ={posx} {posy} ={posz}");
                        placeTask.IsDirty = true;
                        dungeonPlaceTasksByRegion[mapRegionIndex].Add(placeTask);
                        mapRegion.AddGeneratedStructures(placeTask.GeneratedStructures);
                        didGen = true;
                        break;
                    }
                }

                if (didGen) break;
            }
        }

        private bool SatisfiesMinDistance(long mapRegionIndex, int posx, int posy, int posz)
        {
            bool satisfiesDistance = true;
            foreach (var regionPlaceTask in dungeonPlaceTasksByRegion[mapRegionIndex])
            {
                if (regionPlaceTask.DungeonBoundaries.FastCenter.DistanceSq(posx, posy, posz) < tiledDungeonsSys.Tcfg.MinDistanceSq)
                {
                    satisfiesDistance = false;
                    break;
                }
            }

            return satisfiesDistance;
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

            bool valid = true;

            valid &= HasSuitableLandforms(dungeon, map, posXInRegion, posZInRegion, landforms);
            return valid;
        }

        private static bool HasSuitableLandforms(TiledDungeon dungeon, LerpedWeightedIndex2DMap map, float posXInRegion, float posZInRegion,
            LandformVariant[] landforms)
        {
            var weightedIndices = new Dictionary<string, WeightedIndex>();
            foreach (var index in map[posXInRegion, posZInRegion])
            {
                weightedIndices.Add(landforms[index.Index].Code.ToString(), index);
            }

            foreach (var landform in dungeon.RequiredLandform)
            {
                if (landform.Type == "greater")
                {
                    if (!(weightedIndices.TryGetValue(landform.Code, out WeightedIndex index) && index.Weight > landform.Value))
                    {
                        return false;
                    }
                }
                else if (landform.Type == "less")
                {
                    if (weightedIndices.TryGetValue(landform.Code, out WeightedIndex index) && index.Weight > landform.Value)
                    {
                        return false;
                    }
                }
            }

            return true;
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

            IMapRegion region = request.Chunks[0].MapChunk.MapRegion;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    var mapRegionIndex2D = MapRegionIndex2D(regionx + dx, regionz + dz);
                    if (!dungeonPlaceTasksByRegion.TryGetValue(mapRegionIndex2D, out var dungePlaceTasks)) continue;

                    foreach (var placeTask in dungePlaceTasks)
                    {
                        if (!placeTask.DungeonBoundaries.Intersects(cuboid)) continue;
                        if (!tiledDungeonsSys.Tcfg.DungeonsByCode.TryGetValue(placeTask.Code, out var dungeon)) return; // Ignore dungeons that no longer exist in assets

                        Block? rockBlock = null;
                        if (placeTask.RockBlockCode == null)
                        {
                            var posY = placeTask.TilePlaceTasks[0].Pos.Y;
                            for (var j = 0; rockBlock == null && j < 10; j++)
                            {
                                var block = worldgenBlockAccessor.GetBlockRaw(placeTask.TilePlaceTasks[0].Pos.X, posY, placeTask.TilePlaceTasks[0].Pos.Z, BlockLayersAccess.Solid);

                                if (block.BlockMaterial == EnumBlockMaterial.Stone)
                                {
                                    rockBlock = block;
                                    placeTask.RockBlockCode = block.Code.ToString();
                                    placeTask.IsDirty = true;
                                    break;
                                }

                                posY -= 2;
                            }
                        }
                        else
                        {
                            rockBlock = worldgenBlockAccessor.GetBlock(placeTask.RockBlockCode);
                        }

                        generateDungeonPartial(region, dungeon, placeTask, request.Chunks, request.ChunkX, request.ChunkZ, rockBlock);

                        if (placeTask.StairsConnector == null) continue;

                        var connector = placeTask.StairsConnector.Value;
                        // restore the facing from serialization
                        connector.Facing ??= BlockFacing.ALLFACES[connector.FacingInt];

                        // pregenrate the surface connection and then store it
                        // this needs to be done here since now we have the final terrain height
                        if (placeTask.SurfacePlaceTasks == null && dungeon.StairCase != null)
                        {
                            var pos = new BlockPos(connector.Position.X, connector.Position.Y, connector.Position.Z);
                            var height = sapi.World.BlockAccessor.GetTerrainMapheightAt(pos);
                            if (height == 0) continue;
                            // skip if we get our of range where we can generate, should not happen if the stairs and surface rooms are not larger than 2 chunks

                            rand.InitPositionSeed(connector.Position.X, connector.Position.Y, connector.Position.Z);
                            var tileIndices = new int[dungeon.StairCase.Tiles.Count];
                            for (int i = 0; i < tileIndices.Length; i++) tileIndices[i] = i;

                            DungeonGenWorkspace dgd = new DungeonGenWorkspace(dungeon.StairCase, 0, 0, [], [], [], [], null);

                            var currHeight = connector.Position.Y;

                            var surfacePlaceTasks = new List<TilePlaceTask>();

                            var tries = 40;
                            ConnectorMetaData curConnector = connector;
                            BlockSchematicPartial? schematic = null;
                            DungeonTile? tile = null;
                            while (tries-- > 0 && currHeight < height && curConnector.Facing != null)
                            {
                                FastVec3i? offsetPos = null;

                                if (!pickStairTile(dgd, curConnector, tileIndices, ref schematic, ref tile, ref offsetPos)) continue;
                                if (currHeight == height) break; // exit if we reached the final height
                                if (currHeight + schematic.SizeY >= height) continue; // retry with the chance to get a smaller tile that still fits
                                currHeight += schematic.SizeY;

                                var fastPos = curConnector.Position.AddCopy(curConnector.Facing).Sub(offsetPos.Value);
                                var startPos = new BlockPos(fastPos.X, fastPos.Y, fastPos.Z);

                                surfacePlaceTasks.Add(new TilePlaceTask()
                                {
                                    Rotation = connector.Rotation,
                                    Pos = startPos,
                                    FileName = schematic.FromFile,
                                    TileCode = tile.Code,
                                    SizeX = schematic.SizeX,
                                    SizeY = schematic.SizeY,
                                    SizeZ = schematic.SizeZ,
                                });

                                curConnector = schematic.Connectors.First(p => !p.ConnectsTo(curConnector)).Clone().Offset(fastPos);
                                var location = new Cuboidi(startPos, startPos.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ));

                                if (cuboid.Intersects(location))
                                {
                                    region.AddGeneratedStructure(new GeneratedStructure()
                                    {
                                        Code = schematic.FromFile.GetNameWithDomain(),
                                        Group = "stairs",
                                        Location = location,
                                        SuppressTreesAndShrubs = true,
                                        SuppressRivulets = true
                                    });
                                }
                            }

                            if (curConnector.Facing != null && dungeon.Surface != null)
                            {
                                schematic = dungeon.Surface[connector.Rotation];
                                var offset = schematic.Connectors.First(p => p.ConnectsTo(curConnector)).Position;
                                var fastPos = curConnector.Position.AddCopy(curConnector.Facing).Sub(offset);
                                var startPos = new BlockPos(fastPos.X, fastPos.Y, fastPos.Z);

                                surfacePlaceTasks.Add(new TilePlaceTask()
                                {
                                    Rotation = connector.Rotation,
                                    Pos = startPos,
                                    FileName = schematic.FromFile,
                                    TileCode = "surface",
                                    SizeX = schematic.SizeX,
                                    SizeY = schematic.SizeY,
                                    SizeZ = schematic.SizeZ,
                                });
                                var location = new Cuboidi(startPos, startPos.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ));
                                if (cuboid.Intersects(location))
                                {
                                    region.AddGeneratedStructure(new GeneratedStructure()
                                    {
                                        Code = schematic.FromFile.GetNameWithDomain(),
                                        Group = "stairs",
                                        Location = location,
                                        SuppressTreesAndShrubs = true,
                                        SuppressRivulets = true
                                    });
                                }
                            }

                            placeTask.SurfacePlaceTasks = surfacePlaceTasks;
                            placeTask.IsDirty = true;
                        }

                        if (placeTask.SurfacePlaceTasks != null)
                        {
                            generateDungeonSurfacePartial(region, dungeon, placeTask.SurfacePlaceTasks, request.Chunks, request.ChunkX, request.ChunkZ, rockBlock);
                        }
                    }
                }
            }
        }


        private bool pickStairTile(DungeonGenWorkspace dgd, ConnectorMetaData connector, int[] tileIndices, [NotNullWhen(true)] ref BlockSchematicPartial? schematic, [NotNullWhen(true)] ref DungeonTile? tile, [NotNullWhen(true)] ref FastVec3i? offsetPos)
        {
            tile = tiledDungeonsSys.dungeonGen.pickTile(dgd, tileIndices, rand, connector);
            if (tile != null)
            {
                tile.FreshShuffleIndices(rand);
                int len = tile.ResolvedSchematics.Length;
                for (var k = 0; k < len; k++)
                {
                    var schematicByRot = tile.ResolvedSchematics[tile.ShuffledIndices[k]];

                    int startRot = connector.Rotation;
                    if (connector.Facing.IsHorizontal)
                    {
                        startRot = rand.NextInt(4);
                    }

                    // Try any of the 4 sides and see which one can connect
                    for (var i = 0; i < 4; i++)
                    {
                        var rot = (startRot + i) % 4;
                        var path = schematicByRot[rot].Connectors.FirstOrDefault(p => p.ConnectsTo(connector));
                        if (path.Valid)
                        {
                            offsetPos = path.Position;
                            schematic = schematicByRot[rot];
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        private void generateDungeonSurfacePartial(IMapRegion region, TiledDungeon dungeon, List<TilePlaceTask> placeTasks, IServerChunk[] chunks, int chunkX, int chunkZ, Block? rockBlock)
        {
            foreach (var placeTask in placeTasks)
            {
                BlockSchematicPartial? schematic = null;
                if (placeTask.TileCode == null || !dungeon.StairCase.TilesByCode.TryGetValue(placeTask.TileCode, out var tile))
                {
                    if (dungeon.Surface[placeTask.Rotation].FromFile == placeTask.FileName)
                    {
                        schematic = dungeon.Surface[placeTask.Rotation];
                    }
                }
                else
                {
                    int roomIndex = -1;
                    for (var index = 0; index < tile.ResolvedSchematics.Length; index++)
                    {
                        var rooms = tile.ResolvedSchematics[index];
                        if (rooms[0].FromFile != placeTask.FileName) continue;

                        roomIndex = index;
                        break;
                    }

                    if (roomIndex == -1)
                    {
                        sapi.Logger.Warning($"Failed to find room: {placeTask.FileName}, maybe outdated? Will not generate");
                        continue;
                    }

                    schematic = tile.ResolvedSchematics[roomIndex][placeTask.Rotation];
                }

                if (schematic == null) continue;
                schematic.PlacePartial(chunks, worldgenBlockAccessor, sapi.World, chunkX, chunkZ, placeTask.Pos, EnumReplaceMode.ReplaceAll, null, GlobalConfig.ReplaceMetaBlocks, true, dungeon.resolvedRockTypeRemaps, null, rockBlock);

                var location = new Cuboidi(placeTask.Pos, placeTask.Pos.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ));
                region.AddGeneratedStructure(new GeneratedStructure()
                {
                    Code = schematic.FromFile.GetNameWithDomain(),
                    Group = dungeon.Code,
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

        private void generateDungeonPartial(IMapRegion region, TiledDungeon dungeon, DungeonPlaceTask dungeonPlaceTask, IServerChunk[] chunks, int chunkX, int chunkZ, Block? rockBlock)
        {
            foreach (var placeTask in dungeonPlaceTask.TilePlaceTasks)
            {
                if (!dungeon.TilesByCode.TryGetValue(placeTask.TileCode, out var tile)) continue; // Ignore dungeon tiles that no longer exist in assets

                int roomIndex = -1;
                for (var index = 0; index < tile.ResolvedSchematics.Length; index++)
                {
                    var rooms = tile.ResolvedSchematics[index];
                    if (rooms[0].FromFile != placeTask.FileName) continue;

                    roomIndex = index;
                    break;
                }

                if (roomIndex == -1)
                {
                    sapi.Logger.Warning($"Failed to find room: {placeTask.FileName}, maybe outdated? Will not generate");
                    continue;
                }

                var schematic = tile.ResolvedSchematics[roomIndex][placeTask.Rotation];
                schematic.PlacePartial(chunks, worldgenBlockAccessor, sapi.World, chunkX, chunkZ, placeTask.Pos, EnumReplaceMode.ReplaceAll, null, GlobalConfig.ReplaceMetaBlocks, true, dungeon.resolvedRockTypeRemaps, null, rockBlock);

                var location = new Cuboidi(placeTask.Pos, placeTask.Pos.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ));
                region.AddGeneratedStructure(new GeneratedStructure()
                {
                    Code = schematic.FromFile.GetNameWithDomain(),
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
