using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Common.Collectible.Block;

#nullable disable

namespace Vintagestory.ServerMods
{
    public abstract class WorldGenStructureBase
    {
        [JsonProperty]
        public string Code;
        [JsonProperty]
        public string Name;
        [JsonProperty]
        public AssetLocation[] Schematics;
        [JsonProperty]
        public EnumStructurePlacement Placement = EnumStructurePlacement.SurfaceRuin;
        [JsonProperty]
        public NatFloat Depth = null;
        [JsonProperty]
        public bool BuildProtected = false;
        [JsonProperty]
        public string BuildProtectionDesc = null;
        [JsonProperty]
        public string BuildProtectionName = null;
        [JsonProperty]
        public bool AllowUseEveryone = true;
        [JsonProperty]
        public bool AllowTraverseEveryone = true;
        [JsonProperty]
        public int ProtectionLevel = 10;
        [JsonProperty]
        public string RockTypeRemapGroup = null; // For rocktyped ruins
        [JsonProperty]
        public Dictionary<AssetLocation, AssetLocation> RockTypeRemaps = null;  // For rocktyped ruins
        [JsonProperty]
        public AssetLocation[] InsideBlockCodes;
        [JsonProperty]
        public EnumOrigin Origin = EnumOrigin.StartPos;
        [JsonProperty]
        public int? OffsetY;
        [JsonProperty]
        public int MaxYDiff = 3;
        [JsonProperty]
        public int? StoryLocationMaxAmount;
        [JsonProperty]
        public int MinSpawnDistance = 0;
        [JsonProperty]
        public int MaxBelowSealevel = 20;
        /// <summary>
        /// This bitmask for the position in schematics
        /// </summary>
        public const uint PosBitMask = 0x3ff;

        protected T[][] LoadSchematicsWithRotations<T>(ICoreAPI api, WorldGenStructureBase struc, BlockLayerConfig config, WorldGenStructuresConfig structureConfig, Dictionary<string, int> schematicYOffsets, string pathPrefix = "schematics/", bool isDungeon = false) where T : BlockSchematicStructure
        {
            List<T[]> schematics = new List<T[]>();

            for (int i = 0; i < struc.Schematics.Length; i++)
            {
                IAsset[] assets;

                var schematicLoc = Schematics[i];

                if (struc.Schematics[i].Path.EndsWith('*'))
                {
                    assets = api.Assets.GetManyInCategory("worldgen", pathPrefix + schematicLoc.Path.Substring(0, schematicLoc.Path.Length - 1), schematicLoc.Domain).ToArray();
                }
                else
                {
                    assets = new IAsset[] { api.Assets.Get(schematicLoc.Clone().WithPathPrefixOnce("worldgen/" + pathPrefix).WithPathAppendixOnce(".json")) };
                }

                for (int j = 0; j < assets.Length; j++)
                {
                    IAsset asset = assets[j];
                    int offsety = getOffsetY(schematicYOffsets, struc.OffsetY, asset);
                    var sch = LoadSchematic<T>(api, asset, config, structureConfig, struc, offsety, isDungeon);
                    if (sch != null) schematics.Add(sch);
                }
            }

            return schematics.ToArray();
        }

        public static int getOffsetY(Dictionary<string, int> schematicYOffsets, int? defaultOffsetY, IAsset asset)
        {
            var assloc = asset.Location.PathOmittingPrefixAndSuffix("worldgen/schematics/", ".json");
            int offsety = 0;
            if (schematicYOffsets != null && schematicYOffsets.TryGetValue(assloc, out offsety)) { }
            else if (defaultOffsetY != null)
            {
                offsety = (int)defaultOffsetY;
            }

            return offsety;
        }

