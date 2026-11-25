using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Common.Collectible.Block;
using Vintagestory.Datastructures;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    [ProtoContract]
    public class DungeonLocation : IStructureLocation, IWorldGenArea
    {
        [ProtoMember(1)]
        public string Code { get; } = null!;

        [ProtoMember(2)]
        public Cuboidi Location { get; } = null!;

        [ProtoMember(3)]
        public BlockPos Position = null!;

        public BlockPos CenterPos => Location.Center.AsBlockPos;

        public int LandformRadius { get; }
        public int GenerationRadius { get; }
        public Dictionary<int, int> SkipGenerationFlags { get; } = null!;
        public int MaxSkipGenerationRadiusSq { get; }

        public DungeonLocation()
        {
            // needed for ProtoBuf serialization
        }

        public DungeonLocation(BlockPos position, string code, Cuboidi location)
        {
            Position = position;
            Code = code;
            Location = location;
        }
    }

    public class DungeonProvider : IStructureProvider
    {
        public List<DungeonLocation> Dungeons = new List<DungeonLocation>();
        public Dictionary<string, Dictionary<int,int>> DungeonSkipGenerationFlags = new ();
        public Dictionary<long, List<DungeonPlaceTask>> dungeonPlaceTasksByRegion = new Dictionary<long, List<DungeonPlaceTask>>();

        private int regionSize, mapSizeX;
        public void Init(TiledDungeonConfig tcfg, ICoreServerAPI sapi)
        {
            regionSize = sapi.World.BlockAccessor.RegionSize;
            mapSizeX = sapi.World.BlockAccessor.MapSizeX;
            DungeonSkipGenerationFlags.Clear();
            foreach (var dungeon in tcfg.MaybeDungeons)
            {
                if (dungeon.SkipGenerationFlags != null)
                {
                    DungeonSkipGenerationFlags.Add(dungeon.Code, dungeon.SkipGenerationFlags);
                }
            }
        }

        public void LoadFromSavegame(ISaveGame saveGame)
        {
            var dungeons = saveGame.GetData<List<DungeonLocation>>("dungeonLocations");
            if (dungeons != null)
            {
                Dungeons.AddRange(dungeons);
            }
        }

        public long MapRegionIndex2D(int regionX, int regionZ)
        {
            return ((long)regionZ) * (mapSizeX / regionSize) + regionX;
        }

        public IStructureLocation? GetBlockingStructureAt(int x, int z, int category)
        {
            var regionIndex = MapRegionIndex2D(x/regionSize, z/regionSize);
            if (!dungeonPlaceTasksByRegion.TryGetValue(regionIndex, out var list))
            {
                return null;
            }
            foreach (var loc in list)
            {
                if (DungeonSkipGenerationFlags.TryGetValue(loc.Code, out var flags))
                {
                    if(!flags.TryGetValue(category, out var checkRadius)) continue;

                    if (checkRadius == 0 && loc.Location.Contains(x, z)) return loc;

                    var distSq = loc.DungeonBoundaries.FastCenter.HorDistanceSqTo(x, z);

                    if (checkRadius > 0 && distSq < checkRadius * checkRadius)
                    {
                        return loc;
                    }
                }
            }

            return null;
        }

        public bool PreventPlacementAt(int x, int z, int category)
        {
            return GetBlockingStructureAt(x, z, category) != null;
        }
    }

    public class GenDungeons : ModStdWorldGen
    {
        private ModSystemTiledDungeons tiledDungeonsSys = null!;
        private IWorldGenBlockAccessor worldgenBlockAccessor = null!;
        private ICoreServerAPI sapi = null!;
        private LCGRandom rand = null!;

        private int regionSize;
        private BlockPos spawnPos = null!;
        private GenStoryStructures storyStructSys = null!;
        private GenBlockLayers genBlockLayers = null!;
        private LCGRandom grassRand = null!;
        public DungeonProvider DungeonProvider = new DungeonProvider();
        public bool DungeonsDirty = false;

        public override double ExecuteOrder() { return 0.12; }

        public override void StartServerSide(ICoreServerAPI api)
        {
            var genDungeons = api.World.Config.GetAsString("loreContent", "true").ToBool(true);
            if (!genDungeons) return;

            sapi = api;
            base.StartServerSide(api);

            tiledDungeonsSys = api.ModLoader.GetModSystem<ModSystemTiledDungeons>();

            if (TerraGenConfig.DoDecorationPass)
            {
                api.ModLoader.GetModSystem<ModSystemWorldGenStructures>()?.RegisterProvider(DungeonProvider);

                api.Event.MapRegionGeneration(onMapRegionGen, "standard");
                api.Event.ChunkColumnGeneration(onChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

                api.Event.SaveGameLoaded += Event_SaveGameLoaded;
                api.Event.GameWorldSave += Event_GameWorldSave;
                api.Event.MapRegionUnloaded += Event_MapRegionUnloaded;
                api.Event.MapRegionLoaded += Event_MapRegionLoaded;
                api.Event.InitWorldGenerator(OnInitWorldGenerator, "standard");
                genBlockLayers = api.ModLoader.GetModSystem<GenBlockLayers>();

                sapi.ChatCommands.GetOrCreate("wgen")
                    .BeginSubCommand("dungeon")
                        .BeginSubCommand("addwaypoints").WithAlias("addway")
                            .RequiresPlayer()
                            .WithDescription("Add all generated dungeons as waypoints to the map")
                            .HandleWith(OnAddDungeonWaypoints)
                        .EndSubCommand()
                        .BeginSubCommand("deletewaypoints").WithAlias("delway")
                            .RequiresPlayer()
                            .WithDescription("Remove all generated dungeon waypoints from the map")
                            .HandleWith(OnDeleteDungeonWaypoints)
                        .EndSubCommand()
                    .EndSubCommand();
            }
        }

        private TextCommandResult OnDeleteDungeonWaypoints(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            var wml = sapi.ModLoader.GetModSystem<WorldMapManager>()?.MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;

            if (wml == null) return TextCommandResult.Error("Could not find waypoint map layer");

            var ownWaypoints = wml.Waypoints.Where(w => w.OwningPlayerUid == player.PlayerUID).ToList();
            var count = 0;
            foreach (var dungeon in DungeonProvider.Dungeons)
            {
                var w = ownWaypoints.FirstOrDefault(w =>
                    w.OwningPlayerUid == player.PlayerUID &&
                    w.Position.Equals(dungeon.Position.ToVec3d()));
                if (w != null)
                {
                    if (wml.Waypoints.Remove(w))
                    {
                        count++;
                    }
                }
            }

            wml.ResendWaypoints(player);
            return TextCommandResult.Success($"Removed {count} dungeons waypoints from the map.");
        }

        private TextCommandResult OnAddDungeonWaypoints(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;
            var wml = sapi.ModLoader.GetModSystem<WorldMapManager>()?.MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;

            if (wml == null) return TextCommandResult.Error("Could not find waypoint map layer");
            var ownWaypoints = wml.Waypoints.Where(w => w.OwningPlayerUid == player.PlayerUID).ToList();

            foreach (var dungeon in DungeonProvider.Dungeons)
            {
                if (!ownWaypoints.Any(w =>
                        w.OwningPlayerUid == player.PlayerUID &&
                        w.Position.Equals(dungeon.Position.ToVec3d())))
                {
                    wml.AddWaypoint(new Waypoint
                    {
                        Color = ColorUtil.ColorFromRgba(43, 173, 251, 255),
                        Icon = "ruin-undergroundlarge",
                        Pinned = false,
                        Position = dungeon.Position.ToVec3d(),
                        OwningPlayerUid = player.PlayerUID,
                        Title = "",
                    }, player);
                }
            }

            wml.ResendWaypoints(player);
            return TextCommandResult.Success($"Added {DungeonProvider.Dungeons.Count} dungeons to the map.");
        }

        private void OnInitWorldGenerator()
        {
            tiledDungeonsSys.Init();
            DungeonProvider.Init(tiledDungeonsSys.Tcfg, sapi);
            var df = sapi.WorldManager.SaveGame.DefaultSpawn;
            if (df != null)
            {
                spawnPos = new BlockPos(df.x, df.y ?? 0, df.z);
            } else
            {
                spawnPos = sapi.World.BlockAccessor.MapSize.AsBlockPos / 2;
            }

            storyStructSys = sapi.ModLoader.GetModSystem<GenStoryStructures>();
            grassRand = new LCGRandom(sapi.WorldManager.SeedWorldgen);
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
            rand = new LCGRandom(sapi.WorldManager.SeedWorldgen ^ 8991827198);
            regionSize = sapi.World.BlockAccessor.RegionSize;
        }

        private void Event_GameWorldSave()
        {
            if (DungeonsDirty)
            {
                sapi.WorldManager.SaveGame.StoreData("dungeonLocations", SerializerUtil.Serialize(DungeonProvider.Dungeons));
                DungeonsDirty = false;
            }
        }

        private void Event_SaveGameLoaded()
        {
            DungeonProvider.LoadFromSavegame(sapi.WorldManager.SaveGame);
        }

        private void Event_MapRegionLoaded(Vec2i mapCoord, IMapRegion region)
        {
            try
            {
                var tasks = region.GetModdata<List<DungeonPlaceTask>>("dungeonPlaceTasks");
                if (tasks != null)
                {
                    foreach (var placeTask in tasks)
                    {
                        if(placeTask.AttributesBytes != null)
                        {
                            var reader = new BinarySpanReader(placeTask.AttributesBytes);
                            placeTask.Attributes = new TreeAttribute();
                            placeTask.Attributes.FromBytes(ref reader);
                        }
                    }
                    DungeonProvider.dungeonPlaceTasksByRegion[MapRegionIndex2D(mapCoord.X, mapCoord.Y)] = tasks;
                }
            }
            catch
            {

            }
        }

        private void Event_MapRegionUnloaded(Vec2i mapCoord, IMapRegion region)
        {
            if (DungeonProvider.dungeonPlaceTasksByRegion.TryGetValue(MapRegionIndex2D(mapCoord.X, mapCoord.Y), out var list) && list.Any(d => d.IsDirty))
            {
                var ms = new FastMemoryStream();
                var writer = new BinaryWriter(ms);
                foreach (var placeTask in list.Where(placeTask => placeTask.IsDirty))
                {
                    ms.Reset();
                    if (placeTask.Attributes?.Count > 0)
                    {
                        placeTask.Attributes.ToBytes(writer);
                        placeTask.AttributesBytes = ms.ToArray();
                    }
                }
                region.SetModdata("dungeonPlaceTasks", list);
            }
        }

        private void onMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute? chunkGenParams = null)
        {
            int size = sapi.WorldManager.RegionSize;
            var mapRand = new LCGRandom(sapi.WorldManager.SeedWorldgen ^ 8991827198);
            mapRand.InitPositionSeed(regionX * size, regionZ * size);

            long mapRegionIndex = MapRegionIndex2D(regionX, regionZ);
            if (DungeonProvider.dungeonPlaceTasksByRegion.ContainsKey(mapRegionIndex))
            {
                DungeonProvider.Dungeons.RemoveAll(d => d.Position.X / regionSize == regionX && d.Position.Z / regionSize == regionZ);
            }

            DungeonProvider.dungeonPlaceTasksByRegion[mapRegionIndex] = new List<DungeonPlaceTask>();

            var dungeons = tiledDungeonsSys.Tcfg.MaybeDungeons.Where(d => d.Worldgen).ToList();

            var dungeIndices = new int[dungeons.Count];
            for (int l = 0; l < dungeIndices.Length; l++) dungeIndices[l] = l;

            var seaLevel = sapi.World.SeaLevel;
            for (int i = 0; i < 12; i++)
            {
                int posx = regionX * size + mapRand.NextInt(size);
                int posz = regionZ * size + mapRand.NextInt(size);

                if(!SatisfiesMinDistance(posx, seaLevel, posz)) continue;

                if(storyStructSys.Structures.values.Values.Any(storyLoc =>
                       storyLoc.CenterPos.HorDistanceSqTo(posx, posz) < tiledDungeonsSys.Tcfg.StoryStructMinDistanceSq)) continue;

                var dungeon = pickDungeon(dungeIndices, mapRand, dungeons);

                if (spawnPos.HorDistanceSqTo(posx, posz) < dungeon.MinSpawnDistance * dungeon.MinSpawnDistance) continue;

                if (IsInOcean(mapRegion, posx, posz)) continue;

                // Make sure init() is called
                if (!tiledDungeonsSys.Tcfg.TryGetDungeon(dungeon.Code, out dungeon)) continue;

                if (!HasSuitableLandforms(dungeon, mapRegion, posx, posz)) continue;


                int posy = seaLevel-dungeon.MaxDepth + mapRand.NextInt(dungeon.MaxDepth-dungeon.MinDepth);
                var dungeonStartPos = new BlockPos(posx, posy, posz);
                var didGen = false;
                for (int j = 0; j < 5; j++)
                {
                    var placeTask = tiledDungeonsSys.TryPregenerateTiledDungeon(mapRand, dungeon, mapRegion.GeneratedStructures, dungeonStartPos, dungeon.MinTiles, dungeon.MaxTiles);

                    if (placeTask != null)
                    {
                        placeTask.IsDirty = true;
                        DungeonProvider.dungeonPlaceTasksByRegion[mapRegionIndex].Add(placeTask);
                        DungeonProvider.Dungeons.Add(new DungeonLocation(dungeonStartPos, placeTask.Code, placeTask.DungeonBoundaries));
                        DungeonsDirty = true;
                        mapRegion.AddGeneratedStructures(placeTask.GeneratedStructures);
                        didGen = true;
                        break;
                    }
                }

                if (didGen) break;
            }
        }

        private static TiledDungeon pickDungeon(int[] dungeIndices, LCGRandom mapRand, List<TiledDungeon> dungeons)
        {
            TiledDungeon dungeon = null!;
            dungeIndices.Shuffle(mapRand);
            var rndval = (float)mapRand.NextDouble() * dungeons.Sum(d=>d.chance);
            for (var k = 0; k < dungeons.Count; k++)
            {
                dungeon = dungeons[dungeIndices[k]];
                rndval -= dungeon.chance;

                if (rndval > 0)
                {
                    continue;
                }
                return dungeon;
            }

            return dungeon;
        }

        private bool SatisfiesMinDistance(int posx, int posy, int posz)
        {
            bool satisfiesDistance = true;
            foreach (var pos in DungeonProvider.Dungeons)
            {
                if (pos.Position.DistanceSqTo(posx, posy, posz) < tiledDungeonsSys.Tcfg.MinDistanceSq)
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
                var rlX = (posx / chunksize) % regionChunkSize;
                var rlZ = (posz / chunksize) % regionChunkSize;
                var oFac = (float)mapRegion.OceanMap.InnerSize / regionChunkSize;
                var oceanUpLeft = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac), (int)(rlZ * oFac));
                var oceanUpRight = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac + oFac), (int)(rlZ * oFac));
                var oceanBotLeft = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac), (int)(rlZ * oFac + oFac));
                var oceanBotRight = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac + oFac), (int)(rlZ * oFac + oFac));
                float oceanicity = GameMath.BiLerp(oceanUpLeft, oceanUpRight, oceanBotLeft, oceanBotRight, (float)(posx % chunksize) / chunksize, (float)(posz % chunksize) / chunksize);
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

            var landforms = NoiseLandforms.landforms.LandFormsByIndex;
            var landformMap = mapRegion.LandformMap;
            var map = new LerpedWeightedIndex2DMap(landformMap.Data, mapRegion.LandformMap.Size, TerraGenConfig.landFormSmoothingRadius, landformMap.TopLeftPadding, landformMap.BottomRightPadding);

            bool valid = true;

            var sizeX = dungeon.Surface[0].SizeX;
            var sizeZ = dungeon.Surface[0].SizeZ;
            // check the surface on 4 edges (this won't be exactly at the surface room edges but should be good enough, because of the connector block could be of center)
            // of the surface room if those satisfy our landform constraints
            for (int x = -1; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    var tmpX = posx + x * (sizeX/2);
                    var tmpZ = posz + z * (sizeZ/2);
                    var posXInRegion = ((float)tmpX / regionSize - tmpX / regionSize) * noiseSizeLandform;
                    var posZInRegion = ((float)tmpZ / regionSize - tmpZ / regionSize) * noiseSizeLandform;
                    valid &= HasSuitableLandforms(dungeon, map, posXInRegion, posZInRegion, landforms);
                }
            }

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
            return ((long)regionZ) * (sapi.WorldManager.MapSizeX / regionSize) + regionX;
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
                    if (!DungeonProvider.dungeonPlaceTasksByRegion.TryGetValue(mapRegionIndex2D, out var dungePlaceTasks)) continue;

                    var data = new SchematicPlacementData(worldgenBlockAccessor, sapi.World);
                    data.Set(request.Chunks, request.ChunkX, request.ChunkZ, EnumReplaceMode.ReplaceAll, null, GlobalConfig.ReplaceMetaBlocks);

                    if (dungePlaceTasks.Count > 0)
                    {
                        data.Attributes = new TreeAttribute();
                        // the key ID is the start pos of the first tile we generated in TryPregenerateTiledDungeon and is set via SetKeyAttributes
                        data.Attributes.SetString("keyId", $"{dungePlaceTasks[0].DungeonBoundaries.X1},{dungePlaceTasks[0].DungeonBoundaries.Y1},{dungePlaceTasks[0].DungeonBoundaries.Z1}");
                    }

                    foreach (var placeTask in dungePlaceTasks)
                    {
                        if (!placeTask.DungeonBoundaries.Intersects(cuboid)) continue;
                        if (!tiledDungeonsSys.Tcfg.TryGetDungeon(placeTask.Code, out var dungeon)) return; // Ignore dungeons that no longer exist in assets

                        Block? rockBlock = null;
                        // get the rockblock for the first generating chunk and reuse that for each room
                        if (placeTask.RockBlockCode == null)
                        {
                            var posY = placeTask.TilePlaceTasks[0].Pos.Y;
                            for (var j = 0; rockBlock == null && j < 10; j++)
                            {
                                var block = worldgenBlockAccessor.GetBlockRaw(request.ChunkX * chunksize + 15, posY, request.ChunkZ * chunksize + 15, BlockLayersAccess.Solid);

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
                        data.RockBlock = rockBlock;

                        generateDungeonPartial(dungeon, placeTask, data);

                        // pregenrate the surface connection and then store it
                        // this needs to be done here since now we have the final terrain height
                        if (preGenDungeonStairs(placeTask, dungeon, region)) continue;

                        generateDungeonSurfacePartial(dungeon, placeTask, request, data);
                    }
                }
            }
        }

        private bool preGenDungeonStairs(DungeonPlaceTask placeTask, TiledDungeon dungeon, IMapRegion region)
        {
            if (placeTask.SurfacePlaceTasks != null || dungeon.StairCase == null || placeTask.StairsConnector == null)
            {
                return false;
            }

            ConnectorMetaData connector = placeTask.StairsConnector.Value;
            var pos = new BlockPos(connector.Position.X, connector.Position.Y, connector.Position.Z);
            var height = worldgenBlockAccessor.GetTerrainMapheightAt(pos);
            if (height == 0) return true;
            // skip if we get our of range where we can generate, should not happen if the stairs and surface rooms are not larger than 2 chunks
            height += dungeon.surfaceYOffset;

            rand.InitPositionSeed(connector.Position.X, connector.Position.Y, connector.Position.Z);
            var tileIndices = new int[dungeon.StairCase.Tiles.Count];
            for (int i = 0; i < tileIndices.Length; i++) tileIndices[i] = i;

            var dgd = new DungeonGenWorkspace(dungeon.StairCase, 0, 0, [], [], [], [], null);

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

                if (placeTask.DungeonBoundaries.Intersects(location))
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
                if (placeTask.DungeonBoundaries.Intersects(location))
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
            return false;
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

        private void generateDungeonSurfacePartial(TiledDungeon dungeon, DungeonPlaceTask dungeonPlaceTask, IChunkColumnGenerateRequest request, SchematicPlacementData data)
        {
            if (dungeonPlaceTask.SurfacePlaceTasks == null) return;

            foreach (var placeTask in dungeonPlaceTask.SurfacePlaceTasks)
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

                data.Set(placeTask.Pos, dungeon.resolvedRockTypeRemaps, dungeon.replacewithblocklayersBlockids);

                var placed = schematic.PlacePartial(data);
                if (placed > 0)
                {
                    UpdateHeightmap(request, worldgenBlockAccessor);
                    GenerateGrass(sapi, request, grassRand, genBlockLayers);
                    dungeonPlaceTask.IsDirty |= data.AttributesDirty;
                }

                if (data.ChunkX == placeTask.Pos.X / chunksize && data.ChunkZ == placeTask.Pos.Z / chunksize)
                {
                    var location = new Cuboidi(placeTask.Pos, placeTask.Pos.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ));
                    data.Chunks[0].MapChunk.MapRegion.AddGeneratedStructure(new GeneratedStructure()
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
        }

        private void generateDungeonPartial(TiledDungeon dungeon, DungeonPlaceTask dungeonPlaceTask, SchematicPlacementData data)
        {
            data.Attributes = dungeonPlaceTask.Attributes;
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

                data.Set(placeTask.Pos, dungeon.resolvedRockTypeRemaps, dungeon.replacewithblocklayersBlockids);

                var placed = schematic.PlacePartial(data);
                if (placed > 0)
                {
                    dungeonPlaceTask.IsDirty |= data.AttributesDirty;
                }

                if (data.ChunkX == placeTask.Pos.X / chunksize && data.ChunkZ == placeTask.Pos.Z / chunksize)
                {
                    var location = new Cuboidi(placeTask.Pos, placeTask.Pos.AddCopy(schematic.SizeX, schematic.SizeY, schematic.SizeZ));
                    data.Chunks[0].MapChunk.MapRegion.AddGeneratedStructure(new GeneratedStructure()
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
}
