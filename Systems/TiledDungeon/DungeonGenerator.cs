using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Common.Collectible.Block;

namespace Vintagestory.ServerMods
{
    /// <summary>
    /// Generates a DungeonPlaceTask, does not place any blocks
    /// </summary>
    public class DungeonGenerator
    {
        protected ILogger logger;
        protected Vec3i mapsize;

        public DungeonGenerator(ILogger logger, Vec3i mapsize)
        {
            this.logger = logger;
            this.mapsize = mapsize;
        }

        public DungeonPlaceTask? TryPregenerateTiledDungeon(LCGRandom rnd, TiledDungeon dungeon, List<GeneratedStructure> existingStructures, BlockPos startPos, int minTiles, int maxTiles)
        {
            var rot = rnd.NextInt(4);
            // It looks like this is supposed to be Stack<> but we need to iterate over the entire list and rebuild it often so a list is better
            var openSet = new List<ConnectorMetaData>();
            var placeTasks = new List<TilePlaceTask>();
            var gennedStructures = new List<GeneratedStructure>();
            var schematic = dungeon.Start[rot];

            var newloc = new Cuboidi(startPos.X, startPos.Y, startPos.Z, startPos.X + schematic.SizeX, startPos.Y + schematic.SizeY, startPos.Z + schematic.SizeZ);
            var dgd = new DungeonGenWorkspace(dungeon, minTiles, maxTiles, openSet, placeTasks, existingStructures, gennedStructures,null);

            addTile(dgd, new ConnectorMetaData(), dungeon.start, rot, schematic, startPos, newloc);

            if (TryGenerateTiles(rnd, dgd))
            {
                logger.Notification($"Dungeon with {placeTasks.Count} schematics generated");
                if (dgd.OpenSet.Count > 0) logger.Notification("But {0} sides could not be closed", dgd.OpenSet.Count);
                var stairsIndex = -1;
                if (dungeon.Stairs?.Length > 0) stairsIndex = rnd.NextInt(dungeon.Stairs.Length);
                return new DungeonPlaceTask(dungeon.Code, placeTasks, gennedStructures, openSet, stairsIndex);
            }

            return null;
        }