        public static T[] LoadSchematic<T>(ICoreAPI api, IAsset asset, BlockLayerConfig config, WorldGenStructuresConfig structureConfig, WorldGenStructureBase struc, int offsety,
            bool isDungeon = false) where T : BlockSchematicStructure
        {
            string cacheKey = asset.Location.ToShortString() + "~" + offsety;
            if (structureConfig != null && structureConfig.LoadedSchematicsCache.TryGetValue(cacheKey, out BlockSchematicStructure[] cached) && cached is T[] result) return result;

            T schematic = asset.ToObject<T>();

            if (schematic == null)
            {
                api.World.Logger.Warning("Could not load schematic {0}", asset.Location);
                if (structureConfig != null) structureConfig.LoadedSchematicsCache[cacheKey] = null;
                return null;
            }

            schematic.Remap();

            if (isDungeon)
            {
                InitDungeonData(api, schematic, asset.Location);
            }

            schematic.OffsetY = offsety;
            schematic.FromFileName = asset.Location.Domain == GlobalConstants.DefaultDomain ? asset.Name : $"{asset.Location.Domain}:{asset.Name}";
            schematic.MaxYDiff = struc?.MaxYDiff ?? 3;
            schematic.MaxBelowSealevel = struc?.MaxBelowSealevel ?? 3;
            schematic.StoryLocationMaxAmount = struc?.StoryLocationMaxAmount;
            T[] rotations = new T[4];
            rotations[0] = schematic;

            for (int k = 0; k < 4; k++)
            {
                if (k > 0)
                {
                    T unrotated = rotations[0];
                    rotations[k] = unrotated.ClonePacked() as T;
                    if (isDungeon)
                    {
                        var pathways = rotations[k].Connectors = new List<ConnectorMetaData>();
                        var pathwaysSource = unrotated.Connectors;
                        for (var index = 0; index < pathwaysSource.Count; index++)
                        {
                            var path = pathwaysSource[index];
                            var rotatedPos = unrotated.GetRotatedPos(EnumOrigin.BottomCenter, k * 90, path.Position.X, path.Position.Y, path.Position.Z);
                            pathways.Add(new ConnectorMetaData(rotatedPos, path.Facing.GetHorizontalRotated(k * 90), k, path.Name, path.Targets, null));
                        }

                        rotations[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90, null);
                    }
                }

                rotations[k].blockLayerConfig = config;
            }

            if (structureConfig != null) structureConfig.LoadedSchematicsCache[cacheKey] = rotations;
            return rotations;
        }


