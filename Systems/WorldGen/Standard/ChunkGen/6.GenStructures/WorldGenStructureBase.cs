using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Common.Collectible.Block;

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
        public string RockTypeRemapGroup = null; // For rocktyped ruins
        [JsonProperty]
        public Dictionary<AssetLocation, AssetLocation> RockTypeRemaps = null;  // For rocktyped ruins
        [JsonProperty]
        public AssetLocation[] InsideBlockCodes;
        [JsonProperty]
        public EnumOrigin Origin = EnumOrigin.StartPos;

        /// <summary>
        /// This bitmask for the position in schematics
        /// </summary>
        public const uint PosBitMask = 0x3ff;

        protected T[][] LoadSchematicsWithRotations<T>(ICoreAPI api, AssetLocation[] locs, BlockLayerConfig config, WorldGenStructuresConfig structureConfig, Dictionary<string, int> schematicYOffsets, int? defaultOffsetY, string pathPrefix = "schematics/", int maxYDiff = 3, bool isDungeon = false) where T : BlockSchematicStructure
        {
            List<T[]> schematics = new List<T[]>();

            for (int i = 0; i < locs.Length; i++)
            {
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
                    int offsety = getOffsetY(schematicYOffsets, defaultOffsetY, asset);
                    var sch = LoadSchematic<T>(api, asset, config, structureConfig, offsety, maxYDiff, isDungeon);
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

        public static T[] LoadSchematic<T>(ICoreAPI api, IAsset asset, BlockLayerConfig config, WorldGenStructuresConfig structureConfig, int offsety,
            int maxYDiff, bool isDungeon = false) where T : BlockSchematicStructure
        {
            string cacheKey = asset.Location.ToShortString() + "~" + offsety;
            if (structureConfig != null && structureConfig.LoadedSchematicsCache.TryGetValue(cacheKey, out BlockSchematicStructure[] cached) && cached is T[] result) return result;

            T schematic = asset.ToObject<T>();

            if (isDungeon)
            {
                InitDungeonData(api, schematic);
            }

            if (schematic == null)
            {
                api.World.Logger.Warning("Could not load schematic {0}", asset.Location);
                if (structureConfig != null) structureConfig.LoadedSchematicsCache[cacheKey] = null;
                return null;
            }

            schematic.OffsetY = offsety;
            schematic.FromFileName = asset.Name;
            schematic.MaxYDiff = maxYDiff;
            T[] rotations = new T[4];
            rotations[0] = schematic;

            for (int k = 0; k < 4; k++)
            {
                if (k > 0)
                {
                    rotations[k] = rotations[0].ClonePacked() as T;
                    if (isDungeon)
                    {
                        rotations[k].PathwayBlocksUnpacked = new List<BlockPosFacing>();
                        for (var index = 0; index < rotations[0].PathwayBlocksUnpacked.Count; index++)
                        {
                            var path = rotations[0].PathwayBlocksUnpacked[index];
                            var rotatedPos = rotations[0].GetRotatedPos(EnumOrigin.BottomCenter, k * 90, path.Position.X, path.Position.Y, path.Position.Z);
                            rotations[k].PathwayBlocksUnpacked.Add(new BlockPosFacing(rotatedPos, path.Facing.GetHorizontalRotated(k * 90), path.Constraints));
                        }
                    }
                    rotations[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90, null, isDungeon);
                }

                rotations[k].blockLayerConfig = config;
            }

            if (structureConfig != null) structureConfig.LoadedSchematicsCache[cacheKey] = rotations;
            return rotations;
        }

        private static void InitDungeonData(ICoreAPI api, BlockSchematicStructure schematic)
        {
            bool hasX = false, hasZ = false, hasXO = false, hasZO = false;
            var pathwayBlockId = schematic.BlockCodes.First(s => s.Value.Path.Equals("meta-connector")).Key;
            //check 1 side if it only contains a pathway block
            schematic.PathwayBlocksUnpacked = new List<BlockPosFacing>();
            var listIndex = new List<int>();
            for (var i = 0; i < schematic.Indices.Count; i++)
            {
                var index = schematic.Indices[i];
                int dx = (int)(index & PosBitMask);
                int dy = (int)((index >> 20) & PosBitMask);
                int dz = (int)((index >> 10) & PosBitMask);

                if (dx == 0)
                {
                    // x side
                    if (schematic.BlockIds[i] == pathwayBlockId)
                    {
                        hasX = true;
                        var constraint = ExtractDungeonPathConstraint(schematic, index);
                        schematic.PathwayBlocksUnpacked.Add(new BlockPosFacing(new BlockPos(dx,dy,dz), BlockFacing.WEST, constraint));
                        listIndex.Add(i);
                    }

                }

                // add z 0 but do not add 0,0 twice
                if (dz == 0 && dx != 0)
                {
                    if (schematic.BlockIds[i] == pathwayBlockId)
                    {
                        hasZ = true;
                        var constraint = ExtractDungeonPathConstraint(schematic, index);
                        schematic.PathwayBlocksUnpacked.Add(new BlockPosFacing(new BlockPos(dx,dy,dz), BlockFacing.NORTH,constraint));
                        listIndex.Add(i);
                    }
                }

                if (dx == schematic.SizeX - 1)
                {
                    if (schematic.BlockIds[i] == pathwayBlockId)
                    {
                        hasXO = true;
                        var constraint = ExtractDungeonPathConstraint(schematic, index);
                        schematic.PathwayBlocksUnpacked.Add(new BlockPosFacing(new BlockPos(dx,dy,dz), BlockFacing.EAST, constraint));
                        listIndex.Add(i);
                    }
                }

                if (dz == schematic.SizeZ - 1 && dx != schematic.SizeX - 1)
                {
                    if (schematic.BlockIds[i] == pathwayBlockId)
                    {
                        hasZO = true;
                        var constraint = ExtractDungeonPathConstraint(schematic, index);
                        schematic.PathwayBlocksUnpacked.Add(new BlockPosFacing(new BlockPos(dx,dy,dz), BlockFacing.SOUTH, constraint));
                        listIndex.Add(i);
                    }
                }
            }

            // remove all pathway blocks from data
            listIndex.Reverse();
            foreach (var i in listIndex)
            {
                schematic.Indices.RemoveAt(i);
                schematic.BlockIds.RemoveAt(i);
            }

            if (hasXO)
                schematic.SizeX--;
            if (hasZO)
                schematic.SizeZ--;
            if (hasX)
                schematic.SizeX--;
            if (hasZ)
                schematic.SizeZ--;

            // move entire schematic by 1 x and 1 y if at those sides was a pathway block
            for (var i = 0; i < schematic.Indices.Count; i++)
            {
                if(!hasX && !hasZ) continue;
                var index = schematic.Indices[i];
                int dx = (int)(index & PosBitMask);
                int dy = (int)((index >> 20) & PosBitMask);
                int dz = (int)((index >> 10) & PosBitMask);
                if (hasX) dx--;
                if (hasZ) dz--;

                schematic.Indices[i] = (uint)((dy << 20) | (dz << 10) | dx);
            }

            for (var i = 0; i < schematic.DecorIndices.Count; i++)
            {
                if(!hasX && !hasZ) continue;
                var index = schematic.DecorIndices[i];
                int dx = (int)(index & PosBitMask);
                int dy = (int)((index >> 20) & PosBitMask);
                int dz = (int)((index >> 10) & PosBitMask);
                if (hasX) dx--;
                if (hasZ) dz--;

                schematic.DecorIndices[i] = (uint)((dy << 20) | (dz << 10) | dx);
            }

            var tmpBlockEntities = new Dictionary<uint, string>();
            foreach (var (index, data) in schematic.BlockEntities)
            {
                if(!hasX && !hasZ) continue;
                int dx = (int)(index & PosBitMask);
                int dy = (int)((index >> 20) & PosBitMask);
                int dz = (int)((index >> 10) & PosBitMask);
                if (hasX) dx--;
                if (hasZ) dz--;

                tmpBlockEntities[(uint)((dy << 20) | (dz << 10) | dx)] = data;
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

                if (hasX)
                {
                    entity.ServerPos.X--;
                    entity.Pos.X--;
                    entity.PositionBeforeFalling.X--;
                }
                if (hasZ)
                {
                    entity.ServerPos.Z--;
                    entity.Pos.Z--;
                    entity.PositionBeforeFalling.Z--;
                }

                schematic.EntitiesUnpacked.Add(entity);
            }
            schematic.Entities.Clear();
            foreach (var entity in schematic.EntitiesUnpacked)
            {
                using var ms = new MemoryStream();
                var writer = new BinaryWriter(ms);

                writer.Write(api.ClassRegistry.GetEntityClassName(entity.GetType()));

                entity.ToBytes(writer, false);

                schematic.Entities.Add(Ascii85.Encode(ms.ToArray()));
            }

            // move the pathway positions back inside the schematic so when can use it later with blockfacing and opposite to match the positions
            for (int i = 0; i < schematic.PathwayBlocksUnpacked.Count; i++)
            {
                var path = schematic.PathwayBlocksUnpacked[i];
                var posx = 0;
                var posz = 0;
                if (hasX && path.Position.X > 0)
                    posx--;

                if (hasZ && path.Position.Z > 0)
                    posz--;

                if (hasXO && path.Position.X >= schematic.SizeX)
                    posx--;
                if (hasZO && path.Position.Z >= schematic.SizeZ)
                    posz--;

                path.Position.X += posx;
                path.Position.Z += posz;
            }
        }

        private static string ExtractDungeonPathConstraint(BlockSchematicStructure schematic, uint index)
        {
            var beData = schematic.BlockEntities[index];
            var tree = schematic.DecodeBlockEntityData(beData);
            var constraint = (tree["constraints"] as StringAttribute).value;
            schematic.BlockEntities.Remove(index);
            return constraint;
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

                    schematic.FromFileName = asset.Name;
                    schematics.Add(schematic);
                }
            }

            return schematics.ToArray();
        }

    }
}
