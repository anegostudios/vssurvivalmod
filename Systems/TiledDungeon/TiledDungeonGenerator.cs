using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Common.Collectible.Block;

namespace Vintagestory.ServerMods
{
    public class ModSystemTiledDungeonGenerator : ModSystem
    {
        protected ICoreServerAPI api;
        public TiledDungeonConfig Tcfg;
        // bool replaceMetaBlocks;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands
                .GetOrCreate("debug")
                .BeginSub("tiledd")
                    .WithDesc("Tiled dungeon generator debugger/tester")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithArgs(parsers.Word("tiled dungeon code"), parsers.Int("amount of tiles"))
                    .HandleWith(OnCmdTiledCungeonCode)
                .EndSub()
                .BeginSub("tileddd")
                    .WithDesc("Tiled dungeon generator debugger/tester")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithArgs(parsers.Word("tiled dungeon code"))
                    .HandleWith(OnCmdTiledCungeonTest)
                .EndSub()
            ;
        }

        private TextCommandResult OnCmdTiledCungeonTest(TextCommandCallingArgs args)
        {
            api.Assets.Reload(AssetCategory.worldgen);
            init();


            var code = (string)args[0];
            // need copy else shuffle breaks determinism
            var dungeon = Tcfg.Dungeons.FirstOrDefault(td => td.Code == code).Copy();

            if (dungeon == null) return TextCommandResult.Error("No such dungeon defined");

            var pos = args.Caller.Pos.AsBlockPos;
            pos.Y = api.World.BlockAccessor.GetTerrainMapheightAt(pos) +  30;
            var ba = api.World.BlockAccessor;
            var pathwayBlockId = api.World.BlockAccessor.GetBlock(new AssetLocation("meta-connector")).BlockId;
            var orignalX = pos.X;
            foreach (var (_,dungeonTile) in dungeon.TilesByCode)
            {
                for (var i = 0; i < 4; i++)
                {
                    var blockPosFacings = dungeonTile.ResolvedSchematic[0][i].PathwayBlocksUnpacked;

                    //place the damn thing
                    dungeonTile.ResolvedSchematic[0][i].Place(ba, api.World, pos, true);
                    dungeonTile.ResolvedSchematic[0][i].PlaceEntitiesAndBlockEntities(ba, api.World, pos, new Dictionary<int, AssetLocation>(), new Dictionary<int, AssetLocation>());
                    foreach (var path in blockPosFacings)
                    {
                        ba.SetBlock(pathwayBlockId, pos + path.Position);
                    }

                    pos.X += 30;
                }
                pos.Z += 30;
                pos.X = orignalX;
            }

            return TextCommandResult.Success("dungeon generated");
        }

        internal void init()
        {
            var asset = api.Assets.Get("worldgen/tileddungeons.json");
            Tcfg = asset.ToObject<TiledDungeonConfig>();
            Tcfg.Init(api);
        }

        private TextCommandResult OnCmdTiledCungeonCode(TextCommandCallingArgs args)
        {
            api.Assets.Reload(AssetCategory.worldgen);
            init();

            var code = (string)args[0];
            var tiles = (int)args[1];
            var dungeon = Tcfg.Dungeons.FirstOrDefault(td => td.Code == code).Copy();

            if (dungeon == null) return TextCommandResult.Error("No such dungeon defined");

            var pos = args.Caller.Pos.AsBlockPos;
            // var pos = new BlockPos(512400, 30, 512400);
            pos.Y = api.World.BlockAccessor.GetTerrainMapheightAt(pos) +  30;
            var ba = api.World.BlockAccessor;

            int size = api.WorldManager.RegionSize;
            var rnd = new LCGRandom(api.WorldManager.Seed ^ 8991827198);
            rnd.InitPositionSeed(pos.X / size * size, pos.Z / size * size);

            for (var i = 0; i < 50; i++)
            {
                if (TryPlaceTiledDungeon(ba, rnd, dungeon, pos, tiles, tiles))
                {
                    return TextCommandResult.Success("dungeon generated");
                }
            }

            return TextCommandResult.Error("Unable to generate dungeon of this size after 50 attempts");
        }