        private bool TryGenerateTiles(LCGRandom lcgRnd, DungeonGenWorkspace dgd)
        {
            var tries = dgd.MinTiles * 10;

            var tileIndices = new int[dgd.DungeonGenerator.Tiles.Count];
            for (int i = 0; i < tileIndices.Length; i++) tileIndices[i] = i;

            var randomMax = dgd.MinTiles + lcgRnd.NextInt(dgd.MaxTiles + 1 - dgd.MinTiles);

            // Continue until we either tried too many times, the dungeon is fully enclosed, or we capped at max tiles
            while (tries-- > 0 && dgd.OpenSet.Count > 0 && dgd.PlacedTiles < randomMax)
            {
                // TODO: Popping the first element of a list is very inefficient. This needs a better datastructure
                var openSide = dgd.OpenSet.PopFirst();

                // This can result in a loop of 10 to 100 iterations where its just requeing the same few connectors over and over once were done generating everything else
                // We might want to take these out into a separate set?
                if (openSide.Targets.Length == 0 && openSide.TargetsForParent != null && openSide.TargetsForParent.Length > 0)
                {
                    dgd.OpenSet.Add(openSide);
                    continue;
                }

                var pickTileTries = tileIndices.Length;
                while (pickTileTries-- > 0)
                {
                    // 1. Pick a random tile
                    DungeonTile? tile = pickTile(dgd, tileIndices, lcgRnd, openSide);
                    if (tile == null) break;

                    // 2. Is a sub gen? Call sub gen
                    if (tile.TileGenerator != null)
                    {
                        var generated = TryGenSubGenerator(lcgRnd, tile, openSide, dgd);
                        if (generated == 0)
                        {
                            // enqueue the last tried path again when failed
                            dgd.OpenSet.Add(openSide);
                        }
                        else
                        {
                            dgd.PlacedTiles++;
                        }

                        break;
                    }

                    // 3. Otherwise, pick a random connectable schematic from this tile
                    BlockSchematicPartial? schematic = null;
                    int rot = 0;
                    BlockPos? offsetPos = null;

                    tile.FreshShuffleIndices(lcgRnd);
                    int len = tile.ResolvedSchematics.Length;
                    for (var k = 0; k < len; k++)
                    {
                        var schematicByRot = tile.ResolvedSchematics[tile.ShuffledIndices[k]];

                        int startRot = openSide.Rotation;
                        if (openSide.Facing.IsHorizontal)
                        {
                            startRot = lcgRnd.NextInt(4);
                        }

                        // Try any of the 4 sides and see which one can connect
                        for (var i = 0; i < 4; i++)
                        {
                            rot = (startRot + i) % 4;
                            var path = schematicByRot[rot].Connectors.FirstOrDefault(p => p.ConnectsTo(openSide));
                            if (path.Valid)
                            {
                                offsetPos = path.Position;
                                schematic = schematicByRot[rot];
                                k = len;
                                break;
                            }
                        }
                    }

                    if (schematic == null) continue;

                    // 4. Final interesection tests, then place it
                    var startPos = openSide.Position.AddCopy(openSide.Facing).Sub(offsetPos);

                    var newloc = new Cuboidi(startPos.X, startPos.Y, startPos.Z, startPos.X + schematic.SizeX, startPos.Y + schematic.SizeY, startPos.Z + schematic.SizeZ);

                    if (Intersects(dgd.GeneratedStructures, newloc)) continue;
                    if (Intersects(dgd.ExistingStructures, newloc)) continue;
                    if (startPos.X < 0 || startPos.Y < 0 || startPos.Z < 0) continue; // Beyond block bounds
                    if (startPos.X >= mapsize.X || startPos.Y >= mapsize.Y || startPos.Z >= mapsize.Z) continue; // Beyond block bounds

                    addTile(dgd, openSide, tile.Code, rot, schematic, startPos, newloc);
                    dgd.PlacedTiles++;
                    break;
                }
            }

            if (dgd.PlacedTiles >= dgd.MinTiles)
            {
                AddEndCaps(dgd, lcgRnd);
                return true;
            }

            return false;
        }

        private void addTile(DungeonGenWorkspace dgd, ConnectorMetaData openSide, string tileCode, int rot, BlockSchematicPartial schematic, BlockPos startPos, Cuboidi newloc)
        {
            dgd.PlaceTasks.Add(new TilePlaceTask()
            {
                TileCode = tileCode,
                Rotation = rot,
                Pos = startPos.Copy(),
                FileName = schematic.FromFileName,
                SizeX = schematic.SizeX,
                SizeY = schematic.SizeY,
                SizeZ = schematic.SizeZ,
            });

            dgd.GeneratedStructures.Add(new GeneratedStructure()
            {
                Code = "dungeon-" + tileCode,
                Location = newloc,
                SuppressRivulets = true
            });

            FilterOpenSet(schematic, tileCode, rot, startPos, dgd, openSide);

            dgd.OnTileAdded(tileCode);
        }