        private static void InitDungeonData(ICoreAPI api, BlockSchematicStructure schematic, AssetLocation pathForLogging)
        {
            var connectorBlockId = schematic.BlockCodes.First(s => s.Value.Path.Equals("meta-connector")).Key;
            schematic.Connectors = new List<ConnectorMetaData>();

            var schematicMaxX = schematic.SizeX - 1;
            var schematicMaxZ = schematic.SizeZ - 1;
            var schematicMaxY = schematic.SizeY - 1;

            var flagSum = new DungeonTileEdgeFlags();
            var connectorBlockIndices = new List<int>();
            for (var i = 0; i < schematic.Indices.Count; i++)
            {
                // if we have a none connector block in the connector block column
                // because we are at the edge we do not want to move that side inwards
                if (schematic.BlockIds[i] != connectorBlockId)
                {
                    var posIndex = schematic.Indices[i];
                    int idx = (int)(posIndex & PosBitMask);
                    int idy = (int)((posIndex >> 20) & PosBitMask);
                    int idz = (int)((posIndex >> 10) & PosBitMask);
                    if (idx == 0) flagSum.X0HasBlocks = true;
                    if (idy == 0) flagSum.Y0HasBlocks = true;
                    if (idz == 0) flagSum.Z0HasBlocks = true;
                    if (idx == schematicMaxX) flagSum.X1HasBlocks = true;
                    if (idy == schematicMaxY) flagSum.Y1HasBlocks = true;
                    if (idz == schematicMaxZ) flagSum.Z1HasBlocks = true;
                }
                else
                {
                    connectorBlockIndices.Add(i);
                }
            }

            for (int i = 0; i < connectorBlockIndices.Count; i++)
            {
                var index = schematic.Indices[connectorBlockIndices[i]];
                int dx = (int)(index & PosBitMask);
                int dy = (int)((index >> 20) & PosBitMask);
                int dz = (int)((index >> 10) & PosBitMask);

                var flags = new DungeonTileEdgeFlags();
                flags.X0 = dx == 0;
                flags.Z0 = dz == 0;
                flags.X1 = dx == schematicMaxX;
                flags.Z1 = dz == schematicMaxZ;

                bool edge = flags.X0 || flags.X1 || flags.Z0 || flags.Z1;
                flags.Y0 = dy == 0 && !edge;
                flags.Y1 = dy == schematicMaxY && !edge;

                flagSum.X0 |= flags.X0;
                flagSum.Y0 |= flags.Y0;
                flagSum.Z0 |= flags.Z0;
                flagSum.X1 |= flags.X1;
                flagSum.Y1 |= flags.Y1;
                flagSum.Z1 |= flags.Z1;

                var tree = ExtractDungeonPathConstraint(schematic, index, api.Logger);
                if (tree != null)
                {
                    schematic.Connectors.Add(new ConnectorMetaData(
                        new BlockPos(dx, dy, dz),
                        BlockFacing.ALLFACES[tree.GetInt("direction")],
                        BlockFacing.NORTH.Index,
                        tree.GetString("name"),
                        tree.GetString("target"),
                        null
                    ));
                }
            }

            // remove all pathway blocks from data
            connectorBlockIndices.Reverse();
            foreach (var i in connectorBlockIndices)
            {
                schematic.Indices.RemoveAt(i);
                schematic.BlockIds.RemoveAt(i);
            }

            if (flagSum.MoveX0) schematic.SizeX--;
            if (flagSum.MoveX1) schematic.SizeX--;
            if (flagSum.MoveZ0) schematic.SizeZ--;
            if (flagSum.MoveZ1) schematic.SizeZ--;
            if (flagSum.MoveY0) schematic.SizeY--;
            if (flagSum.MoveY1) schematic.SizeY--;

            // Move entire schematic by 1 x and 1 y if X0, Y0 or Z0 had a pathway block
            if (flagSum.MoveX0 || flagSum.MoveY0 || flagSum.MoveZ0)
            {
                for (var i = 0; i < schematic.Indices.Count; i++)
                {
                    schematic.Indices[i] = move(flagSum, schematic.Indices[i]);
                }

                for (var i = 0; i < schematic.DecorIndices.Count; i++)
                {
                    schematic.DecorIndices[i] = move(flagSum, schematic.DecorIndices[i]);
                }

                var tmpBlockEntities = new Dictionary<uint, string>();
                foreach (var (index, data) in schematic.BlockEntities)
                {
                    tmpBlockEntities[move(flagSum, index)] = data;
                }

                schematic.BlockEntities = tmpBlockEntities;

                schematic.EntitiesUnpacked.Clear();
                foreach (var entityData in schematic.Entities)
                {
                    using var ms = new MemoryStream(Ascii85.Decode(entityData));
                    var reader = new BinaryReader(ms);

                    var className = reader.ReadString();
                    var entity = api.ClassRegistry.CreateEntity(className);

                    entity.FromBytes(reader, false);

                    if (flagSum.MoveX0)
                    {
                        entity.Pos.X--;
                        entity.PositionBeforeFalling.X--;
                    }
                    if (flagSum.MoveY0)
                    {
                        entity.Pos.Y--;
                        entity.PositionBeforeFalling.Y--;
                    }
                    if (flagSum.MoveZ0)
                    {
                        entity.Pos.Z--;
                        entity.PositionBeforeFalling.Z--;
                    }

                    schematic.EntitiesUnpacked.Add(entity);
                }
                schematic.Entities.Clear();
                if (schematic.EntitiesUnpacked.Count > 0)
                {
                    using FastMemoryStream ms = new FastMemoryStream();
                    foreach (var entity in schematic.EntitiesUnpacked)
                    {
                        ms.Reset();
                        var writer = new BinaryWriter(ms);

                        writer.Write(api.ClassRegistry.GetEntityClassName(entity.GetType()));

                        entity.ToBytes(writer, false);

                        schematic.Entities.Add(Ascii85.Encode(ms.ToArray()));
                    }
                }
            }


            // move the pathway positions back inside the schematic so when can use it later with blockfacing and opposite to match the positions
            for (int i = 0; i < schematic.Connectors.Count; i++)
            {
                var path = schematic.Connectors[i];
                int dx = 0, dy = 0, dz = 0;
                if (flagSum.MoveX0 && path.Position.X > 0) dx--;
                if (flagSum.MoveY0 && path.Position.Y > 0) dy--;
                if (flagSum.MoveZ0 && path.Position.Z > 0) dz--;
                if (flagSum.MoveX1 && path.Position.X >= schematic.SizeX) dx--;
                if (flagSum.MoveY1 && path.Position.Y >= schematic.SizeY) dy--;
                if (flagSum.MoveZ1 && path.Position.Z >= schematic.SizeZ) dz--;

                path.Position.X += dx;
                path.Position.Z += dz;
                path.Position.Y += dy;
            }
        }