        public DungeonPlaceTask TryPregenerateTiledDungeon(IRandom rnd, TiledDungeon dungeon, BlockPos startPos, int minTiles, int maxTiles)
        {
            var rot = rnd.NextInt(4);

            var openSet = new Queue<BlockPosFacing>();
            var placeTasks = new List<TilePlaceTask>();
            var gennedStructures = new List<GeneratedStructure>();

            //var btile = dungeon.Tiles[rnd.NextInt(dungeon.Tiles.Count)];

            var btile = dungeon.Start != null ? dungeon.Start : dungeon.TilesByCode["4way"].ResolvedSchematic[0];
            var startCode = dungeon.Start != null ? dungeon.start : "4way";
            var loc = place(btile, startCode, rot, startPos, openSet, placeTasks);
            gennedStructures.Add(new GeneratedStructure()
            {
                Code = "dungeon-"+startCode,
                Location = loc,
                SuppressRivulets = true
            });

            var tries = minTiles * 10;
            while (tries-- > 0 && openSet.Count > 0)
            {
                var openside = openSet.Dequeue();
                dungeon.Tiles.Shuffle(rnd);
                var rndval = (float)rnd.NextDouble() * dungeon.totalChance;
                var cnt = dungeon.Tiles.Count;
                var skipped = 0;

                var maxTilesReached = placeTasks.Count >= maxTiles;
                if (maxTilesReached) rndval = 0;

                for (var k = 0; k < cnt + skipped; k++)
                {
                    var tile = dungeon.Tiles[k % cnt];
                    if (!tile.IgnoreMaxTiles && maxTilesReached) continue;

                    // Prefer tiles with higher chance value
                    rndval -= tile.Chance;
                    if (rndval > 0)
                    {
                        skipped++;
                        continue;
                    }

                    if(tile.ResolvedSchematic[0].Any(s => s.PathwayBlocksUnpacked.Any(p => openside.Facing.Opposite == p.Facing && WildcardUtil.Match(openside.Constraints, tile.Code))))
                    {
                        var startRot = rnd.NextInt(4);
                        rot = 0;
                        var attachingFace = openside.Facing.Opposite;

                        var ok = false;
                        BlockPos offsetPos=null;
                        BlockSchematicPartial schematic = null;
                        for (var i = 0; i < 4; i++)
                        {
                            rot = (startRot + i) % 4;
                            schematic = tile.ResolvedSchematic[0][rot];

                            if (schematic.PathwayBlocksUnpacked.Any(p => p.Facing == attachingFace && WildcardUtil.Match(openside.Constraints, tile.Code)))
                            {
                                offsetPos = schematic.PathwayBlocksUnpacked.First(p => p.Facing == attachingFace && WildcardUtil.Match(openside.Constraints, tile.Code)).Position;
                                ok = true;
                                break;
                            }
                        }
                        if (!ok) continue;


                        var pos = openside.Position.Copy();
                        // calc pos offset
                        pos = pos.AddCopy(openside.Facing) - offsetPos;

                        var newloc = new Cuboidi(pos.X, pos.Y, pos.Z, pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);
                        if (intersects(gennedStructures, newloc)) continue;

                        loc = place(tile, rot, pos, openSet, placeTasks, openside.Facing.Opposite);
                        gennedStructures.Add(new GeneratedStructure()
                        {
                            Code = tile.Code,
                            Location = loc,
                            SuppressRivulets = true
                        });

                        break;
                    }
                }
            }

            if (placeTasks.Count >= minTiles)
            {
                return new DungeonPlaceTask()
                {
                    Code = dungeon.Code,
                    TilePlaceTasks = placeTasks
                }.GenBoundaries();
            }

            return null;
        }

