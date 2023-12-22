using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods
{
    [ProtoContract]
    public class DungeonPlaceTask
    {
        [ProtoMember(1)]
        public string Code;
        [ProtoMember(2)]
        public List<TilePlaceTask> TilePlaceTasks;
        [ProtoMember(3)]
        public Cuboidi DungeonBoundaries;

        public DungeonPlaceTask GenBoundaries()
        {
            DungeonBoundaries = new Cuboidi(TilePlaceTasks[0].Pos, TilePlaceTasks[0].Pos);

            foreach (var task in TilePlaceTasks)
            {
                DungeonBoundaries.X1 = Math.Min(DungeonBoundaries.X1, task.Pos.X);
                DungeonBoundaries.Y1 = Math.Min(DungeonBoundaries.Y1, task.Pos.Y);
                DungeonBoundaries.Z1 = Math.Min(DungeonBoundaries.Z1, task.Pos.Z);

                DungeonBoundaries.X2 = Math.Max(DungeonBoundaries.X2, task.Pos.X + task.SizeX);
                DungeonBoundaries.Y2 = Math.Max(DungeonBoundaries.Y2, task.Pos.Y + task.SizeY);
                DungeonBoundaries.Z2 = Math.Max(DungeonBoundaries.Z2, task.Pos.Z + task.SizeZ);
            }

            return this;
        }
    }

    [ProtoContract]
    public class TilePlaceTask
    {
        [ProtoMember(1)]
        public string TileCode;
        [ProtoMember(2)]
        public int Rotation;
        [ProtoMember(3)]
        public BlockPos Pos;

        public int SizeX;
        public int SizeY;
        public int SizeZ;
    }

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
            ;
        }

        internal void init()
        {
            IAsset asset = api.Assets.Get("worldgen/tileddungeons.json");
            Tcfg = asset.ToObject<TiledDungeonConfig>();
            Tcfg.Init(api);
        }

        private TextCommandResult OnCmdTiledCungeonCode(TextCommandCallingArgs args)
        {
            api.Assets.Reload(AssetCategory.worldgen);
            init();

            string code = (string)args[0];
            int tiles = (int)args[1];
            var dungeon = Tcfg.Dungeons.FirstOrDefault(td => td.Code == code);

            if (dungeon == null) return TextCommandResult.Error("No such dungeon defined");

            var pos = args.Caller.Pos.AsBlockPos;
            pos.Y = api.World.BlockAccessor.GetTerrainMapheightAt(pos) +  1;
            var ba = api.World.BlockAccessor;

            var rnd = new NormalRandom(api.World.Rand.Next());

            for (int i = 0; i < 50; i++)
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
            int rot = rnd.NextInt(4);
            
            Queue<DungeonTileSide> openSet = new Queue<DungeonTileSide>();
            List<TilePlaceTask> placeTasks = new List<TilePlaceTask>();
            List<GeneratedStructure> gennedStructures = new List<GeneratedStructure>();

            var btile = dungeon.Tiles[rnd.NextInt(dungeon.Tiles.Count)];
            var loc = place(btile, rot, startPos, openSet, placeTasks);
            gennedStructures.Add(new GeneratedStructure()
            {
                Code = btile.Code,
                Location = loc,
                SuppressRivulets = true
            });

            int tries = minTiles * 10;
            while (tries-- > 0 && openSet.Count > 0)
            {
                var openside = openSet.Dequeue();
                dungeon.Tiles.Shuffle(rnd);
                float rndval = (float)rnd.NextDouble() * dungeon.totalChance;
                int cnt = dungeon.Tiles.Count;
                int skipped = 0;

                bool maxTilesReached = placeTasks.Count >= maxTiles;
                if (maxTilesReached) rndval = 0;

                for (int k = 0; k < cnt + skipped; k++)
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

                    if (openside.Constraints != null && WildcardUtil.Match(openside.Constraints, tile.Code))
                    {
                        int startRot = api.World.Rand.Next(4);
                        rot = 0;
                        var attachingFace = openside.Side.Opposite;

                        bool ok = false;
                        for (int i = 0; i < 4; i++)
                        {
                            rot = (startRot + i) % 4;
                            var constraints = tile.Constraints[(attachingFace.Index + (4 - rot)) % 4];

                            if (WildcardUtil.Match(constraints, openside.Code))
                            {
                                ok = true;
                                break;
                            }
                        }
                        if (!ok) continue;

                        var schematic = tile.ResolvedSchematic[0][rot];

                        BlockPos pos = openside.Pos.Copy();
                        if (openside.Side == BlockFacing.NORTH) pos.Z -= schematic.SizeZ;
                        if (openside.Side == BlockFacing.DOWN) pos.Y -= schematic.SizeY;
                        if (openside.Side == BlockFacing.WEST) pos.X -= schematic.SizeX;

                        if (openside.Side.Axis == EnumAxis.Z)
                        {
                            pos.X += (openside.SizeX - schematic.SizeX) / 2;
                        }
                        if (openside.Side.IsHorizontal)
                        {
                            pos.Y += (openside.SizeY - schematic.SizeY) / 2;
                        }
                        if (openside.Side.Axis == EnumAxis.X)
                        {
                            pos.Z += (openside.SizeZ - schematic.SizeZ) / 2;
                        }

                        var hereschematic = tile.ResolvedSchematic[0][rot];
                        var newloc = new Cuboidi(pos.X, pos.Y, pos.Z, pos.X + hereschematic.SizeX, pos.Y + hereschematic.SizeY, pos.Z + hereschematic.SizeZ);
                        if (intersects(gennedStructures, newloc)) continue;

                        loc = place(tile, rot, pos, openSet, placeTasks, openside.Side.Opposite);
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
                        tile.ResolvedSchematic[rndval][placeTask.Rotation].Place(ba, api.World, placeTask.Pos, WorldEdit.WorldEdit.ReplaceMetaBlocks);
                    }
                    
                }

                return true;
            }

            return false;
        }

        protected bool intersects(List<GeneratedStructure> gennedStructures, Cuboidi newloc)
        {
            for (int i = 0; i < gennedStructures.Count; i++)
            {
                var loc = gennedStructures[i].Location;
                if (loc.Intersects(newloc)) return true;
            }

            return false;
        }

        protected Cuboidi place(DungeonTile tile, int rot, BlockPos startPos, Queue<DungeonTileSide> openSet, List<TilePlaceTask> placeTasks, BlockFacing attachingFace = null)
        {
            var schematic = tile.ResolvedSchematic[0][rot];

            placeTasks.Add(new TilePlaceTask()
            {
                TileCode = tile.Code,
                Rotation = rot,
                Pos = startPos.Copy(),
                SizeX = schematic.SizeX,
                SizeY = schematic.SizeY,
                SizeZ = schematic.SizeZ,
            });            

            var constraints = rotate(rot, tile.Constraints);

            for (int i = 0; i < 6; i++)
            {
                var face = BlockFacing.ALLFACES[i];
                if (constraints[i] != null && constraints[i].Length > 0)
                {
                    if (face == attachingFace) continue;

                    openSet.Enqueue(new DungeonTileSide()
                    {
                        Constraints = constraints[i],
                        Code = tile.Code,
                        Pos = startPos.AddCopy(
                            (face.Normali.X + 1) / 2 * schematic.SizeX,
                            (face.Normali.Y + 1) / 2 * schematic.SizeY,
                            (face.Normali.Z + 1) / 2 * schematic.SizeZ
                        ),
                        SizeX = schematic.SizeX,
                        SizeY = schematic.SizeY,
                        SizeZ = schematic.SizeZ,
                        Side = face
                    });
                }
            }

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
