using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.ServerMods
{
    public delegate bool TryGenerateHandler(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos, string locationCode);

    public enum EnumStructurePlacement
    {
        SurfaceRuin,
        Surface,
        Underwater,
        Underground
    }

    public class WorldGenStoryStructure : WorldGenStructureBase
    {
        [JsonProperty]
        public string Group;
        [JsonProperty]
        public string RequireLandform;
        [JsonProperty]
        public int LandformRadius;
        [JsonProperty]
        public int GenerationRadius;
        [JsonProperty]
        public string DependsOnStructure;
        [JsonProperty]
        public int MinSpawnDistX;
        [JsonProperty]
        public int MaxSpawnDistX;
        [JsonProperty]
        public int MinSpawnDistZ;
        [JsonProperty]
        public int MaxSpawnDistZ;
        [JsonProperty]
        public int ExtraLandClaimX;
        [JsonProperty]
        public int ExtraLandClaimZ;

        [JsonProperty]
        public Dictionary<string, int> SkipGenerationCategories;
        public Dictionary<int, int> SkipGenerationFlags;

        [JsonProperty]
        public int? ForceRain;

        [JsonProperty]
        public int? ForceTemperature;

        [JsonProperty]
        public bool GenerateGrass;

        [JsonProperty]
        public Cuboidi[] CustomLandClaims;

        [JsonProperty]
        public bool ExcludeSchematicSizeProtect;

        [JsonProperty]
        public bool UseWorldgenHeight;


        internal BlockSchematicPartial schematicData;

        [JsonProperty]
        public AssetLocation[] ReplaceWithBlocklayers;
        internal int[] replacewithblocklayersBlockids = Array.Empty<int>();

        internal Dictionary<int, Dictionary<int, int>> resolvedRockTypeRemaps = null;

        [JsonProperty]
        public bool DisableSurfaceTerrainBlending;

        public void Init(ICoreServerAPI api, WorldGenStoryStructuresConfig scfg, RockStrataConfig rockstrata, BlockLayerConfig blockLayerConfig)
        {
            schematicData = LoadSchematics<BlockSchematicPartial>(api, Schematics, null)[0];
            // schematicData.Init(api.World.BlockAccessor);     // radfast note: do not Init() here for performance; .Init() will be called in BlockSchematicStructure.Unpack()
            schematicData.blockLayerConfig = blockLayerConfig;

            scfg.SchematicYOffsets.TryGetValue("story/" + schematicData.FromFileName.Replace(".json", ""), out var offset);
            schematicData.OffsetY = offset;

            if (SkipGenerationCategories != null)
            {
                SkipGenerationFlags = new Dictionary<int, int>();
                foreach (var category in SkipGenerationCategories)
                {
                    SkipGenerationFlags.Add(BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(category.Key.ToLowerInvariant()))), category.Value);
                }
            }

            // For rocktyped structures
            if (RockTypeRemapGroup != null)
            {
                resolvedRockTypeRemaps = scfg.resolvedRocktypeRemapGroups[RockTypeRemapGroup];
            }

            if (RockTypeRemaps != null)
            {
                if (resolvedRockTypeRemaps != null)
                {
                    var ownRemaps = WorldGenStructuresConfigBase.ResolveRockTypeRemaps(RockTypeRemaps, rockstrata, api);
                    foreach (var val in resolvedRockTypeRemaps)
                    {
                        ownRemaps[val.Key] = val.Value;
                    }

                    resolvedRockTypeRemaps = ownRemaps;
                }
                else
                {
                    resolvedRockTypeRemaps = WorldGenStructuresConfigBase.ResolveRockTypeRemaps(RockTypeRemaps, rockstrata, api);
                }
            }

            if (ReplaceWithBlocklayers != null)
            {
                replacewithblocklayersBlockids = new int[ReplaceWithBlocklayers.Length];
                for (var i = 0; i < replacewithblocklayersBlockids.Length; i++)
                {
                    var block = api.World.GetBlock(ReplaceWithBlocklayers[i]);
                    if (block == null)
                    {
                        throw new Exception(string.Format("Schematic with code {0} has replace block layer {1} defined, but no such block found!",
                            Code, ReplaceWithBlocklayers[i]));
                    }

                    replacewithblocklayersBlockids[i] = block.Id;
                }
            }
        }
    }

    public class WorldGenStructure : WorldGenStructureBase
    {
        [JsonProperty]
        public string Group;
        [JsonProperty]
        public int MinGroupDistance = 0;
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
        public AssetLocation[] ReplaceWithBlocklayers;
        [JsonProperty]
        public bool PostPass = false;
        [JsonProperty]
        public bool SuppressTrees = false;
        [JsonProperty]
        public bool SuppressWaterfalls = false;
        [JsonProperty]
        public int StoryMaxFromCenter = 0;


        internal BlockSchematicStructure[][] schematicDatas;
        internal int[] replacewithblocklayersBlockids = Array.Empty<int>();
        internal HashSet<int> insideblockids = new HashSet<int>();

        internal Dictionary<int, Dictionary<int, int>> resolvedRockTypeRemaps = null;

        TryGenerateHandler[] Generators;

        public WorldGenStructure()
        {
            Generators = new TryGenerateHandler[]
            {
                TryGenerateRuinAtSurface,
                TryGenerateAtSurface,
                TryGenerateUnderwater,
                TryGenerateUnderground
            };
        }

        LCGRandom rand;

        int unscaledMinRain;
        int unscaledMaxRain;
        int unscaledMinTemp;
        int unscaledMaxTemp;

        GenStructures genStructuresSys;

        public void Init(ICoreServerAPI api, BlockLayerConfig config, RockStrataConfig rockstrata, WorldGenStructuresConfig structureConfig, LCGRandom rand)
        {
            this.rand = rand;

            genStructuresSys = api.ModLoader.GetModSystem<GenStructures>();

            unscaledMinRain = (int)(MinRain * 255);
            unscaledMaxRain = (int)(MaxRain * 255);
            unscaledMinTemp = Climate.DescaleTemperature(MinTemp);
            unscaledMaxTemp = Climate.DescaleTemperature(MaxTemp);


            this.schematicDatas = LoadSchematicsWithRotations<BlockSchematicStructure>(api, this, config, structureConfig, structureConfig.SchematicYOffsets);

            if (ReplaceWithBlocklayers != null)
            {
                replacewithblocklayersBlockids = new int[ReplaceWithBlocklayers.Length];
                for (int i = 0; i < replacewithblocklayersBlockids.Length; i++)
                {
                    Block block = api.World.GetBlock(ReplaceWithBlocklayers[i]);
                    if (block == null)
                    {
                        throw new Exception(string.Format("Schematic with code {0} has replace block layer {1} defined, but no such block found!", Code, ReplaceWithBlocklayers[i]));
                    } else
                    {
                        replacewithblocklayersBlockids[i] = block.Id;
                    }

                }
            }

            if (InsideBlockCodes != null)
            {
                for (int i = 0; i < InsideBlockCodes.Length; i++)
                {
                    Block block = api.World.GetBlock(InsideBlockCodes[i]);
                    if (block == null)
                    {
                        throw new Exception(string.Format("Schematic with code {0} has inside block {1} defined, but no such block found!", Code, InsideBlockCodes[i]));
                    }
                    else
                    {
                        insideblockids.Add(block.Id);
                    }

                }
            }

            // For rocktyped ruins
            if (RockTypeRemapGroup != null)
            {
                resolvedRockTypeRemaps = structureConfig.resolvedRocktypeRemapGroups[RockTypeRemapGroup];
            }

            if (RockTypeRemaps != null)
            {
                if (resolvedRockTypeRemaps != null)
                {
                    var ownRemaps = WorldGenStructuresConfigBase.ResolveRockTypeRemaps(RockTypeRemaps, rockstrata, api);
                    foreach (var val in resolvedRockTypeRemaps)
                    {
                        ownRemaps[val.Key] = val.Value;
                    }
                    resolvedRockTypeRemaps = ownRemaps;
                } else
                {
                    resolvedRockTypeRemaps = WorldGenStructuresConfigBase.ResolveRockTypeRemaps(RockTypeRemaps, rockstrata, api);
                }

            }
        }



        BlockPos tmpPos = new BlockPos();
        public Cuboidi LastPlacedSchematicLocation = new Cuboidi();
        public BlockSchematicStructure LastPlacedSchematic;
        int climateUpLeft, climateUpRight, climateBotLeft, climateBotRight;

        internal bool TryGenerate(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, int climateUpLeft,
            int climateUpRight, int climateBotLeft, int climateBotRight, string locationCode)
        {
            this.climateUpLeft = climateUpLeft;
            this.climateUpRight = climateUpRight;
            this.climateBotLeft = climateBotLeft;
            this.climateBotRight = climateBotRight;
            const int chunksize = GlobalConstants.ChunkSize;

            int climate = GameMath.BiLerpRgbColor((float)(startPos.X % chunksize) / chunksize, (float)(startPos.Z % chunksize) / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

            int rain = Climate.GetRainFall((climate >> 8) & 0xff, startPos.Y);
            int unscaledtempsealevel = (climate >> 16) & 0xff;

            int temp = Climate.GetScaledAdjustedTemperature(unscaledtempsealevel, startPos.Y - TerraGenConfig.seaLevel);
            int unscaledtemp = Climate.DescaleTemperature(temp);

            if (rain < unscaledMinRain || rain > unscaledMaxRain || unscaledtemp < unscaledMinTemp || unscaledtemp > unscaledMaxTemp) return false;

            // Hardcoding crime here. Please don't look. Takes these tasty cookies as a bribe. (Prevent generation of schematics on glaciers)
            if (unscaledtemp < 20 && startPos.Y > worldForCollectibleResolve.SeaLevel + 15) return false;

            rand.InitPositionSeed(startPos.X, startPos.Z);

            bool generated = Generators[(int)Placement](blockAccessor, worldForCollectibleResolve, startPos, locationCode);

            if (generated && Placement == EnumStructurePlacement.SurfaceRuin)
            {
                float rainValMoss = Math.Max(0, (rain - 50) / 255f);
                float tempValMoss = Math.Max(0, (unscaledtemp - 50) / 255f);
                float mossGrowthChance = 1.5f * rainValMoss * tempValMoss + 1f * rainValMoss * GameMath.Clamp((tempValMoss + 0.33f) / 1.33f, 0, 1);

                int mossTries = (int)(10 * mossGrowthChance * GameMath.Sqrt(LastPlacedSchematicLocation.SizeXYZ));
                int sizex = LastPlacedSchematic.SizeX;
                int sizey = LastPlacedSchematic.SizeY;
                int sizez = LastPlacedSchematic.SizeZ;
                BlockPos tmpPos = new BlockPos(startPos.dimension);

                Block mossDecor = blockAccessor.GetBlock(new AssetLocation("attachingplant-spottymoss"));

                while (mossTries-- > 0)
                {
                    int dx = rand.NextInt(sizex);
                    int dy = rand.NextInt(sizey);
                    int dz = rand.NextInt(sizez);
                    tmpPos.Set(startPos.X + dx, startPos.Y + dy, startPos.Z + dz);
                    var block = blockAccessor.GetBlock(tmpPos);
                    if (block.BlockMaterial == EnumBlockMaterial.Stone)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            var face = BlockFacing.ALLFACES[i];
                            if (!block.SideSolid[i]) continue;

                            var nblock = blockAccessor.GetBlockOnSide(tmpPos, face);
                            if (!nblock.SideSolid[face.Opposite.Index])
                            {
                                blockAccessor.SetDecor(mossDecor, tmpPos, face);
                                break;
                            }
                        }
                    }
                }
            }

            return generated;
        }

        /// <summary>
        /// Finds the side with lowest summed height values along the border of the to be placed schematic.
        /// The search is done on each side +2.
        /// Requires <see cref="BlockSchematic.EntranceRotation"/>
        /// </summary>
        /// <param name="blockAccessor"></param>
        /// <param name="schematics">array of schematics[4] N=0, E=1, S=2, W=3</param>
        /// <param name="pos">start position</param>
        /// <returns>The index for the rotation of the desired side to pick from the <see cref="schematicDatas"/>[num][rotation] North=0 East=1 South=2 West=3</returns>
        private int FindClearEntranceRotation(IBlockAccessor blockAccessor, BlockSchematicStructure[] schematics,  BlockPos pos)
        {
            const int chunksize = GlobalConstants.ChunkSize;
            var schematic = schematics[0];
            var entranceRot = GameMath.Clamp(schematics[0].EntranceRotation / 90, 0, 3);
            // pos is in the corner and not centered
            var minX = pos.X-2;
            var maxX = pos.X + schematic.SizeX+2;
            var minZ = pos.Z-2;
            var maxZ = pos.Z + schematic.SizeZ+2;

            // used to detect whether downwards slope is East-West etc
            int weightedHeightW = 1, weightedHeightE = 1, weightedHeightN = 1, weightedHeightS = 1;
            var x = minX;
            int z;

            IMapChunk mapchunk;
            int lowSide;
            // entrance is east or west
            if (entranceRot == 1 || entranceRot == 3)
            {
                for (z = minZ; z <= maxZ; z++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    weightedHeightW += h;
                }

                x = maxX;
                for (z = minZ; z <= maxZ; z++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    weightedHeightE += h;
                }
            } else if (entranceRot == 0 || entranceRot == 2) // entrance is north or south
            {
                z = minZ;
                for (x = minX; x <= maxX; x++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    weightedHeightN += h;
                }

                z = maxZ;
                for (x = minX; x <= maxX; x++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    weightedHeightS += h;
                }
            }

            // check 2nd rot - rotate the schematic once by 90
            schematic = schematics[1];
            var entranceRot2 = GameMath.Clamp(schematic.EntranceRotation / 90, 0, 3);
            // pos is in the corner and not centered
            minX = pos.X-2;
            maxX = pos.X + schematic.SizeX+2;
            minZ = pos.Z-2;
            maxZ = pos.Z + schematic.SizeZ+2;
            int weightedHeightW2 = 1, weightedHeightE2 = 1, weightedHeightN2 = 1, weightedHeightS2 = 1;

            // entrance is east or west
            if (entranceRot2 == 1 || entranceRot2 == 3)
            {
                for (z = minZ; z <= maxZ; z++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    weightedHeightW2 += h;
                }

                x = maxX;
                for (z = minZ; z <= maxZ; z++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    weightedHeightE2 += h;
                }
            } else if (entranceRot2 == 0 || entranceRot2 == 2) // entrance is north or south
            {
                z = minZ;
                for (x = minX; x <= maxX; x++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    weightedHeightN2 += h;
                }

                z = maxZ;
                for (x = minX; x <= maxX; x++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    weightedHeightS2 += h;
                }
            }

            // entranceRot E/W
            if (entranceRot == 1 || entranceRot == 3)
            {
                // so entranceRot2 must be N/S
                if (weightedHeightE < weightedHeightW)
                {
                    if (weightedHeightN2 < weightedHeightS2)
                    {
                        lowSide = weightedHeightE < weightedHeightN2 ? 1 : 0;
                    }
                    else
                    {
                        lowSide = weightedHeightE < weightedHeightS2 ? 1 : 2;
                    }
                }
                else // weightedHeightE > weightedHeightW
                {
                    if (weightedHeightN2 < weightedHeightS2)
                    {
                        lowSide = weightedHeightW < weightedHeightN2 ? 3 : 0;
                    }
                    else
                    {
                        lowSide = weightedHeightW < weightedHeightS2 ? 3 : 2;
                    }
                }
            }
            else // entranceRot N/S
            {
                // so entranceRot2 must be E/W
                if (weightedHeightN < weightedHeightS)
                {
                    if (weightedHeightE2 < weightedHeightW2)
                    {
                        lowSide = weightedHeightN < weightedHeightE2 ? 0 : 1;
                    }
                    else // weightedHeightE2 > weightedHeightW2
                    {
                        lowSide = weightedHeightN < weightedHeightW2 ? 0 : 3;
                    }
                }
                else // weightedHeightN > weightedHeightS
                {
                    if (weightedHeightE2 < weightedHeightW2)
                    {
                        lowSide = weightedHeightS < weightedHeightE2 ? 2 : 1;
                    }
                    else
                    {
                        lowSide = weightedHeightS < weightedHeightW2 ? 0 : 3;
                    }
                }
            }

            // calculate the index to get the correct rotation offset by entranceRot (eg. entranceRot = 1, lowestSide = 2 => 1, e: 3, l: 2 => 3)
            return (4 + lowSide - entranceRot) % 4;
        }

        internal bool TryGenerateRuinAtSurface(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, string locationCode)
        {
            if (schematicDatas.Length == 0) return false;
            int num = rand.NextInt(schematicDatas.Length);
            int orient = rand.NextInt(4);
            BlockSchematicStructure schematic = schematicDatas[num][orient];
            schematic.Unpack(worldForCollectibleResolve.Api, orient);

            startPos = startPos.AddCopy(0, schematic.OffsetY, 0);

            if (schematic.EntranceRotation != -1)
            {
                orient = FindClearEntranceRotation(blockAccessor, schematicDatas[num], startPos);
                schematic = schematicDatas[num][orient];
                schematic.Unpack(worldForCollectibleResolve.Api, orient);
            }

            int wdthalf = (int)Math.Ceiling(schematic.SizeX / 2f);
            int lenhalf = (int)Math.Ceiling(schematic.SizeZ / 2f);

            int wdt = schematic.SizeX;
            int len = schematic.SizeZ;

            tmpPos.Set(startPos.X + wdthalf, 0, startPos.Z + lenhalf);
            int centerY = blockAccessor.GetTerrainMapheightAt(startPos);

            // check if we are to deep underwater
            if (centerY < worldForCollectibleResolve.SeaLevel - MaxBelowSealevel)
                return false;
            // Probe all 4 corners + center if they either touch the surface or are sightly below ground

            tmpPos.Set(startPos.X, 0, startPos.Z);
            int topLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(startPos.X + wdt, 0, startPos.Z);
            int topRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(startPos.X, 0, startPos.Z + len);
            int botLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(startPos.X + wdt, 0, startPos.Z + len);
            int botRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            int maxY = GameMath.Max(centerY, topLeftY, topRightY, botLeftY, botRightY);
            int minY = GameMath.Min(centerY, topLeftY, topRightY, botLeftY, botRightY);

            // improve flatness check for larger structures
            if (schematic.SizeX >= 30)
            {
                var size = (int)(schematic.SizeX * 0.15 + 8);
                for (int i = size; i < schematic.SizeX; i+=size)
                {
                    tmpPos.Set(startPos.X + i, 0, startPos.Z);
                    var topSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                    tmpPos.Set(startPos.X + i, 0, startPos.Z + len);
                    var botSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                    tmpPos.Set(startPos.X + i, 0, startPos.Z + len / 2);
                    var centerSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                    maxY = GameMath.Max(maxY, topSide, botSide, centerSide);
                    minY = GameMath.Min(minY, topSide, botSide, centerSide);
                }
            }
            else if (schematic.SizeX >= 15) // check center on X
            {
                var size = schematic.SizeX / 2;
                tmpPos.Set(startPos.X + size, 0, startPos.Z);
                var topSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                tmpPos.Set(startPos.X + size, 0, startPos.Z + len);
                var botSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                tmpPos.Set(startPos.X + size, 0, startPos.Z + len / 2);
                var centerSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                maxY = GameMath.Max(maxY, topSide, botSide, centerSide);
                minY = GameMath.Min(minY, topSide, botSide, centerSide);
            }
            if (schematic.SizeZ >= 30)
            {
                var size = (int)(schematic.SizeZ * 0.15 + 8);
                for (int i = size; i < schematic.SizeZ; i+=size)
                {
                    tmpPos.Set(startPos.X + wdt, 0, startPos.Z + i);
                    var rightSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                    tmpPos.Set(startPos.X, 0, startPos.Z + i);
                    var leftSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                    tmpPos.Set(startPos.X + wdt / 2, 0, startPos.Z + i);
                    var centerSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                    maxY = GameMath.Max(maxY, rightSide, leftSide, centerSide);
                    minY = GameMath.Min(minY, rightSide, leftSide, centerSide);
                }
            }
            else if (schematic.SizeZ >= 15) // check center on Z
            {
                var size = schematic.SizeZ / 2;
                tmpPos.Set(startPos.X + wdt, 0, startPos.Z + size);
                var rightSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                tmpPos.Set(startPos.X, 0, startPos.Z + size);
                var leftSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                tmpPos.Set(startPos.X + wdt / 2, 0, startPos.Z + size);
                var centerSide = blockAccessor.GetTerrainMapheightAt(tmpPos);

                maxY = GameMath.Max(maxY, rightSide, leftSide, centerSide);
                minY = GameMath.Min(minY, rightSide, leftSide, centerSide);
            }

            int diff = Math.Abs(maxY - minY);
            if (diff > schematic.MaxYDiff) return false;

            startPos.Y = minY + schematic.OffsetY;


            // Ensure not deeply submerged in water  =>  actually, that's now OK!


            tmpPos.Set(startPos.X, startPos.Y + 1, startPos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y + 1, startPos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X, startPos.Y + 1, startPos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y + 1, startPos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            // Generating exactly at "TerrainMapheightAt" means we will place it 1 block below ground.
            // With a offsetY of 0, the structure should literally be fully above ground.
            // But offsetY default is -1 so it will be just a block below the surface unless specified otherwise
            startPos.Y++;

            // if we have above ground blocks check if they are above the ground
            if (!TestAboveGroundCheckPositions(blockAccessor, startPos, schematic.AbovegroundCheckPositions)) return false;

            if (!SatisfiesMinDistance(startPos, worldForCollectibleResolve)) return false;
            if (WouldOverlapAt(blockAccessor,startPos, schematic, locationCode)) return false;

            LastPlacedSchematicLocation.Set(startPos.X, startPos.Y, startPos.Z, startPos.X + schematic.SizeX, startPos.Y + schematic.SizeY, startPos.Z + schematic.SizeZ);
            LastPlacedSchematic = schematic;

            schematic.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, startPos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, resolvedRockTypeRemaps, replacewithblocklayersBlockids, GenStructures.ReplaceMetaBlocks);

            return true;
        }

        internal bool TryGenerateAtSurface(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, string locationCode)
        {
            int num = rand.NextInt(schematicDatas.Length);
            int orient = rand.NextInt(4);
            BlockSchematicStructure schematic = schematicDatas[num][orient];
            schematic.Unpack(worldForCollectibleResolve.Api, orient);

            startPos = startPos.AddCopy(0, schematic.OffsetY, 0);

            if (schematic.EntranceRotation != -1)
            {
                orient = FindClearEntranceRotation(blockAccessor, schematicDatas[num], startPos);
                schematic = schematicDatas[num][orient];
                schematic.Unpack(worldForCollectibleResolve.Api, orient);
            }

            int wdthalf = (int)Math.Ceiling(schematic.SizeX / 2f);
            int lenhalf = (int)Math.Ceiling(schematic.SizeZ / 2f);
            int wdt = schematic.SizeX;
            int len = schematic.SizeZ;


            tmpPos.Set(startPos.X + wdthalf, 0, startPos.Z + lenhalf);
            int centerY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            // check if we are to deep underwater
            if (centerY < worldForCollectibleResolve.SeaLevel - MaxBelowSealevel)
                return false;

            // Probe all 4 corners + center if they are on the same height
            tmpPos.Set(startPos.X, 0, startPos.Z);
            int topLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(startPos.X + wdt, 0, startPos.Z);
            int topRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(startPos.X, 0, startPos.Z + len);
            int botLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(startPos.X + wdt, 0, startPos.Z + len);
            int botRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            // Is the ground flat?
            int diff = GameMath.Max(centerY, topLeftY, topRightY, botLeftY, botRightY) - GameMath.Min(centerY, topLeftY, topRightY, botLeftY, botRightY);
            if (diff != 0) return false;

            startPos.Y = centerY + 1 + schematic.OffsetY;

            // Ensure not floating on water
            tmpPos.Set(startPos.X + wdthalf, startPos.Y - 1, startPos.Z + lenhalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X, startPos.Y - 1, startPos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y - 1, startPos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X, startPos.Y - 1, startPos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y - 1, startPos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            // Ensure not submerged in water
            tmpPos.Set(startPos.X, startPos.Y, startPos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;


            tmpPos.Set(startPos.X + wdt, startPos.Y, startPos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y, startPos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X, startPos.Y, startPos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y, startPos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;



            tmpPos.Set(startPos.X, startPos.Y + 1, startPos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y + 1, startPos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X, startPos.Y + 1, startPos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y + 1, startPos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;


            if (!SatisfiesMinDistance(startPos, worldForCollectibleResolve)) return false;
            if (WouldOverlapAt(blockAccessor, startPos, schematic, locationCode)) return false;

            LastPlacedSchematicLocation.Set(startPos.X, startPos.Y, startPos.Z, startPos.X + schematic.SizeX, startPos.Y + schematic.SizeY, startPos.Z + schematic.SizeZ);
            LastPlacedSchematic = schematic;
            schematic.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, startPos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, resolvedRockTypeRemaps, replacewithblocklayersBlockids, GenStructures.ReplaceMetaBlocks);
            return true;
        }



        internal bool TryGenerateUnderwater(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos, string locationCode)
        {
            return false;
        }

        internal bool TryGenerateUnderground(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos, string locationCode)
        {
            int num = rand.NextInt(schematicDatas.Length);

            BlockSchematicStructure[] schematicStruc = schematicDatas[num];
            BlockPos targetPos = pos.Copy();
            schematicStruc[0].Unpack(worldForCollectibleResolve.Api, 0);

            if (schematicStruc[0].PathwayStarts.Length > 0)
            {
                return tryGenerateAttachedToCave(blockAccessor, worldForCollectibleResolve, schematicStruc, targetPos, locationCode);
            }

            int orient = rand.NextInt(4);
            BlockSchematicStructure schematic = schematicStruc[orient];
            schematic.Unpack(worldForCollectibleResolve.Api, orient);

            BlockPos placePos = schematic.AdjustStartPos(targetPos.Copy(), Origin);

            LastPlacedSchematicLocation.Set(placePos.X, placePos.Y, placePos.Z, placePos.X + schematic.SizeX, placePos.Y + schematic.SizeY, placePos.Z + schematic.SizeZ);
            LastPlacedSchematic = schematic;

            if (insideblockids.Count > 0 && !insideblockids.Contains(blockAccessor.GetBlock(targetPos).Id)) return false;
            if (!TestUndergroundCheckPositions(blockAccessor, placePos, schematic.UndergroundCheckPositions)) return false;
            if (!SatisfiesMinDistance(pos, worldForCollectibleResolve)) return false;
            if (WouldOverlapAt(blockAccessor, pos, schematic, locationCode)) return false;

            if (resolvedRockTypeRemaps != null)
            {
                Block rockBlock = null;

                for (int i = 0; rockBlock == null && i < 10; i++)
                {
                    var block = blockAccessor.GetBlockRaw(
                        placePos.X + rand.NextInt(schematic.SizeX),
                        placePos.Y + rand.NextInt(schematic.SizeY),
                        placePos.Z + rand.NextInt(schematic.SizeZ),
                        BlockLayersAccess.Solid
                    );

                    if (block.BlockMaterial == EnumBlockMaterial.Stone) rockBlock = block;
                }

                schematic.PlaceReplacingBlocks(blockAccessor, worldForCollectibleResolve, placePos, schematic.ReplaceMode, resolvedRockTypeRemaps, rockBlock?.Id, GenStructures.ReplaceMetaBlocks);

            } else
            {
                schematic.Place(blockAccessor, worldForCollectibleResolve, targetPos);
            }

            return true;
        }

        private bool tryGenerateAttachedToCave(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockSchematicStructure[] schematicStruc, BlockPos targetPos, string locationCode)
        {
            // 1. Give up if non air block or mapheight is not at least 4 blocks higher
            // 2. Search up to 4 blocks downwards. Give up if no stone is found.
            // 3. Select one pathway randomly
            // 4. For every horizontal orientation
            //    - Get the correctly rotated version for this pathway
            //    - Starting at 2 blocks away, move one block closer each iteration
            //      - Check if
            //        - at every pathway block pos there is stone or air
            //        - at least one pathway block has an air block facing towards center?
            //      - If yes, remove the blocks that are in the way and place schematic

            Block rockBlock = null;
            Block block = blockAccessor.GetBlock(targetPos);
            if (block.Id != 0) return false;

            // 1./2. Search an underground position that has air and a stone floor below
            bool found = false;
            for (int dy = 0; dy <= 4; dy++)
            {
                targetPos.Down();
                block = blockAccessor.GetBlock(targetPos);

                if (block.BlockMaterial == EnumBlockMaterial.Stone)
                {
                    rockBlock = block;
                    targetPos.Up();
                    found = true;
                    break;
                }
            }

            if (!found) return false;

            // 3. Random pathway
            BlockSchematicStructure schematic = schematicStruc[0];
            schematic.Unpack(worldForCollectibleResolve.Api, 0);
            int pathwayNum = rand.NextInt(schematic.PathwayStarts.Length);
            int targetDistance = -1;
            BlockFacing targetFacing = null;
            BlockPos[] pathway = null;

            // 4. At that position search for a suitable stone wall in any direction
            for (int targetOrientation = 0; targetOrientation < 4; targetOrientation++)
            {
                schematic = schematicStruc[targetOrientation];
                schematic.Unpack(worldForCollectibleResolve.Api, targetOrientation);
                // Try every rotation
                pathway = schematic.PathwayOffsets[pathwayNum];
                // This is the facing we are currently checking
                targetFacing = schematic.PathwaySides[pathwayNum];

                targetDistance = CanPlacePathwayAt(blockAccessor, pathway, targetFacing, targetPos);
                if (targetDistance != -1) break;
            }

            if (targetDistance == -1) return false;

            BlockPos pathwayStart = schematic.PathwayStarts[pathwayNum];

            // Move back the structure so that the door aligns to the cave wall
            targetPos.Add(
                -pathwayStart.X - targetFacing.Normali.X * targetDistance,
                -pathwayStart.Y - targetFacing.Normali.Y * targetDistance + schematic.OffsetY,
                -pathwayStart.Z - targetFacing.Normali.Z * targetDistance
            );

            if (targetPos.Y <= 0) return false;
            if (!TestUndergroundCheckPositions(blockAccessor, targetPos, schematic.UndergroundCheckPositions)) return false;
            if (WouldOverlapAt(blockAccessor, targetPos, schematic, locationCode)) return false;

            LastPlacedSchematicLocation.Set(targetPos.X, targetPos.Y, targetPos.Z, targetPos.X + schematic.SizeX, targetPos.Y + schematic.SizeY, targetPos.Z + schematic.SizeZ);
            LastPlacedSchematic = schematic;

            if (resolvedRockTypeRemaps != null)
            {
                schematic.PlaceReplacingBlocks(blockAccessor, worldForCollectibleResolve, targetPos, schematic.ReplaceMode, resolvedRockTypeRemaps, rockBlock.Id, GenStructures.ReplaceMetaBlocks);
            }
            else
            {
                schematic.Place(blockAccessor, worldForCollectibleResolve, targetPos);
            }

            // Free up a layer of blocks in front of the door
            ushort blockId = 0; // blockAccessor.GetBlock(new AssetLocation("creativeblock-37")).BlockId;
            for (int i = 0; i < pathway.Length; i++)
            {
                for (int d = 0; d <= targetDistance; d++)
                {
                    tmpPos.Set(
                        targetPos.X + pathwayStart.X + pathway[i].X + (d + 1) * targetFacing.Normali.X,
                        targetPos.Y + pathwayStart.Y + pathway[i].Y + (d + 1) * targetFacing.Normali.Y,
                        targetPos.Z + pathwayStart.Z + pathway[i].Z + (d + 1) * targetFacing.Normali.Z
                    );

                    blockAccessor.SetBlock(blockId, tmpPos);
                }
            }

            return true;
        }


        BlockPos utestPos = new BlockPos();
        private bool TestUndergroundCheckPositions(IBlockAccessor blockAccessor, BlockPos pos, BlockPos[] testPositionsDelta)
        {
            for (int i = 0; i < testPositionsDelta.Length; i++)
            {
                BlockPos deltapos = testPositionsDelta[i];

                utestPos.Set(pos.X + deltapos.X, pos.Y + deltapos.Y, pos.Z + deltapos.Z);

                Block block = blockAccessor.GetBlock(utestPos);
                if (block.BlockMaterial != EnumBlockMaterial.Stone) return false;
            }

            return true;
        }

        private bool TestAboveGroundCheckPositions(IBlockAccessor blockAccessor, BlockPos pos, BlockPos[] testPositionsDelta)
        {
            for (int i = 0; i < testPositionsDelta.Length; i++)
            {
                BlockPos deltapos = testPositionsDelta[i];

                utestPos.Set(pos.X + deltapos.X, pos.Y + deltapos.Y, pos.Z + deltapos.Z);

                var height = blockAccessor.GetTerrainMapheightAt(utestPos);
                if (utestPos.Y <= height) return false;
            }

            return true;
        }

        private int CanPlacePathwayAt(IBlockAccessor blockAccessor, BlockPos[] pathway, BlockFacing towardsFacing, BlockPos targetPos)
        {
            int quantityAir;
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


        static Cuboidi tmpLoc = new Cuboidi();

        private bool WouldOverlapAt(IBlockAccessor blockAccessor, BlockPos pos, BlockSchematicStructure schematic, string locationCode)
        {
            int regSize = blockAccessor.RegionSize;

            int mapRegionSizeX = blockAccessor.MapSizeX / regSize;
            int mapRegionSizeZ = blockAccessor.MapSizeZ / regSize;

            int minrx = GameMath.Clamp(pos.X / regSize, 0, mapRegionSizeX);
            int minrz = GameMath.Clamp(pos.Z / regSize, 0, mapRegionSizeZ);

            int maxrx = GameMath.Clamp((pos.X + schematic.SizeX) / regSize, 0, mapRegionSizeX);
            int maxrz = GameMath.Clamp((pos.Z + schematic.SizeZ) / regSize, 0, mapRegionSizeZ);


            tmpLoc.Set(pos.X, pos.Y, pos.Z, pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);
            for (int rx = minrx; rx <= maxrx; rx++)
            {
                for (int rz = minrz; rz <= maxrz; rz++)
                {
                    IMapRegion mapregion = blockAccessor.GetMapRegion(rx, rz);
                    if (mapregion == null) continue;
                    foreach (var val in mapregion.GeneratedStructures)
                    {
                        if (val.Location.Intersects(tmpLoc))
                        {
                            return true;
                        }
                    }
                }
            }

            if (genStructuresSys.WouldSchematicOverlapAt(blockAccessor, pos, tmpLoc, locationCode)) return true;

            return false;
        }

        public bool SatisfiesMinDistance(BlockPos pos, IWorldAccessor world)
        {
            return SatisfiesMinDistance(pos, world, MinGroupDistance, Group);
        }

        public static bool SatisfiesMinDistance(BlockPos pos, IWorldAccessor world, int mingroupDistance, string group)
        {
            if (mingroupDistance < 1) return true;

            int regSize = world.BlockAccessor.RegionSize;

            int mapRegionSizeX = world.BlockAccessor.MapSizeX / regSize;
            int mapRegionSizeZ = world.BlockAccessor.MapSizeZ / regSize;

            int x1 = pos.X - mingroupDistance;
            int z1 = pos.Z - mingroupDistance;
            int x2 = pos.X + mingroupDistance;
            int z2 = pos.Z + mingroupDistance;

            // Definition: Max structure size is 256x256x256
            //int maxStructureSize = 256;

            long minDistSq = (long)mingroupDistance * mingroupDistance;

            int minrx = GameMath.Clamp(x1 / regSize, 0, mapRegionSizeX);
            int minrz = GameMath.Clamp(z1 / regSize, 0, mapRegionSizeZ);

            int maxrx = GameMath.Clamp(x2 / regSize, 0, mapRegionSizeX);
            int maxrz = GameMath.Clamp(z2 / regSize, 0, mapRegionSizeZ);

            for (int rx = minrx; rx <= maxrx; rx++)
            {
                for (int rz = minrz; rz <= maxrz; rz++)
                {
                    IMapRegion mapregion = world.BlockAccessor.GetMapRegion(rx, rz);
                    if (mapregion == null) continue;

                    foreach (var val in mapregion.GeneratedStructures)
                    {
                        if (val.Group == group && val.Location.Center.SquareDistanceTo(pos.X, pos.Y, pos.Z) < minDistSq)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
