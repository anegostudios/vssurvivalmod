using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.ServerMods
{
    public class ModSystemTiledDungeons : ModSystem
    {
        protected ICoreServerAPI sapi = null!;
        public TiledDungeonConfig Tcfg = null!;

        DungeonGenerator dungeonGen = null!;
        TextCommandCallingArgs? dungeonGenRequest;

        int debugStopStep = -1;
        private bool placeDebugConnectors;

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
                .BeginSub("tiledd")
                    .WithDesc("Generate a dungeon at the callers position")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithArgs(parsers.Word("tiled dungeon code"), parsers.Int("amount of tiles"), parsers.OptionalWorldPosition("pos"), parsers.OptionalWord("randomSeed"))
                    .HandleWith(OnCmdGenDeungon)
                .EndSub()
                .BeginSub("tiledds")
                    .WithDesc("Generate a dungeon at the callers position, stepped")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithArgs(parsers.Word("tiled dungeon code"), parsers.Int("amount of tiles"), parsers.OptionalWorldPosition("pos"), parsers.OptionalWord("randomSeed"))
                    .HandleWith(OnCmdGenDeungonStepped)
                .EndSub()
                .BeginSub("tdstep")
                    .WithDesc("Generate a dungeon at the callers position, stepped")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith((_) => {
                        if (placeTask == null || lastDungeon == null) return TextCommandResult.Error("No dungeon to step");
                        if (placeTask.TilePlaceTasks.Count > debugStopStep) debugStopStep++;
                        placeDungeon(sapi.World.BlockAccessor, lastDungeon, debugStopStep, placeTask);
                        return TextCommandResult.Success("Stepped to " + debugStopStep + " / " + placeTask.TilePlaceTasks.Count);
                    })
                .EndSub()
                .BeginSub("tdstepreset")
                    .WithDesc("Generate a dungeon at the callers position, stepped")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith((_) => {
                        debugStopStep = -1;
                        lastDungeon = null;
                        placeTask = null;
                        return TextCommandResult.Success();
                    })
                .EndSub()

                .BeginSub("ctiledd")
                    .WithDesc("Clear chunks of given area radius, then generated a dungeon at the callers position")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithArgs(parsers.Int("chunk clear radius"), parsers.Word("tiled dungeon code"), parsers.Int("amount of tiles"), parsers.OptionalWorldPosition("pos"), parsers.OptionalWord("randomSeed"))
                    .HandleWith(OnCmdClearAndTiledCungeonCode)
                .EndSub()
                .BeginSub("tileddl")
                    .WithDesc("Lineup of all variations of tiles with all orientations side by side for debugging")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithArgs(parsers.Word("tiled dungeon code"))
                    .HandleWith(OnCmdTiledDungeonTest)
                .EndSub()
                .BeginSub("tileddd")
                    .WithDesc("Toogle debug connector placing on/off for tiledd commands")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithArgs(parsers.OptionalBool("mode"))
                    .HandleWith(OnCmdToggleDebugConnectors)
                .EndSub()
            ;
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
            // need copy else shuffle breaks determinism
            var dungeon = Tcfg.Dungeons.FirstOrDefault(td => td.Code == code)?.Copy();

            if (dungeon == null) return TextCommandResult.Error("No such dungeon defined");

            var pos = args.Caller.Pos.AsBlockPos;
            pos.Y = sapi.World.BlockAccessor.GetTerrainMapheightAt(pos) +  5;
            var ba = sapi.World.BlockAccessor;
            var orignalX = pos.X;

            var signblock = sapi.World.GetBlock("sign-ground-north");
            if(signblock == null) return TextCommandResult.Error("sign-ground-north block not found");

            foreach (var (tilecode,dungeonTile) in dungeon.TilesByCode)
            {
                for (var tileIndex = 0; tileIndex < dungeonTile.ResolvedSchematics.Length; tileIndex++)
                {
                    var schematicByRot = dungeonTile.ResolvedSchematics[tileIndex];

                    for (var i = 0; i < 4; i++)
                    {
                        schematicByRot[i].Place(ba, sapi.World, pos);
                        schematicByRot[i].PlaceEntitiesAndBlockEntities(ba, sapi.World, pos, schematicByRot[i].BlockCodes, schematicByRot[i].ItemCodes);

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
                        bes.SetText(tilecode + " / " + schematicByRot[0].FromFileName);
                    }


                    pos.Z += schematicByRot[0].SizeZ + 3;
                }

                pos.Z += 3;
            }

            return TextCommandResult.Success("dungeon generated");
        }

        internal void Init()
        {
            var asset = sapi.Assets.Get("worldgen/tileddungeons.json");
            Tcfg = asset.ToObject<TiledDungeonConfig>();
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
            debugStopStep = -1;
            lastDungeon = null;
            placeTask = null;

            debugStopStep++;
            return OnCmdTiledCungeonCode(args, 0);
        }

        private TextCommandResult OnCmdTiledCungeonCode(TextCommandCallingArgs args, int argOffset)
        {
            sapi.Assets.Reload(AssetCategory.worldgen);
            Init();

            var code = (string)args[0 + argOffset];
            var tiles = (int)args[1 + argOffset];
            var dungeon = Tcfg.Dungeons.FirstOrDefault(td => td.Code == code)?.Copy();

            if (dungeon == null) return TextCommandResult.Error("No such dungeon defined");

            BlockPos pos;
            if (args.Parsers[2 + argOffset].IsMissing)
            {
                pos = args.Caller.Pos.AsBlockPos;
                pos.Y = sapi.World.BlockAccessor.GetTerrainMapheightAt(pos) + 5;
                // pos = new BlockPos(512000, 10, 512000);
            }
            else
            {
                pos = ((Vec3d)args[2 + argOffset]).AsBlockPos;
            }

            long seed = sapi.WorldManager.Seed ^ 8991827198;
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
            for (var i = 0; i < 1; i++)
            {
                if (TryPlaceTiledDungeon(ba, rnd, dungeon, pos, tiles, tiles, debugStopStep))
                {
                    return TextCommandResult.Success("dungeon generated");
                }

                sapi.Logger.Notification($"Dungeon current seed: {rnd.currentSeed} , map: {rnd.mapGenSeed}");
            }

            return TextCommandResult.Success("Unable to generate dungeon of this size after 1 attempts");
        }

        DungeonPlaceTask? placeTask;
        TiledDungeon? lastDungeon;

        public bool TryPlaceTiledDungeon(IBlockAccessor ba, LCGRandom rnd, TiledDungeon dungeon, BlockPos startPos, int minTiles, int maxTiles, int debugStop = -1)
        {
            lastDungeon = dungeon;
            placeTask = dungeonGen.TryPregenerateTiledDungeon(rnd, dungeon, new List<GeneratedStructure>(), startPos, minTiles, maxTiles);
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
                if (dungeon.TilesByCode.TryGetValue(placeTask.TileCode, out var tile))
                {
                    int roomIndex = -1;
                    for (var index = 0; index < tile.ResolvedSchematics.Length; index++)
                    {
                        var rooms = tile.ResolvedSchematics[index];
                        if (rooms[0].FromFileName != placeTask.FileName) continue;

                        roomIndex = index;
                        break;
                    }

                    if (roomIndex == -1)
                    {
                        sapi.Logger.Warning("Failed to generate dungeon");
                        return false;
                    }

                    var schem = tile.ResolvedSchematics[roomIndex][placeTask.Rotation];

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

                    schem.Place(ba, sapi.World, placeTask.Pos);
                    schem.PlaceEntitiesAndBlockEntities(ba, sapi.World, placeTask.Pos, schem.BlockCodes, schem.ItemCodes);

                    if (i == 0)
                    {
                        var blockId = sapi.World.GetBlock("creativeblock-79")?.Id ?? 1;
                        ba.SetBlock(blockId, placeTask.Pos.Copy().Add(schem.SizeX / 2, schem.SizeY, schem.SizeZ / 2));
                    }

                    if (placeDebugConnectors)
                    {
                        //tile.PlaceConnectorsForDebug(ba, placeTask.Pos, roomIndex, placeTask.Rotation);
                    }
                }
                else
                {
                    sapi.Logger.Warning($"Could not find dungeon tile: {placeTask.TileCode} | {placeTask.FileName}");
                }
            }

            if (placeDebugConnectors)
            {
                foreach (var posFacing in dungeonPlaceTask.OpenSet)
                {
                    var blockPos = new BlockPos(posFacing.Position.X, posFacing.Position.Y, posFacing.Position.Z);
                    ba.SetBlock(BlockSchematic.ConnectorBlockId, blockPos);
                    // ba.SetBlock(BlockSchematic.ConnectorBlockId, posFacing.Position.AddCopy(0,1,0));
                    var be = ba.GetBlockEntity<BETileConnector>(blockPos);
                    be.Target = string.Join(",", posFacing.Targets);
                    be.Name = posFacing.Name;
                    be.Direction = posFacing.Facing;
                    be.MarkDirty();
                }
            }
            return true;
        }

        internal DungeonPlaceTask? TryPregenerateTiledDungeon(LCGRandom rand2, TiledDungeon dungeon, List<GeneratedStructure> generatedStructures, BlockPos blockPos, int mintiles, int maxtiles)
        {
            return dungeonGen.TryPregenerateTiledDungeon(rand2, dungeon, generatedStructures, blockPos, mintiles, maxtiles);
        }
    }
}
