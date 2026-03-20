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

        public bool DebugLogging;
        public List<string> debugLogs;

        public DungeonGenerator(ILogger logger, Vec3i mapsize, bool debugLogging = false)
        {
            this.logger = logger;
            this.mapsize = mapsize;
            DebugLogging = debugLogging;
        }

        public DungeonPlaceTask? TryPregenerateTiledDungeon(LCGRandom rnd, TiledDungeon dungeon, List<GeneratedStructure> existingStructures, BlockPos startPos, int minTiles, int maxTiles)
        {
            if (DebugLogging) debugLogs = new List<string>();

            var rot = rnd.NextInt(4);
            // It looks like this is supposed to be Stack<> but we need to iterate over the entire list and rebuild it often so a list is better
            var openSet = new List<ConnectorMetaData>();
            var placeTasks = new List<TilePlaceTask>();
            var gennedStructures = new List<GeneratedStructure>();
            var schematic = dungeon.Start[rot];

            var newloc = new Cuboidi(startPos.X, startPos.Y, startPos.Z, startPos.X + schematic.SizeX, startPos.Y + schematic.SizeY, startPos.Z + schematic.SizeZ);
            var dgd = new DungeonGenWorkspace(dungeon, minTiles, maxTiles, openSet, placeTasks, existingStructures, gennedStructures,null);

            addTile(dgd, new ConnectorMetaData(), dungeon.start, rot, schematic, new FastVec3i(startPos), newloc);

            if (TryGenerateTiles(rnd, dgd))
            {
                logger.Notification($"Dungeon with {placeTasks.Count} schematics generated");
                if (DebugLogging && dgd.OpenSet.Count > 0) debugLogs.Add(string.Format("But {0} sides could not be closed", dgd.OpenSet.Count));

                var stairsCon = openSet.FirstOrDefault(c => c.Name == dungeon.SurfaceConnectorName);
                if(stairsCon.Valid) stairsCon.FacingInt = stairsCon.Facing.Index;
                return new DungeonPlaceTask(dungeon.Code, placeTasks, gennedStructures, openSet, stairsCon);
            }

            return null;
        }

        private bool TryGenerateTiles(LCGRandom lcgRnd, DungeonGenWorkspace dgd)
        {
            if (DebugLogging)
            {
                debugLogs.Add("==================");
                debugLogs.Add("Attempting to generate tiles for dungeon gen " + dgd.DungeonGenerator.Code);
            }

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

                if (DebugLogging) debugLogs.Add(string.Format("Attempting to close connector {0}", openSide));

                var pickTileTries = tileIndices.Length;
                while (pickTileTries-- > 0)
                {
                    if (DebugLogging) debugLogs.Add("Attempting to pick next tile");

                    // 1. Pick a random tile
                    DungeonTile? tile = pickTile(dgd, tileIndices, lcgRnd, openSide);
                    if (tile == null)
                    {
                        // requeue the failed side, so if we finish the room it can also be removed properly from the open connectors
                        // else we will leave behind sides that won't close
                        dgd.OpenSet.Add(openSide);
                        break;
                    }

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
                            if(tile.GroupMaxName != null && dgd.GroupMaxCount.TryGetValue(tile.GroupMaxName, out var cur))
                            {
                                dgd.GroupMaxCount[tile.GroupMaxName]=cur+1;
                            }
                        }

                        break;
                    }

                    // 3. Otherwise, pick a random connectable schematic from this tile
                    BlockSchematicPartial? schematic = null;
                    int rot = 0;
                    FastVec3i offsetPos = new FastVec3i();

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

                    if (CheckIfNewPathsAreBlocked(tile, dgd, schematic, openSide, startPos)) continue;

                    if(tile.GroupMaxName != null && dgd.GroupMaxCount.TryGetValue(tile.GroupMaxName, out var curr))
                    {
                        dgd.GroupMaxCount[tile.GroupMaxName]=curr+1;
                    }
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

            if (DebugLogging) debugLogs.Add(string.Format("Could not finish dungeon gen " + dgd.DungeonGenerator.Code + " only {0} of {1} placeable", dgd.PlacedTiles, dgd.MinTiles));
            return false;
        }

        private bool CheckIfNewPathsAreBlocked(DungeonTile tilefordebug, DungeonGenWorkspace dgd, BlockSchematicPartial schematic, ConnectorMetaData openSide, FastVec3i startPos)
        {
            if (dgd.DungeonGenerator.RequireUnblocked == null) return false;

            // check when adding a new room/sub room piece if its connectors would lead directly into another room without any connection to it
            // and potentially block any end cap generation
            foreach (var con in schematic.Connectors)
            {
                if (!dgd.DungeonGenerator.RequireUnblocked.Contains(con.Name)) continue;

                // connection where the new rooms is attached
                if (con.ConnectsTo(openSide, startPos)) continue;
                // any other connections from the new room to any other room in the dungeon
                if (dgd.OpenSet.Any(o => con.ConnectsTo(o, startPos))) continue;

                var newpos = con.Position.Add(startPos).Add(con.Facing.Normali * 2);
                if (dgd.GeneratedStructures.Any(s => s.Location.Contains(newpos)))
                {
                    if (DebugLogging) logger.Notification("Failed to place tile {0} at {1} because it would block potential connector {2} at {3}", tilefordebug.Code, startPos, con.ToString(), newpos);
                    return true;
                }
                if (dgd.ExistingStructures.Any(s => s.Location.Contains(newpos)))
                {
                    return true;
                }
            }

            return false;
        }

        private void addTile(DungeonGenWorkspace dgd, ConnectorMetaData openSide, string tileCode, int rot, BlockSchematicPartial schematic, FastVec3i startPos, Cuboidi newloc)
        {
            var placeTask = new TilePlaceTask()
            {
                TileCode = tileCode,
                Rotation = rot,
                Pos = new BlockPos(startPos.X, startPos.Y, startPos.Z),
                FileName = schematic.FromFile,
                SizeX = schematic.SizeX,
                SizeY = schematic.SizeY,
                SizeZ = schematic.SizeZ,
            };

            dgd.PlaceTasks.Add(placeTask);

            dgd.GeneratedStructures.Add(new GeneratedStructure()
            {
                Code = "dungeon-" + tileCode,
                Location = newloc,
                SuppressRivulets = true
            });


            if (DebugLogging)
            {
                debugLogs.Add(string.Format("Added tile {0} (total place tasks={1})", tileCode, dgd.PlaceTasks.Count));
            }

            FilterOpenSet(schematic, startPos, dgd, openSide);

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
                    FastVec3i offsetPos = new FastVec3i();
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
        public DungeonTile? pickTile(DungeonGenWorkspace dgd, int[] tileIndices, LCGRandom lcgrand, ConnectorMetaData openSide)
        {
            DungeonTile? currentBestTile = null;
            // TODO check if we have a tile that can even satisfy the current openside
            // var totalWeight = GetTotalWeight(dgd);
            // if (dgd.MustGenerate.Count > 0)
            // {
            //     currentBestTile = dgd.MustGenerate[dgd.MustGenerate.Count - 1];
            // }
            // else
            // {
            //     double rndVal = lcgrand.NextDouble() * totalWeight;
            //     int i = 0;
            //     while (rndVal > 0)
            //     {
            //         currentBestTile = dgd.CanGenerate[i++];
            //         if (dgd.TileQuantityByCode[currentBestTile.Code] < currentBestTile.Max)
            //         {
            //             rndVal -= currentBestTile.Chance;
            //         }
            //     }
            // }


            tileIndices.Shuffle(lcgrand);
            var rndval = (float)lcgrand.NextDouble() * dgd.DungeonGenerator.totalChance;
            var cnt = dgd.DungeonGenerator.Tiles.Count;

            if (openSide.Targets.Length == 0)
            {
                if (DebugLogging) debugLogs.Add(string.Format("Connector {0} in the open set has no targets. Skipping.", openSide));
                return null;
            }

            for (var k = 0; k < cnt; k++)
            {
                var tile = dgd.DungeonGenerator.Tiles[tileIndices[k]];

                dgd.TileQuantityByCode.TryGetValue(tile.Code, out var quantity);
                if (quantity >= tile.Max)
                {
                    continue;
                }

                if (tile.GroupMaxName != null && dgd.DungeonGenerator.GroupMax != null &&
                    dgd.DungeonGenerator.GroupMax.TryGetValue(tile.GroupMaxName, out var max)
                    && dgd.GroupMaxCount.TryGetValue(tile.GroupMaxName, out var cur))
                {
                    if (cur+1 > max)
                    {
                        continue;
                    }
                }

                if (!tile.CachedNames.Any(n => openSide.ConnectsTo(n)))
                {
                    if (DebugLogging) debugLogs.Add(string.Format("Attempt to connect {0}. Tile '{1}' unsuitable. It has no connector with such name", openSide, tile.Code));
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

                if (DebugLogging && tile != null) debugLogs.Add(string.Format("For open connector '{0}' found suitable tile '{1}'", openSide, currentBestTile.Code));
                return tile;
            }

            if (DebugLogging && currentBestTile != null) debugLogs.Add(string.Format("For open connector '{0}' found suitable tile '{1}'", openSide, currentBestTile.Code));

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
            if (DebugLogging)
            {
                debugLogs.Add("=============================");
                debugLogs.Add("Enter sub dungeon generator for tile '" + dungeonTile.Code + "'");
            }

            if (dungeonTile.TileGenerator.MinTiles == 0)
            {
                logger.Warning("Child generator '{0}' has mintiles set to 0, will not generate anything", dungeonTile.TileGenerator.Code);
            }

            var childDgd = dgd.SpawnChild(dungeonTile);

            var tries = 20;
            while (tries-- > 0)
            {
                if (DebugLogging) debugLogs.Add("Sub dungeon generator for tile '" + dungeonTile.Code + "', attempt nr. " + tries + "/20");

                childDgd.Reset(openside, dgd.GeneratedStructures);

                if (TryGenerateTiles(rnd, childDgd))
                {
                    if (dungeonTile.TileGenerator.RequireClosed != null && childDgd.OpenSet.Any(s => dungeonTile.TileGenerator.RequireClosed.Any(n => s.Name.Equals(n) || s.Targets.Contains(n))))
                    {
                        if (DebugLogging)
                        {
                            var conn = childDgd.OpenSet.First(s => dungeonTile.TileGenerator.RequireClosed.Any(n => s.Name.Equals(n) || s.Targets.Contains(n)));
                            debugLogs.Add(string.Format("Sub dungeon generator for tile " + dungeonTile.Code + ", cannot complete. 'RequireClosed' Connector {0} was not closable", conn));
                        }

                        rnd.NextDouble();
                        continue;
                    }

                    childDgd.CommitToParent(DebugLogging, debugLogs);

                    return childDgd.PlaceTasks.Count - 1;
                }
            }

            if (DebugLogging) debugLogs.Add("Unable to create sub dungeon. Giving up.");
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

        public void FilterOpenSet(BlockSchematicPartial schematic, FastVec3i startPos, DungeonGenWorkspace dgd, ConnectorMetaData attachingCon)
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
                    if (DebugLogging) connector.FromSchematicForDebug = schematic.FromFile;
                    toAdd.Add(connector);
                }
            }

            dgd.OpenSet.Clear();
            dgd.OpenSet.AddRange(newOpenSet);
            dgd.OpenSet.AddRange(toAdd);


            if (DebugLogging && toAdd.Count > 0)
            {
                debugLogs.Add(string.Format("Added connectors {0} to open set", string.Join(",", toAdd.Select(conn => conn.ToString()))));
            }
        }
    }
}