        private void AddEndCaps(DungeonGenWorkspace dgd, LCGRandom lcgRnd)
        {
            if (dgd.DungeonGenerator.EndSchematics == null) return;
            var endLength = dgd.DungeonGenerator.EndSchematics.Length;
            var toClose = new List<ConnectorMetaData>(dgd.OpenSet);

            int added = 0;

            foreach (var openCon in toClose)
            {
                var startSchematic = lcgRnd.NextInt(endLength);
                for (var k = 0; k < endLength; k++)
                {
                    var curSchematicIndex = (startSchematic + k) % endLength;
                    var schematicByRot = dgd.DungeonGenerator.EndSchematics[curSchematicIndex];

                    int startRot = openCon.Rotation;
                    if (openCon.Facing.IsHorizontal)
                    {
                        startRot = lcgRnd.NextInt(4);
                    }

                    int rot = 0;
                    BlockPos? offsetPos = null;
                    BlockSchematicPartial? schematic = null;
                    for (var i = 0; i < 4; i++)
                    {
                        rot = (startRot + i) % 4;
                        var path = schematicByRot[rot].Connectors.FirstOrDefault(p => p.ConnectsTo(openCon));
                        if (path.Valid)
                        {
                            offsetPos = path.Position;
                            schematic = schematicByRot[rot];
                            k = endLength;
                            break;
                        }
                    }

                    if (schematic == null) continue;

                    var startPos = openCon.Position.AddCopy(openCon.Facing).Sub(offsetPos);
                    var newloc = new Cuboidi(startPos.X, startPos.Y, startPos.Z, startPos.X + schematic.SizeX, startPos.Y + schematic.SizeY, startPos.Z + schematic.SizeZ);

                    if (startPos.X < 0 || startPos.Y < 0 || startPos.Z < 0) continue; // Beyond block bounds
                    if (startPos.X >= mapsize.X || startPos.Y >= mapsize.Y || startPos.Z >= mapsize.Z) continue; // Beyond block bounds

                    addTile(dgd, openCon, dgd.DungeonGenerator.ends[curSchematicIndex], rot, schematic, startPos, newloc);
                    added++;
                }
            }

            //System.Diagnostics.Debug.Write(added + " end caps added, " + toClose.Count + " needed.");
        }

        private double GetTotalWeight(DungeonGenWorkspace dgd)
        {
            double weight = 0;
            for (int i = 0; i < dgd.CanGenerate.Count; i++)
            {
                var tile = dgd.CanGenerate[i];
                if (dgd.TileQuantityByCode[tile.Code] < tile.Max) weight += tile.Chance;
            }
            return weight;
        }

        /// <summary>
        /// Provides a random tile from given list of tiles and their weighting values
        /// </summary>
        /// <param name="dgd"></param>1
        /// <param name="tileIndices"></param>
        /// <param name="lcgrand"></param>
        /// <param name="openSide"></param>
        /// <returns></returns>
        protected DungeonTile? pickTile(DungeonGenWorkspace dgd, int[] tileIndices, LCGRandom lcgrand, ConnectorMetaData openSide)
        {
            // TODO check if we have a tile that can even satisfy the current openside
            // var totalWeight = GetTotalWeight(dgd);
            // if (dgd.MustGenerate.Count > 0)
            // {
            //     tile = dgd.MustGenerate[dgd.MustGenerate.Count - 1];
            // }
            // else
            // {
            //     double rndVal = lcgrand.NextDouble() * totalWeight;
            //     int i = 0;
            //     while (rndVal > 0)
            //     {
            //         tile = dgd.CanGenerate[i++];
            //         if (dgd.TilesQuantity[tile.Code] < tile.Max)
            //         {
            //             rndVal -= tile.Chance;
            //         }
            //     }
            // }



            tileIndices.Shuffle(lcgrand);
            var rndval = (float)lcgrand.NextDouble() * dgd.DungeonGenerator.totalChance;
            var cnt = dgd.DungeonGenerator.Tiles.Count;
            DungeonTile? currentBestTile = null;

            for (var k = 0; k < cnt; k++)
            {
                var tile = dgd.DungeonGenerator.Tiles[tileIndices[k]];

                dgd.TileQuantityByCode.TryGetValue(tile.Code, out var quantity);
                if (quantity >= tile.Max)
                {
                    continue;
                }

                if (!tile.CachedNames.Any(n => openSide.Targets.Contains(n)))
                {
                    continue;
                }

                currentBestTile = tile;

                // Prefer tiles with higher chance value
                var tileChance = tile.Chance;
                if (quantity > tile.Max)
                {
                    tileChance = 0;
                }
                rndval -= tileChance;

                if (rndval > 0)
                {
                    continue;
                }

                return tile;
            }

            return currentBestTile;
        }

