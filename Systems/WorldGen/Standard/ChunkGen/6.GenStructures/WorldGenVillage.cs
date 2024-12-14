using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public delegate void DidGenerate(Cuboidi location, BlockSchematicStructure schematic);

    public class VillageSchematic
    {
        public string Path;
        public int OffsetY = 0;
        public double Weight;
        public BlockSchematicStructure[] Structures;
        public int MinQuantity = 0;
        public int MaxQuantity = 9999;

        // Used by worldgen
        public int NowQuantity;

        public bool ShouldGenerate => NowQuantity < MaxQuantity;
    }

    public class GeneratableStructure
    {
        public BlockPos StartPos;
        public BlockSchematicStructure Structure;
        public Cuboidi Location;
    }

    public class WorldGenVillage
    {
        [JsonProperty]
        public string Code;
        [JsonProperty]
        public string Name;
        [JsonProperty]
        public string Group;
        [JsonProperty]
        public int MinGroupDistance = 0;
        [JsonProperty]
        public VillageSchematic[] Schematics;
        [JsonProperty]
        public float Chance = 0.05f;
        [JsonProperty]
        public int MinTemp = -30;
        [JsonProperty]
        public int MaxTemp = 40;
        [JsonProperty]
        public float MinRain = 0;
        [JsonProperty]
        public float MaxRain = 1;
        [JsonProperty]
        public float MinForest = 0;
        [JsonProperty]
        public float MaxForest = 1;
        [JsonProperty]
        public float MinY = -0.3f;
        [JsonProperty]
        public float MaxY = 1;
        [JsonProperty]
        public NatFloat QuantityStructures = NatFloat.createGauss(7, 7);
        [JsonProperty]
        public AssetLocation[] ReplaceWithBlocklayers;
        [JsonProperty]
        public bool BuildProtected = false;
        [JsonProperty]
        public bool PostPass = false;
        [JsonProperty]
        public string BuildProtectionDesc = null;
        [JsonProperty]
        public string BuildProtectionName = null;
        [JsonProperty]
        public Dictionary<AssetLocation, AssetLocation> RockTypeRemaps = null;
        [JsonProperty]
        public string RockTypeRemapGroup = null; // For rocktyped ruins
        [JsonProperty]
        public Dictionary<string, int> OffsetYByCode;

        internal int[] replaceblockids = new int[0];
        internal Dictionary<int, Dictionary<int, int>> resolvedRockTypeRemaps = null;

        LCGRandom rand;




        public void Init(ICoreServerAPI api, BlockLayerConfig blockLayerConfig, WorldGenStructuresConfig structureConfig, Dictionary<string, Dictionary<int, Dictionary<int, int>>> resolvedRocktypeRemapGroups, Dictionary<string, int> schematicYOffsets, int? defaultOffsetY, RockStrataConfig rockstrata, LCGRandom rand)
        {
            this.rand = rand;

            for (int i = 0; i < Schematics.Length; i++)
            {
                List<BlockSchematicStructure> schematics = new List<BlockSchematicStructure>();
                IAsset[] assets;
                VillageSchematic schem = Schematics[i];

                if (schem.Path.EndsWith('*'))
                {
                    assets = api.Assets.GetManyInCategory("worldgen", "schematics/" + schem.Path.Substring(0, schem.Path.Length - 1)).ToArray();
                }
                else
                {
                    assets = new IAsset[] { api.Assets.Get("worldgen/schematics/" + Schematics[i].Path + ".json") };
                }

                for (int j = 0; j < assets.Length; j++)
                {
                    int offsety = WorldGenStructureBase.getOffsetY(schematicYOffsets, defaultOffsetY, OffsetYByCode, assets[j]);
                    var sch = WorldGenStructureBase.LoadSchematic<BlockSchematicStructure>(api, assets[j], blockLayerConfig, structureConfig, offsety);
                    if (sch != null) schematics.AddRange(sch);
                }

                schem.Structures = schematics.ToArray();
                if (schem.Structures.Length == 0)
                {
                    throw new Exception(string.Format("villages.json, village with code {0} has a schematic definition at index {1} that resolves into zero schematics. Please fix or remove this entry", Code, i));
                }
            }


            if (ReplaceWithBlocklayers != null)
            {
                replaceblockids = new int[ReplaceWithBlocklayers.Length];
                for (int i = 0; i < replaceblockids.Length; i++)
                {
                    Block block = api.World.GetBlock(ReplaceWithBlocklayers[i]);
                    if (block == null)
                    {
                        throw new Exception(string.Format("Schematic with code {0} has replace block layer {1} defined, but no such block found!", Code, ReplaceWithBlocklayers[i]));
                    }
                    else
                    {
                        replaceblockids[i] = (ushort)block.Id;
                    }
                }
            }

            // For rocktyped ruins
            if (RockTypeRemapGroup != null)
            {
                resolvedRockTypeRemaps = resolvedRocktypeRemapGroups[RockTypeRemapGroup];
            }

            if (RockTypeRemaps != null)
            {
                resolvedRockTypeRemaps = WorldGenStructuresConfigBase.ResolveRockTypeRemaps(RockTypeRemaps, rockstrata, api);
            }
        }



        public bool TryGenerate(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos, int climateUpLeft, int climateUpRight, int climateBotLeft, int climateBotRight, DidGenerate didGenerateStructure)
        {
            if (!WorldGenStructure.SatisfiesMinDistance(pos, worldForCollectibleResolve, MinGroupDistance, Group)) return false;

            rand.InitPositionSeed(pos.X, pos.Z);

            float cnt = QuantityStructures.nextFloat(1, rand);
            int minQuantity = (int)cnt;
            BlockPos botCenterPos = pos.Copy();
            Cuboidi location = new Cuboidi();

            List<GeneratableStructure> generatables = new List<GeneratableStructure>();
            List<VillageSchematic> mustGenerate = new List<VillageSchematic>();
            List<VillageSchematic> canGenerate = new List<VillageSchematic>();

            for (int i = 0; i < Schematics.Length; i++)
            {
                var schem = Schematics[i];
                schem.NowQuantity = 0;

                if (schem.MinQuantity > 0)
                {
                    for (int j = 0; j < schem.MinQuantity; j++) mustGenerate.Add(schem);
                }

                if (schem.MaxQuantity > schem.MinQuantity) canGenerate.Add(schem);
            }

            while (cnt-- > 0)
            {
                if (cnt < 1 && rand.NextFloat() > cnt) break;

                int tries = 30;
                int dr = 0;
                var totalWeight = getTotalWeight(canGenerate);
                while (tries-- > 0)
                {
                    int r = Math.Min(16 + dr++ / 2, 24);

                    botCenterPos.Set(pos);
                    botCenterPos.Add(rand.NextInt(2*r) - r, 0, rand.NextInt(2*r) - r);
                    botCenterPos.Y = blockAccessor.GetTerrainMapheightAt(botCenterPos);
                    if (botCenterPos.Y == 0) continue;    // Can only be because it couldn't find a mapchunk or invalid position

                    VillageSchematic schem = null;
                    bool genRequired = mustGenerate.Count > 0;

                    if (genRequired)
                    {
                        schem = mustGenerate[mustGenerate.Count - 1];
                    }
                    else
                    {
                        double rndVal = rand.NextDouble() * totalWeight;
                        int i = 0;
                        while (rndVal > 0)
                        {
                            schem = canGenerate[i++];
                            if (schem.ShouldGenerate)
                            {
                                rndVal -= schem.Weight;
                            }
                        }
                    }

                    // First get a random structure from the VillageSchematic
                    int num = rand.NextInt(schem.Structures.Length);
                    BlockSchematicStructure struc = schem.Structures[num];

                    location.Set(
                        botCenterPos.X - struc.SizeX / 2, botCenterPos.Y, botCenterPos.Z - struc.SizeZ / 2,
                        botCenterPos.X + (int)Math.Ceiling(struc.SizeX / 2f), botCenterPos.Y + struc.SizeY, botCenterPos.Z + (int)Math.Ceiling(struc.SizeZ / 2f)
                    );

                    bool intersect = false;
                    for (int k = 0; k < generatables.Count; k++)
                    {
                        if (location.IntersectsOrTouches(generatables[k].Location))
                        {
                            intersect = true;
                            break;
                        }
                    }

                    if (intersect) continue;

                    struc.Unpack(worldForCollectibleResolve.Api);
                    if (CanGenerateStructureAt(struc, blockAccessor, location))
                    {
                        if (genRequired) mustGenerate.RemoveAt(mustGenerate.Count - 1);
                        schem.NowQuantity++;
                        generatables.Add(new GeneratableStructure() { Structure = struc, StartPos = location.Start.AsBlockPos, Location = location.Clone() });
                        tries = 0;
                    }
                }
            }

            if (generatables.Count >= minQuantity && mustGenerate.Count == 0)
            {
                foreach (var val in generatables)
                {
                    val.Structure.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, val.StartPos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, resolvedRockTypeRemaps, replaceblockids, GenStructures.ReplaceMetaBlocks);
                    didGenerateStructure(val.Location, val.Structure);
                }

                return true;
            }

            return false;
        }

        private double getTotalWeight(List<VillageSchematic> canGenerate)
        {
            double weight = 0;
            for (int i = 0; i < canGenerate.Count; i++)
            {
                var schem = canGenerate[i];
                if (schem.ShouldGenerate) weight += schem.Weight;
            }
            return weight;
        }


        protected bool CanGenerateStructureAt(BlockSchematicStructure schematic, IBlockAccessor ba, Cuboidi location)
        {
            BlockPos centerPos = new BlockPos(location.CenterX, location.Y1 + schematic.OffsetY, location.CenterZ);
            BlockPos tmpPos = new BlockPos();

            // 1. Make sure the terrain doesn't slope too much
            int topLeftY = ba.GetTerrainMapheightAt(tmpPos.Set(location.X1, 0, location.Z1));
            int topRightY = ba.GetTerrainMapheightAt(tmpPos.Set(location.X2, 0, location.Z1));
            int botLeftY = ba.GetTerrainMapheightAt(tmpPos.Set(location.X1, 0, location.Z2));
            int botRightY = ba.GetTerrainMapheightAt(tmpPos.Set(location.X2, 0, location.Z2));

            int centerY = location.Y1;
            int highestY = GameMath.Max(centerY, topLeftY, topRightY, botLeftY, botRightY);
            int lowestY = GameMath.Min(centerY, topLeftY, topRightY, botLeftY, botRightY);
            if (highestY - lowestY > 2) return false;

            // 1.5. Adjust schematic location to be at the lowest point of all 4 corners, otherwise some corners will float
            // "+1" because using the y-value from GetTerrainMapheightAt() means the structure will already be 1 block sunken into the ground. It is more intuitive for the builder when setting OffsetY=0 means the structure is placed on top of the surface
            location.Y1 = lowestY + schematic.OffsetY + 1;
            location.Y2 = location.Y1 + schematic.SizeY;

            // 2. Verify U Blocks are in solid ground
            if (!testUndergroundCheckPositions(ba, location.Start.AsBlockPos, schematic.UndergroundCheckPositions)) return false;

            // 3. Make sure not floating on, in, or under water. blockAccessor caches the current chunk so this should be decently fast in most cases
            tmpPos.Set(location.X1, centerPos.Y - 1, location.Z1);
            if (ba.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;
            if (ba.GetBlock(tmpPos.Up(), BlockLayersAccess.Fluid).IsLiquid()) return false;
            if (ba.GetBlock(tmpPos.Up(), BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(location.X2, centerPos.Y - 1, location.Z1);
            if (ba.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;
            if (ba.GetBlock(tmpPos.Up(), BlockLayersAccess.Fluid).IsLiquid()) return false;
            if (ba.GetBlock(tmpPos.Up(), BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(location.X1, centerPos.Y - 1, location.Z2);
            if (ba.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;
            if (ba.GetBlock(tmpPos.Up(), BlockLayersAccess.Fluid).IsLiquid()) return false;
            if (ba.GetBlock(tmpPos.Up(), BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(location.X2, centerPos.Y - 1, location.Z2);
            if (ba.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;
            if (ba.GetBlock(tmpPos.Up(), BlockLayersAccess.Fluid).IsLiquid()) return false;
            if (ba.GetBlock(tmpPos.Up(), BlockLayersAccess.Fluid).IsLiquid()) return false;

            // 4. May not overlap with another ruin
            if (overlapsExistingStructure(ba, location)) return false;

            return true;
        }


        protected bool testUndergroundCheckPositions(IBlockAccessor blockAccessor, BlockPos pos, BlockPos[] testPositionsDelta)
        {
            int posX = pos.X;
            int posY = pos.Y;
            int posZ = pos.Z;
            for (int i = 0; i < testPositionsDelta.Length; i++)
            {
                BlockPos deltapos = testPositionsDelta[i];
                pos.Set(posX + deltapos.X, posY + deltapos.Y, posZ + deltapos.Z);

                EnumBlockMaterial material = blockAccessor.GetBlock(pos, BlockLayersAccess.Solid).BlockMaterial;
                if (material != EnumBlockMaterial.Stone && material != EnumBlockMaterial.Soil) return false;
            }

            return true;
        }

        protected bool overlapsExistingStructure(IBlockAccessor ba, Cuboidi cuboid)
        {
            int regsize = ba.RegionSize;
            IMapRegion mapregion = ba.GetMapRegion(cuboid.CenterX / regsize, cuboid.CenterZ / regsize);
            if (mapregion == null) return false;

            foreach (var val in mapregion.GeneratedStructures)
            {
                if (val.Location.Intersects(cuboid))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
