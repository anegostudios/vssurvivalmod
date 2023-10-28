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
    }

    public class GeneratableStructure
    {
        public BlockPos pos;
        public BlockSchematicStructure struc;
        public Cuboidi location;
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
        double totalWeight;

        public void Init(ICoreServerAPI api, BlockLayerConfig config, Dictionary<string, Dictionary<int, Dictionary<int, int>>> resolvedRocktypeRemapGroups, Dictionary<string, int> schematicYOffsets, int? defaultOffsetY, RockStrataConfig rockstrata, LCGRandom rand)
        {
            this.rand = rand;
            totalWeight = 0;

            for (int i = 0; i < Schematics.Length; i++)
            {
                List<BlockSchematicStructure> schematics = new List<BlockSchematicStructure>();
                IAsset[] assets;
                VillageSchematic schem = Schematics[i];

                totalWeight += schem.Weight;

                if (schem.Path.EndsWith("*"))
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
                    var sch = WorldGenStructureBase.LoadSchematic<BlockSchematicStructure>(api, assets[j], config, offsety);
                    if (sch != null) schematics.AddRange(sch);
                }

                schem.Structures = schematics.ToArray();
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


        BlockPos tmpPos = new BlockPos();
        //int climateUpLeft, climateUpRight, climateBotLeft, climateBotRight;

        public bool TryGenerate(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos, int climateUpLeft, int climateUpRight, int climateBotLeft, int climateBotRight, DidGenerate didGenerateStructure)
        {
            /*this.climateUpLeft = climateUpLeft;
            this.climateUpRight = climateUpRight;
            this.climateBotLeft = climateBotLeft;
            this.climateBotRight = climateBotRight;*/

            rand.InitPositionSeed(pos.X, pos.Z);

            float cnt = QuantityStructures.nextFloat(1, rand);
            int minQuantity = (int)cnt;
            BlockPos schemPos = pos.Copy();
            Cuboidi location = new Cuboidi();

            List<GeneratableStructure> generatables = new List<GeneratableStructure>();

            while (cnt-- > 0)
            {
                if (cnt < 1 && rand.NextFloat() > cnt) break;

                int tries = 30;
                while (tries-- > 0)
                {
                    schemPos.Set(pos);
                    schemPos.Add(rand.NextInt(50) - 25 , 0, rand.NextInt(50) - 25);
                    schemPos.Y = blockAccessor.GetTerrainMapheightAt(schemPos);

                    double rndVal = rand.NextDouble() * totalWeight;
                    int i = 0;
                    VillageSchematic schem = null;
                    while (rndVal > 0)
                    {
                        schem = Schematics[i++];
                        rndVal -= schem.Weight;
                    }
                    BlockSchematicStructure struc = GetGeneratableStructure(schem, blockAccessor, worldForCollectibleResolve, schemPos);

                    if (struc != null)
                    {
                        location.Set(schemPos.X, schemPos.Y, schemPos.Z, schemPos.X + struc.SizeX, schemPos.Y + struc.SizeY, schemPos.Z + struc.SizeZ);
                        bool intersect = false;
                        for (int k = 0; k < generatables.Count; k++)
                        {
                            if (location.IntersectsOrTouches(generatables[k].location))
                            {
                                intersect = true;
                                break;
                            }
                        }

                        if (!intersect)
                        {
                            generatables.Add(new GeneratableStructure() { struc = struc, pos = schemPos.Copy(), location = location.Clone() });
                        }
                        break;
                    }
                }
            }

            if (generatables.Count >= minQuantity)
            {
                foreach (var val in generatables)
                {
                    val.struc.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, val.pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, resolvedRockTypeRemaps, replaceblockids);
                    didGenerateStructure(val.location, val.struc);
                }

                return true;
            }

            return false;
        }


        protected BlockSchematicStructure GetGeneratableStructure(VillageSchematic schem, IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos)
        {
            int num = rand.NextInt(schem.Structures.Length);
            BlockSchematicStructure schematic = schem.Structures[num];

            int widthHalf = (int)Math.Ceiling(schematic.SizeX / 2f);
            int lengthHalf = (int)Math.Ceiling(schematic.SizeZ / 2f);

            pos.Y += schematic.OffsetY;

            // Probe all 4 corners + center if they are on the same height

            int centerY = blockAccessor.GetTerrainMapheightAt(pos);

            tmpPos.Set(pos.X - widthHalf, 0, pos.Z - lengthHalf);
            int topLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(pos.X + widthHalf, 0, pos.Z - lengthHalf);
            int topRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(pos.X - widthHalf, 0, pos.Z + lengthHalf);
            int botLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(pos.X + widthHalf, 0, pos.Z + lengthHalf);
            int botRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);


            int diff = GameMath.Max(centerY, topLeftY, topRightY, botLeftY, botRightY) - GameMath.Min(centerY, topLeftY, topRightY, botLeftY, botRightY);
            if (diff > 2) return null;

            pos.Y += centerY - pos.Y + 1 + schematic.OffsetY;
            if (pos.Y <= 0) return null;

            // Ensure not floating on water
            tmpPos.Set(pos.X - widthHalf, pos.Y - 1, pos.Z - lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            tmpPos.Set(pos.X + widthHalf, pos.Y - 1, pos.Z - lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            tmpPos.Set(pos.X - widthHalf, pos.Y - 1, pos.Z + lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            tmpPos.Set(pos.X + widthHalf, pos.Y - 1, pos.Z + lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            // Ensure not submerged in water
            tmpPos.Set(pos.X - widthHalf, pos.Y, pos.Z - lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            tmpPos.Set(pos.X + widthHalf, pos.Y, pos.Z - lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            tmpPos.Set(pos.X - widthHalf, pos.Y, pos.Z + lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            tmpPos.Set(pos.X + widthHalf, pos.Y, pos.Z + lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;



            tmpPos.Set(pos.X - widthHalf, pos.Y + 1, pos.Z - lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            tmpPos.Set(pos.X + widthHalf, pos.Y + 1, pos.Z - lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            tmpPos.Set(pos.X - widthHalf, pos.Y + 1, pos.Z + lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            tmpPos.Set(pos.X + widthHalf, pos.Y + 1, pos.Z + lengthHalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return null;

            if (!TestUndergroundCheckPositions(blockAccessor, pos, schematic.UndergroundCheckPositions)) return null;

            if (isStructureAt(pos, worldForCollectibleResolve)) return null;

            return schematic;
        }


        BlockPos utestPos = new BlockPos();
        private bool TestUndergroundCheckPositions(IBlockAccessor blockAccessor, BlockPos pos, BlockPos[] testPositionsDelta)
        {
            for (int i = 0; i < testPositionsDelta.Length; i++)
            {
                BlockPos deltapos = testPositionsDelta[i];

                utestPos.Set(pos.X + deltapos.X, pos.Y + deltapos.Y, pos.Z + deltapos.Z);

                Block block = blockAccessor.GetBlock(utestPos);
                if (block.BlockMaterial != EnumBlockMaterial.Stone && block.BlockMaterial != EnumBlockMaterial.Soil) return false;
            }

            return true;
        }

        private int CanPlacePathwayAt(IBlockAccessor blockAccessor, BlockPos[] pathway, BlockFacing towardsFacing, BlockPos targetPos)
        {
            int quantityAir = 0;
            BlockPos tmpPos = new BlockPos();

            bool oppositeDir = rand.NextInt(2) > 0;

            for (int i = 3; i >= 1; i--)
            {
                int dist = oppositeDir ? 3 - i : i;
                int dx = dist * towardsFacing.Normali.X;
                int dz = dist * towardsFacing.Normali.Z;

                quantityAir = 0;

                for (int k = 0; k < pathway.Length; k++)
                {
                    tmpPos.Set(targetPos.X + pathway[k].X + dx, targetPos.Y + pathway[k].Y, targetPos.Z + pathway[k].Z + dz);

                    Block block = blockAccessor.GetBlock(tmpPos);
                    if (block.Id == 0) quantityAir++;
                    else if (block.BlockMaterial != EnumBlockMaterial.Stone) return -1;
                }

                if (quantityAir > 0 && quantityAir < pathway.Length) return dist;
            }

            return -1;
        }

        public bool isStructureAt(BlockPos pos, IWorldAccessor world)
        {
            int rx = pos.X / world.BlockAccessor.RegionSize;
            int rz = pos.Z / world.BlockAccessor.RegionSize;

            IMapRegion mapregion = world.BlockAccessor.GetMapRegion(rx, rz);
            if (mapregion == null) return false;

            foreach (var val in mapregion.GeneratedStructures)
            {
                if (val.Location.Contains(pos))
                {
                    return true;
                }
            }

            return false;
        }
    }
}