        public T[] LoadSchematics<T>(ICoreAPI api, AssetLocation[] locs, BlockLayerConfig config, string pathPrefix = "schematics/") where T : BlockSchematicStructure
        {
            List<T> schematics = new List<T>();

            for (int i = 0; i < locs.Length; i++)
            {
                string error = "";
                IAsset[] assets;

                var schematicLoc = Schematics[i];

                if (locs[i].Path.EndsWith('*'))
                {
                    assets = api.Assets.GetManyInCategory("worldgen", pathPrefix + schematicLoc.Path.Substring(0, schematicLoc.Path.Length - 1), schematicLoc.Domain).ToArray();
                }
                else
                {
                    assets = new IAsset[] { api.Assets.Get(schematicLoc.Clone().WithPathPrefixOnce("worldgen/" + pathPrefix).WithPathAppendixOnce(".json")) };
                }

                for (int j = 0; j < assets.Length; j++)
                {
                    IAsset asset = assets[j];

                    T schematic = asset.ToObject<T>();


                    if (schematic == null)
                    {
                        api.World.Logger.Warning("Could not load {0}: {1}", Schematics[i], error);   // error here is unused, it is always ""
                        continue;
                    }

                    schematic.FromFileName = asset.Location.Domain == GlobalConstants.DefaultDomain ? asset.Name : $"{asset.Location.Domain}:{asset.Name}";

                    schematics.Add(schematic);
                }
            }

            return schematics.ToArray();
        }


        private static uint move(DungeonTileEdgeFlags flagSum, uint index)
        {
            int dx = (int)(index & PosBitMask);
            int dy = (int)((index >> 20) & PosBitMask);
            int dz = (int)((index >> 10) & PosBitMask);
            if (flagSum.MoveX0) dx--;
            if (flagSum.MoveZ0) dz--;
            if (flagSum.MoveY0) dy--;
            return (uint)((dy << 20) | (dz << 10) | dx);
        }

        private static TreeAttribute ExtractDungeonPathConstraint(BlockSchematicStructure schematic, uint index, ILogger logger)
        {
            try
            {
                var beData = schematic.BlockEntities[index];
                var tree = schematic.DecodeBlockEntityData(beData);
                schematic.BlockEntities.Remove(index);
                return tree;
            }
            catch (Exception)
            {
                logger.Warning($"Unable to decode block entity data in schematic: {schematic.FromFileName}");
            }

            return null;
        }

        private struct DungeonTileEdgeFlags
        {
            public bool X0, Y0, Z0, X1, Y1, Z1;

            public bool X0HasBlocks, Y0HasBlocks, Z0HasBlocks, X1HasBlocks, Y1HasBlocks, Z1HasBlocks;

            public bool MoveX0 => X0 && !X0HasBlocks;
            public bool MoveY0 => Y0 && !Y0HasBlocks;
            public bool MoveZ0 => Z0 && !Z0HasBlocks;
            public bool MoveX1 => X1 && !X1HasBlocks;
            public bool MoveY1 => Y1 && !Y1HasBlocks;
            public bool MoveZ1 => Z1 && !Z1HasBlocks;
        }
    }
}