        public bool TryPlaceTiledDungeon(IBlockAccessor ba, IRandom rnd, TiledDungeon dungeon, BlockPos startPos, int minTiles, int maxTiles)
        {
            var dungenPlaceTask = TryPregenerateTiledDungeon(rnd, dungeon, startPos, minTiles, maxTiles);

            if (dungenPlaceTask != null)
            {
                foreach (var placeTask in dungenPlaceTask.TilePlaceTasks)
                {
                    if (dungeon.TilesByCode.TryGetValue(placeTask.TileCode, out var tile))
                    {
                        var rndval = rnd.NextInt(tile.ResolvedSchematic.Length);
                        tile.ResolvedSchematic[rndval][placeTask.Rotation].Place(ba, api.World, placeTask.Pos, true);

                        //tile.ResolvedSchematic[rndval][placeTask.Rotation].PlaceEntitiesAndBlockEntities(ba, api.World, placeTask.Pos, new Dictionary<int, AssetLocation>(), new Dictionary<int, AssetLocation>());
                    }

                }
                return true;
            }

            return false;
        }

        protected bool intersects(List<GeneratedStructure> gennedStructures, Cuboidi newloc)
        {
            for (var i = 0; i < gennedStructures.Count; i++)
            {
                var loc = gennedStructures[i].Location;
                if (loc.Intersects(newloc)) return true;
            }

            return false;
        }

        protected Cuboidi place(DungeonTile tile, int rot, BlockPos startPos, Queue<BlockPosFacing> openSet, List<TilePlaceTask> placeTasks,
            BlockFacing attachingFace = null)
        {
            var schematics = tile.ResolvedSchematic[0];
            return place(schematics,tile.Code, rot, startPos, openSet, placeTasks, attachingFace);
        }

        protected Cuboidi place(BlockSchematicPartial[] schematics,string code, int rot, BlockPos startPos, Queue<BlockPosFacing> openSet, List<TilePlaceTask> placeTasks, BlockFacing attachingFace = null)
        {
            var schematic = schematics[rot];
            placeTasks.Add(new TilePlaceTask()
            {
                TileCode = code,
                Rotation = rot,
                Pos = startPos.Copy(),
                SizeX = schematic.SizeX,
                SizeY = schematic.SizeY,
                SizeZ = schematic.SizeZ,
            });

            foreach (var path in schematic.PathwayBlocksUnpacked)
            {
                if(path.Facing == attachingFace) continue;
                openSet.Enqueue(new BlockPosFacing(path.Position + startPos, path.Facing, path.Constraints));
            }

            // var constraints = rotate(rot, tile.Constraints);

            // for (int i = 0; i < 6; i++)
            // {
            //     var face = BlockFacing.ALLFACES[i];
            //     if (constraints[i] != null && constraints[i].Length > 0)
            //     {
            //         if (face == attachingFace) continue;
            //
            //         openSet.Enqueue(new DungeonTileSide()
            //         {
            //             Constraints = constraints[i],
            //             Code = tile.Code,
            //             Pos = startPos.AddCopy(
            //                 (face.Normali.X + 1) / 2 * schematic.SizeX,
            //                 (face.Normali.Y + 1) / 2 * schematic.SizeY,
            //                 (face.Normali.Z + 1) / 2 * schematic.SizeZ
            //             ),
            //             SizeX = schematic.SizeX,
            //             SizeY = schematic.SizeY,
            //             SizeZ = schematic.SizeZ,
            //             Side = face
            //         });
            //     }
            // }

            return new Cuboidi(startPos.X, startPos.Y, startPos.Z, startPos.X + schematic.SizeX, startPos.Y + schematic.SizeY, startPos.Z + schematic.SizeZ);
        }

        private string[][] rotate(int rot, string[][] constraints)
        {
            return new string[][] {
                constraints[(0 - rot + 4) % 4],
                constraints[(1 - rot + 4) % 4],
                constraints[(2 - rot + 4) % 4],
                constraints[(3 - rot + 4) % 4],
                constraints[4],
                constraints[5]
            };
        }
    }
}
