using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Common.Collectible.Block;
using Vintagestory.Datastructures;
using Vintagestory.GameContent;

namespace Vintagestory.ServerMods
{
    public class ModSystemTiledDungeons : ModSystem
    {
        protected ICoreServerAPI sapi = null!;
        public TiledDungeonConfig Tcfg = null!;

        internal DungeonGenerator dungeonGen = null!;
        TextCommandCallingArgs? dungeonGenRequest;

        int debugStopStep = -1;
        private bool placeDebugConnectors;

        bool debugLogging = false;

        public override void StartServerSide(ICoreServerAPI api)
        {
            // Yellow = Tile boundaries, "Sort of"
            // Red = Walls
            // Green = Pathways
            // Purpose = Connector meta block
            this.sapi = api;

            dungeonGen = new DungeonGenerator(api.World.Logger, api.World.BlockAccessor.MapSize);
            api.Event.RegisterEventBusListener(onEvent, 0, "wgenregendone");

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands
                .GetOrCreate("debug")
                .BeginSub("dungeon").WithAlias("d")
                    .BeginSub("debug").WithAlias("d")
                        .WithDesc("Toggle debug logging on/off for dungeon commands")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.OptionalBool("mode"))
                        .HandleWith((args) => { debugLogging = (bool)args[0]; return TextCommandResult.Success("Debug logging now " + (debugLogging ? "on" : "off")); })
                    .EndSub()
                    .BeginSub("generate").WithAlias("g")
                        .WithDesc("Generate a dungeon at the callers/specified position")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.Word("tiled dungeon code"), parsers.Int("amount of tiles"), parsers.OptionalWorldPosition("pos"), parsers.OptionalWord("randomSeed"))
                        .HandleWith(OnCmdGenDeungon)
                    .EndSub()
                    .BeginSub("stepped").WithAlias("st")
                        .WithDesc("Start a dungeon stepped placement at the callers/specified position")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.Word("tiled dungeon code"), parsers.Int("amount of tiles"), parsers.OptionalWorldPosition("pos"), parsers.OptionalWord("randomSeed"))
                        .HandleWith(OnCmdGenDeungonStepped)
                    .EndSub()
                    .BeginSub("step")
                        .WithDesc("Advance a dungeon stepped placement to the next step")
                        .RequiresPrivilege(Privilege.controlserver)
                        .HandleWith((_) => {
                            if (placeTask == null || lastDungeon == null) return TextCommandResult.Error("No dungeon to step");
                            if (placeTask.TilePlaceTasks.Count > debugStopStep) debugStopStep++;
                            placeDungeon(sapi.World.BlockAccessor, lastDungeon, debugStopStep, placeTask);
                            return TextCommandResult.Success("Stepped to " + debugStopStep + " / " + placeTask.TilePlaceTasks.Count);
                        })
                    .EndSub()
                    .BeginSub("stepreset").WithAlias("r")
                        .WithDesc("Reset a dungeon stepped placement. You will need to call /debug dungeon stepped to start a new one.")
                        .RequiresPrivilege(Privilege.controlserver)
                        .HandleWith((_) => {
                            debugStopStep = -1;
                            lastDungeon = null;
                            placeTask = null;
                            return TextCommandResult.Success();
                        })
                    .EndSub()
                    .BeginSub("cleargenerate").WithAlias("cgen")
                        .WithDesc("Clear chunks of given area radius, then generated a dungeon at the callers/specified position")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.Int("chunk clear radius"), parsers.Word("tiled dungeon code"), parsers.Int("amount of tiles"), parsers.OptionalWorldPosition("pos"), parsers.OptionalWord("randomSeed"))
                        .HandleWith(OnCmdClearAndTiledCungeonCode)
                    .EndSub()
                    .BeginSub("lineup").WithAlias("l")
                        .WithDesc("Lineup of all variations of tiles with all orientations side by side for debugging at the callers position")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.Word("tiled dungeon code"), parsers.OptionalBool("with rotation"))
                        .HandleWith(OnCmdTiledDungeonTest)
                    .EndSub()
                    .BeginSub("debugconnector").WithAlias("dc")
                        .WithDesc("Toggle debug connector placing on/off for dungeon commands")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.OptionalBool("mode"))
                        .HandleWith(OnCmdToggleDebugConnectors)
                    .EndSub()
                    .BeginSub("printloottable").WithAlias("plt")
                        .WithDesc("Print avg loot for a dungeon")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.Word("tiled dungeon code"), parsers.Int("amount of tiles"), parsers.Int("amount dungeons to gen"), parsers.OptionalWorldPosition("pos"), parsers.OptionalWord("randomSeed"))
                        .HandleWith(OnCmdPrintLoot)
                    .EndSub()
                .EndSub()
            ;
        }

        private TextCommandResult OnCmdPrintLoot(TextCommandCallingArgs args)
        {
            sapi.Assets.Reload(AssetCategory.worldgen);
            Init();

            var code = (string)args[0];
            var tiles = (int)args[1];
            var dungeon = Tcfg.Dungeons.FirstOrDefault(td => td.Code == code);

            if (dungeon == null) return TextCommandResult.Error("No such dungeon defined");

            var dungeonsToGen = (int)args[2];
            BlockPos pos;
            if (args.Parsers[3].IsMissing)
            {
                pos = args.Caller.Pos.AsBlockPos;
                pos.Y = sapi.World.BlockAccessor.GetTerrainMapheightAt(pos) + 50;
            }
            else
            {
                pos = ((Vec3d)args[3]).AsBlockPos;
            }

            int size = sapi.WorldManager.RegionSize;
            var ba = sapi.World.BlockAccessor;

            var rnd = new LCGRandom(sapi.World.Rand.NextInt64());
            rnd.InitPositionSeed(pos.X / size * size, pos.Z / size * size);
            var odds = new List<Dictionary<string, int>>();
            sapi.Logger.Notification($"Testing {dungeonsToGen} dungeons for loot");
            for (int j = 0; j < dungeonsToGen; j++)
            {
                var dungeonPlaceTask = dungeonGen.TryPregenerateTiledDungeon(rnd, dungeon, new List<GeneratedStructure>(), pos, tiles, tiles);
                if (dungeonPlaceTask == null) continue;
                var dungeonOdds = new Dictionary<string, int>();

                for (var i = 0; i < dungeonPlaceTask.TilePlaceTasks.Count; i++)
                {
                    if (!TryGetDungeonTile(dungeonPlaceTask.TilePlaceTasks[i], dungeon, out var schem))
                    {
                        sapi.Logger.Warning($"Could not find dungeon tile: {dungeonPlaceTask.TilePlaceTasks[i].TileCode} | {dungeonPlaceTask.TilePlaceTasks[i].FileName}");
                        continue;
                    }

                    for (int k = 0; k < schem.Indices.Count; k++)
                    {
                        var block = ba.GetBlock(schem.BlockIds[k]);
                        if(string.IsNullOrEmpty(block.EntityClass)) continue;

                        var blockEntityType = sapi.ClassRegistry.GetBlockEntity(block.EntityClass);
                        if (blockEntityType != typeof(BlockEntityGenericTypedContainer) && !typeof(BlockEntityGenericTypedContainer).IsAssignableFrom(blockEntityType)) continue;

                        var container = (BlockEntityGenericTypedContainer)sapi.ClassRegistry.CreateBlockEntity(block.EntityClass);
                        var dataTree = schem.DecodeBlockEntityData(schem.BlockEntities[schem.Indices[k]]);
                        if (!dataTree.HasAttribute("inventory")) continue;

                        container.InitInventory(block);
                        container.Inventory.FromTreeAttributes(dataTree.GetTreeAttribute("inventory"));
                        if (!container.Inventory.Empty)
                        {
                            container.Inventory.Api = sapi;
                            container.OnLoadCollectibleMappings(sapi.World, schem.BlockCodes, schem.ItemCodes, 1, true);
                            container.Inventory.ResolveBlocksOrItems();
                            foreach (var slot in container.Inventory)
                            {
                                if (!slot.Empty)
                                {
                                    dungeonOdds.TryGetValue(slot.Itemstack.Collectible.Code, out int count);
                                    dungeonOdds[slot.Itemstack.Collectible.Code] = count + slot.Itemstack.StackSize;
                                }
                            }
                        }
                    }
                }

                rnd.SetWorldSeed(sapi.World.Rand.NextInt64());
                odds.Add(dungeonOdds);
            }

            var sb = new StringBuilder($"Dungeon loot from {odds.Count} dungeons\n");
            var stats = DungeonLootSample.BuildStats(odds);
            DungeonLootSample.GenerateReport(stats, sb);

            sapi.Logger.Notification(sb.ToString());

            GamePaths.EnsurePathExists(GamePaths.ModConfig);
            File.WriteAllText(Path.Join(GamePaths.ModConfig,"dungeon_loot.csv"),sb.ToString());
            return TextCommandResult.Success("Results are in the log file and also in ModConfig/dungeon_loot.csv");
        }

        private bool TryGetDungeonTile(TilePlaceTask tileTask, TiledDungeon dungeon, [NotNullWhen(true)] out BlockSchematicPartial? schematic)
        {
            if (!dungeon.TilesByCode.TryGetValue(tileTask.TileCode, out var tile))
            {
                schematic = null;
                return false;
            }

            int roomIndex = -1;
            for (var index = 0; index < tile.ResolvedSchematics.Length; index++)
            {
                var rooms = tile.ResolvedSchematics[index];
                if (rooms[0].FromFile != tileTask.FileName) continue;

                roomIndex = index;
                break;
            }

            if (roomIndex == -1)
            {
                schematic = null;
                return false;
            }

            schematic = tile.ResolvedSchematics[roomIndex][tileTask.Rotation];
            return true;
        }

        private TextCommandResult OnCmdToggleDebugConnectors(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success("Debug connector placing is currently: " + (placeDebugConnectors ? "on" : "off"));
            }

            placeDebugConnectors = (bool)args[0];
            return TextCommandResult.Success("Debug connector placing is now: " + (placeDebugConnectors ? "on" : "off"));
        }

        private void onEvent(string eventName, ref EnumHandling handling, IAttribute data)
        {
            if (dungeonGenRequest != null)
            {
                var req = dungeonGenRequest;
                sapi.Event.EnqueueMainThreadTask(() => OnCmdTiledCungeonCode(req, 1), "gendungeon");
                dungeonGenRequest = null;
            }
        }

        private TextCommandResult OnCmdTiledDungeonTest(TextCommandCallingArgs args)
        {
            sapi.Assets.Reload(AssetCategory.worldgen);
            Init();

            var code = (string)args[0];
            var dungeon = Tcfg.Dungeons.FirstOrDefault(td => td.Code == code);

            if (dungeon == null) return TextCommandResult.Error("No such dungeon defined");
            var rot = (bool)args[1] ? 4 : 1;

            var pos = args.Caller.Pos.AsBlockPos;
            pos.Y = sapi.World.BlockAccessor.GetRainMapHeightAt(pos) +  5;
            var ba = sapi.World.BlockAccessor;
            var orignalX = pos.X;
            var orignalZ = pos.Z;
            var offX = 0;

            var signblock = sapi.World.GetBlock("sign-ground-north");
            if(signblock == null) return TextCommandResult.Error("sign-ground-north block not found");
            SchematicPlacementData data = new(ba, sapi.World)
            {
                StartPos = pos,
            };

            foreach (var (tilecode,dungeonTile) in dungeon.TilesByCode)
            {
                for (var tileIndex = 0; tileIndex < dungeonTile.ResolvedSchematics.Length; tileIndex++)
                {
                    var schematicByRot = dungeonTile.ResolvedSchematics[tileIndex];

                    for (var i = 0; i < rot; i++)
                    {
                        data.ReplaceMode = schematicByRot[i].ReplaceMode;
                        schematicByRot[i].Place(data);
                        data.BlockCodes = schematicByRot[i].BlockCodes;
                        data.ItemCodes = schematicByRot[i].ItemCodes;
                        schematicByRot[i].PlaceEntitiesAndBlockEntities(data);

                        if (placeDebugConnectors)
                        {
                            var connectors = schematicByRot[i].Connectors;
                            foreach (var path in connectors)
                            {
                                var pathPosition = pos.AddCopy(path.Position).Add(path.Facing);
                                ba.SetBlock(BlockSchematic.ConnectorBlockId, pathPosition);
                                var be = ba.GetBlockEntity<BETileConnector>(pathPosition);
                                if (be != null)
                                {
                                    be.Target = string.Join(",", path.Targets);
                                    be.Name = path.Name;
                                    be.Direction = path.Facing;
                                    be.MarkDirty();
                                }
                            }
                        }

                        pos.X += schematicByRot[i].SizeX + 3;
                    }

                    pos.X = orignalX;

                    sapi.World.BlockAccessor.SetBlock(signblock.Id, pos.AddCopy(-3, 0, 0));
                    var bes = sapi.World.BlockAccessor.GetBlockEntity(pos.AddCopy(-3, 0, 0)) as BlockEntitySign;
                    if (bes != null)
                    {
                        bes.MeshAngleRad = -GameMath.PIHALF;
                        bes.SetText(tilecode + " / " + schematicByRot[0].FromFile);
                    }


                    pos.Z += schematicByRot[0].SizeZ + 3;
                    if (pos.Z - orignalZ > 450)
                    {
                        pos.Z = orignalZ;
                        offX = schematicByRot[0].SizeX*4+3;
                        orignalX += offX;
                        pos.X = orignalX;
                    }
                }

                pos.Z += 3;
            }

            return TextCommandResult.Success("dungeon generated");
        }

        internal void Init()
        {
            var assets = sapi.Assets.GetMany<TiledDungeonConfig>(sapi.Logger, "worldgen/tileddungeons.json");
            Tcfg = new TiledDungeonConfig();
            var defaultConf = assets.First(v => v.Key.Domain == GlobalConstants.DefaultDomain).Value;
            Tcfg.storyStructMinDistance = defaultConf.storyStructMinDistance;
            Tcfg.minDistance = defaultConf.minDistance;

            Tcfg.RocktypeRemapGroups = new Dictionary<string, Dictionary<AssetLocation, AssetLocation>>();
            var dungeons = new List<TiledDungeon>();

            foreach (var (code, conf) in assets)
            {
                foreach (var remap in conf.RocktypeRemapGroups)
                {
                    if (Tcfg.RocktypeRemapGroups.TryGetValue(remap.Key, out var remapGroup))
                    {
                        foreach (var (source, target) in remap.Value)
                        {
                            remapGroup.TryAdd(source, target);
                        }
                    }
                    else
                    {
                        Tcfg.RocktypeRemapGroups.TryAdd(remap.Key, remap.Value);
                    }
                }
                dungeons.AddRange(conf.Dungeons);
            }

            Tcfg.Dungeons = dungeons.ToArray();
            Tcfg.Init(sapi);
        }

        private TextCommandResult OnCmdClearAndTiledCungeonCode(TextCommandCallingArgs args)
        {
            sapi.ChatCommands.ExecuteUnparsed("/wgen regen " + (int)args[0], new TextCommandCallingArgs() { Caller = args.Caller });
            dungeonGenRequest = args;
            return TextCommandResult.Success();
        }

        private TextCommandResult OnCmdGenDeungon(TextCommandCallingArgs args)
        {
            return OnCmdTiledCungeonCode(args, 0);
        }

        private TextCommandResult OnCmdGenDeungonStepped(TextCommandCallingArgs args)
        {
            debugStopStep = 0;
            lastDungeon = null;
            placeTask = null;
            return OnCmdTiledCungeonCode(args, 0);
        }

        private TextCommandResult OnCmdTiledCungeonCode(TextCommandCallingArgs args, int argOffset)
        {
            sapi.Assets.Reload(AssetCategory.worldgen);
            Init();

            var code = (string)args[0 + argOffset];
            var tiles = (int)args[1 + argOffset];
            var dungeon = Tcfg.Dungeons.FirstOrDefault(td => td.Code == code);

            if (dungeon == null) return TextCommandResult.Error("No such dungeon defined");

            BlockPos pos;
            if (args.Parsers[2 + argOffset].IsMissing)
            {
                pos = args.Caller.Pos.AsBlockPos;
                pos.Y = sapi.World.BlockAccessor.GetTerrainMapheightAt(pos) + 50;
                // pos = new BlockPos(512000, 10, 512000);
            }
            else
            {
                pos = ((Vec3d)args[2 + argOffset]).AsBlockPos;
            }


            long seed = sapi.WorldManager.SeedWorldgen ^ 8991827198;
            if (!args.Parsers[3 + argOffset].IsMissing)
            {
                var seedString = (string)args[3 + argOffset];
                if (seedString == "true")
                {
                    seed = sapi.World.Rand.NextInt64();
                }
                else
                {
                    if (!long.TryParse(seedString,  out seed))
                    {
                        seed = GameMath.DotNetStringHash(seedString);
                    }
                }
            }
            sapi.Logger.Notification($"Dungeon seed is: {seed} , pos: {pos}");
            int size = sapi.WorldManager.RegionSize;

            var rnd = new LCGRandom(seed);
            rnd.InitPositionSeed(pos.X / size * size, pos.Z / size * size);


            var ba = sapi.World.BlockAccessor;
            if (TryPlaceTiledDungeon(ba, rnd, dungeon, pos, tiles, tiles, debugStopStep))
            {
                return TextCommandResult.Success("dungeon generated");
            }

            sapi.Logger.Notification($"Dungeon current seed: {rnd.currentSeed}, map: {rnd.mapGenSeed}");

            return TextCommandResult.Success(string.Format("Unable to generate dungeon with mintiles={0}, maxtiles={1}, seed={2}", tiles, tiles, seed));
        }

        DungeonPlaceTask? placeTask;
        TiledDungeon? lastDungeon;

        public bool TryPlaceTiledDungeon(IBlockAccessor ba, LCGRandom rnd, TiledDungeon dungeon, BlockPos startPos, int minTiles, int maxTiles, int debugStop = -1)
        {
            lastDungeon = dungeon;
            dungeonGen.DebugLogging = debugLogging;
            placeTask = dungeonGen.TryPregenerateTiledDungeon(rnd, dungeon, new List<GeneratedStructure>(), startPos, minTiles, maxTiles);

            if (debugLogging)
            {
                sapi.Logger.Notification(string.Join("\r\n", dungeonGen.debugLogs));
                sapi.Logger.Notification("The tiles contain the following names:");
                foreach (var val in dungeon.Tiles)
                {
                    sapi.Logger.Notification(val.Code + ": " + string.Join(", ", val.CachedNames));
                }
            }

            if (placeTask != null)
            {
                return placeDungeon(ba, dungeon, debugStop, placeTask);
            }
            return false;
        }

        private bool placeDungeon(IBlockAccessor ba, TiledDungeon dungeon, int debugStop, DungeonPlaceTask dungeonPlaceTask)
        {
            for (var i = 0; i < dungeonPlaceTask.TilePlaceTasks.Count; i++)
            {
                var placeTask = dungeonPlaceTask.TilePlaceTasks[i];
                if (!TryGetDungeonTile(placeTask, dungeon, out var schem))
                {
                    sapi.Logger.Warning($"Could not find dungeon tile: {placeTask.TileCode} | {placeTask.FileName}");
                    continue;
                }

                if (i == debugStop)
                {
                    var blockId = sapi.World.GetBlock("creativeblock-79")?.Id ?? 1;
                    ba.SetBlock(blockId, placeTask.Pos.Copy().Add(0, 0, 0));
                    ba.SetBlock(blockId, placeTask.Pos.Copy().Add(schem.SizeX, 0, 0));
                    ba.SetBlock(blockId, placeTask.Pos.Copy().Add(schem.SizeX, 0, schem.SizeZ));
                    ba.SetBlock(blockId, placeTask.Pos.Copy().Add(0, 0, schem.SizeZ));

                    ba.SetBlock(blockId, placeTask.Pos.Copy().Add(0, schem.SizeY, 0));
                    ba.SetBlock(blockId, placeTask.Pos.Copy().Add(schem.SizeX, schem.SizeY, 0));
                    ba.SetBlock(blockId, placeTask.Pos.Copy().Add(schem.SizeX, schem.SizeY, schem.SizeZ));
                    ba.SetBlock(blockId, placeTask.Pos.Copy().Add(0, schem.SizeY, schem.SizeZ));

                    return true;
                }

                SchematicPlacementData data = new(ba, sapi.World)
                {
                    StartPos = placeTask.Pos,
                    ReplaceMode = schem.ReplaceMode,
                    BlockCodes = schem.BlockCodes,
                    ItemCodes = schem.ItemCodes,
                    Attributes = dungeonPlaceTask.Attributes
                };
                schem.Place(data);

                if (i == 0)
                {
                    var blockId = sapi.World.GetBlock("creativeblock-79")?.Id ?? 1;
                    ba.SetBlock(blockId, placeTask.Pos.Copy().Add(schem.SizeX / 2, schem.SizeY, schem.SizeZ / 2));
                }

                if (placeDebugConnectors)
                {
                    placeConnectorsForDebug(ba, schem.Connectors, placeTask.Pos);
                }
            }

            if (placeDebugConnectors)
            {
                placeConnectorsForDebug(ba, dungeonPlaceTask.OpenSet, null);
            }

            if (debugLogging)
            {
                sapi.Logger.Notification("Open set:\r\n" + string.Join("\r\n", dungeonPlaceTask.OpenSet.Select(conn => conn.Name + ":" + string.Join(",", conn.Targets))));
            }
            return true;
        }

        private void placeConnectorsForDebug(IBlockAccessor ba, List<ConnectorMetaData> connectors, BlockPos? basePosition)
        {
            foreach (var connector in connectors)
            {
                var pathPosition = basePosition != null ?
                    basePosition.AddCopy(connector.Position) :
                    new BlockPos(connector.Position.X, connector.Position.Y, connector.Position.Z);

                ba.SetBlock(BlockSchematic.ConnectorBlockId, pathPosition);
                var be = ba.GetBlockEntity<BETileConnector>(pathPosition);
                be.Target = string.Join(",", connector.Targets);
                be.Name = connector.Name;
                be.Direction = connector.Facing;
                be.MarkDirty();
            }
        }

        internal DungeonPlaceTask? TryPregenerateTiledDungeon(LCGRandom rand2, TiledDungeon dungeon, List<GeneratedStructure> generatedStructures, BlockPos blockPos, int mintiles, int maxtiles)
        {
            return dungeonGen.TryPregenerateTiledDungeon(rand2, dungeon, generatedStructures, blockPos, mintiles, maxtiles);
        }
    }
}