        /// <summary>
        /// Removes from targets and entry that are in requireOpened
        /// </summary>
        /// <param name="requireOpened"></param>
        /// <param name="targets"></param>
        /// <returns></returns>
        private static ConnectorMetaData delegateRequireOpenedToParent(string[]? requireOpened, ConnectorMetaData connector)
        {
            if (requireOpened == null) return connector;

            string[] targets = connector.Targets;
            string[] targetsForParent = connector.TargetsForParent;

            for (int i = 0; i < requireOpened.Length; i++)
            {
                var index = targets.IndexOf(requireOpened[i]);
                if (index < 0) continue;

                if (targetsForParent == null) {
                    targetsForParent = [requireOpened[i]];
                } else
                {
                    targetsForParent = targetsForParent.Append(requireOpened[i]);
                }

                targets = targets.RemoveAt(index);
            }

            return new ConnectorMetaData(connector.Position, connector.Facing, connector.Rotation, connector.Name, targets, targetsForParent);
        }

        private int TryGenSubGenerator(LCGRandom rnd, DungeonTile dungeonTile, ConnectorMetaData openside, DungeonGenWorkspace dgd)
        {
            if (dungeonTile.TileGenerator.MinTiles == 0)
            {
                logger.Warning("Child generator {0} has mintiles set to 0, will not generate anything", dungeonTile.TileGenerator.Code);
            }

            var childDgd = dgd.SpawnChild(dungeonTile);

            var tries = 20;
            while (tries-- > 0  )
            {
                childDgd.Reset(openside, dgd.GeneratedStructures);

                if (TryGenerateTiles(rnd, childDgd))
                {
                    if (dungeonTile.TileGenerator.RequireClosed != null &&
                        childDgd.OpenSet.Any(s =>
                            dungeonTile.TileGenerator.RequireClosed.Any(n =>
                                s.Name.Equals(n)
                                || s.Targets.Contains(n))))
                    {
                        rnd.NextDouble();
                        continue;
                    }

                    childDgd.CommitToParent();

                    return childDgd.PlaceTasks.Count - 1;
                }
            }


            return 0;
        }



        public bool Intersects(List<GeneratedStructure> gennedStructures, Cuboidi newloc)
        {
            for (var i = 0; i < gennedStructures.Count; i++)
            {
                var loc = gennedStructures[i].Location;
                if (loc.Intersects(newloc)) return true;
            }

            return false;
        }

        public void FilterOpenSet(BlockSchematicPartial schematic, string code, int rot, BlockPos startPos, DungeonGenWorkspace dgd, ConnectorMetaData attachingCon)
        {
            var newOpenSet = dgd.OpenSet.ToList();
            List<ConnectorMetaData> toAdd = new(); // Needs to be added after we looped over the OpenSet

            foreach (var selfCon in schematic.Connectors)
            {
                // This is is the side we're attaching to
                if (selfCon.ConnectsTo(attachingCon, startPos)) continue;

                // Perhaps we end up connecting to something else currently not connected?
                // If so, remove from open set, and don't add our own connector
                bool found = false;
                for (int i = 0; i < newOpenSet.Count; i++)
                {
                    var openCon = newOpenSet[i];
                    if (selfCon.ConnectsTo(openCon, startPos))
                    {
                        newOpenSet.RemoveAt(i);
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    var connector = selfCon.Clone().Offset(startPos);
                    connector = delegateRequireOpenedToParent(dgd.DungeonGenerator.RequireOpened, connector);
                    toAdd.Add(connector);
                }
            }


            dgd.OpenSet.Clear();
            dgd.OpenSet.AddRange(newOpenSet);
            dgd.OpenSet.AddRange(toAdd);
        }
    }
}
