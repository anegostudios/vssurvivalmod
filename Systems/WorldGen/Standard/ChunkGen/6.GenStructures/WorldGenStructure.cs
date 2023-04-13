using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public delegate bool TryGenerateHandler(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos);

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
        public int LandformSizeX;
        [JsonProperty]
        public int LandformSizeZ;
        [JsonProperty]
        public int MinSpawnDist;
        [JsonProperty]
        public int MaxSpawnDist;
        [JsonProperty]
        public float MinY;
        [JsonProperty]
        public float MaxY;

        internal BlockSchematicPartial schematicData;

        public void Init(ICoreServerAPI api, LCGRandom rand)
        {
            schematicData = LoadSchematics<BlockSchematicPartial>(api, Schematics, null)[0];
            schematicData.InitMetaBlocks(api.World.BlockAccessor);
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
        public float MinForest = 0;
        [JsonProperty]
        public float MaxForest = 1;
        [JsonProperty]
        public float MinY = -0.3f;
        [JsonProperty]
        public float MaxY = 1;
        [JsonProperty]
        public NatFloat OffsetX = NatFloat.createGauss(0, 5);
        [JsonProperty]
        public int OffsetY = 0;
        [JsonProperty]
        public NatFloat OffsetZ = NatFloat.createGauss(0, 5);
        [JsonProperty]
        public NatFloat BlockCodeIndex = null;
        [JsonProperty]
        public AssetLocation[] ReplaceWithBlocklayers;
        [JsonProperty]
        public bool PostPass = false;


        internal BlockSchematicStructure[][] schematicDatas;
        internal int[] replaceblockids = new int[0];
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

        public void Init(ICoreServerAPI api, BlockLayerConfig config, RockStrataConfig rockstrata, WorldGenStructuresConfigBase structureConfig, LCGRandom rand)
        {
            this.rand = rand;

            genStructuresSys = api.ModLoader.GetModSystem<GenStructures>();

            unscaledMinRain = (int)(MinRain * 255);
            unscaledMaxRain = (int)(MaxRain * 255);
            unscaledMinTemp = (int)TerraGenConfig.DescaleTemperature(MinTemp);
            unscaledMaxTemp = (int)TerraGenConfig.DescaleTemperature(MaxTemp);


            this.schematicDatas = LoadSchematicsWithRotations<BlockSchematicStructure>(api, Schematics, config);

            if (ReplaceWithBlocklayers != null)
            {
                replaceblockids = new int[ReplaceWithBlocklayers.Length];
                for (int i = 0; i < replaceblockids.Length; i++)
                {
                    Block block = api.World.GetBlock(ReplaceWithBlocklayers[i]);
                    if (block == null)
                    {
                        throw new Exception(string.Format("Schematic with code {0} has replace block layer {1} defined, but no such block found!", Code, ReplaceWithBlocklayers[i]));
                    } else
                    {
                        replaceblockids[i] = block.Id;
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

        internal bool TryGenerate(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, int climateUpLeft, int climateUpRight, int climateBotLeft, int climateBotRight)
        {
            this.climateUpLeft = climateUpLeft;
            this.climateUpRight = climateUpRight;
            this.climateBotLeft = climateBotLeft;
            this.climateBotRight = climateBotRight;
            int chunksize = blockAccessor.ChunkSize;

            int climate = GameMath.BiLerpRgbColor((float)(startPos.X % chunksize) / chunksize, (float)(startPos.Z % chunksize) / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

            int rain = TerraGenConfig.GetRainFall((climate >> 8) & 0xff, startPos.Y);
            int unscaledtempsealevel = (climate >> 16) & 0xff;

            int temp = TerraGenConfig.GetScaledAdjustedTemperature(unscaledtempsealevel, startPos.Y - TerraGenConfig.seaLevel);
            int unscaledtemp = TerraGenConfig.DescaleTemperature(temp);

            if (rain < unscaledMinRain || rain > unscaledMaxRain || unscaledtemp < unscaledMinTemp || unscaledtemp > unscaledMaxTemp) return false;

            startPos.Y += OffsetY;

            // Hardcoding crime here. Please don't look. Prevent generation of schematics on glaciers
            if (unscaledtemp < 20 && startPos.Y > worldForCollectibleResolve.SeaLevel + 15) return false;
            
            rand.InitPositionSeed(startPos.X, startPos.Z);

            bool generated = Generators[(int)Placement](blockAccessor, worldForCollectibleResolve, startPos);

            if (generated && Placement == EnumStructurePlacement.SurfaceRuin)
            {
                float rainValMoss = Math.Max(0, (rain - 50) / 255f);
                float tempValMoss = Math.Max(0, (unscaledtemp - 50) / 255f);
                float mossGrowthChance = 1.5f * rainValMoss * tempValMoss + 1f * rainValMoss * GameMath.Clamp((tempValMoss + 0.33f) / 1.33f, 0, 1);

                int mossTries = (int)(10 * mossGrowthChance * GameMath.Sqrt(LastPlacedSchematicLocation.SizeXYZ));
                int sizex = LastPlacedSchematic.SizeX;
                int sizey = LastPlacedSchematic.SizeY;
                int sizez = LastPlacedSchematic.SizeZ;
                BlockPos tmpPos = new BlockPos();

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

                            var nblock = blockAccessor.GetBlock(tmpPos.X + face.Normali.X, tmpPos.Y + face.Normali.Y, tmpPos.Z + face.Normali.Z);
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


        internal bool TryGenerateRuinAtSurface(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos)
        {
            int num = rand.NextInt(schematicDatas.Length);
            int orient = rand.NextInt(4);
            BlockSchematicStructure schematic = schematicDatas[num][orient];


            int wdthalf = (int)Math.Ceiling(schematic.SizeX / 2f);
            int lenhalf = (int)Math.Ceiling(schematic.SizeZ / 2f);

            int wdt = schematic.SizeX;
            int len = schematic.SizeZ;


            tmpPos.Set(pos.X + wdthalf, 0, pos.Z + lenhalf);
            int centerY = blockAccessor.GetTerrainMapheightAt(pos);

            // Probe all 4 corners + center if they either touch the surface or are sightly below ground

            tmpPos.Set(pos.X, 0, pos.Z);
            int topLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(pos.X + wdt, 0, pos.Z);
            int topRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(pos.X, 0, pos.Z + len);
            int botLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(pos.X + wdt, 0, pos.Z + len);
            int botRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);


            int maxY = GameMath.Max(centerY, topLeftY, topRightY, botLeftY, botRightY);
            int minY = GameMath.Min(centerY, topLeftY, topRightY, botLeftY, botRightY);
            int diff = Math.Abs(maxY - minY);

            if (diff > 3) return false;

            pos.Y = minY;


            // Ensure not deeply submerged in water  =>  actually, that's now OK!

            tmpPos.Set(pos.X, pos.Y + 1 + OffsetY, pos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y + 1 + OffsetY, pos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X, pos.Y + 1 + OffsetY, pos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y + 1 + OffsetY, pos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;


            pos.Y--;

            if (!satisfiesMinDistance(pos, worldForCollectibleResolve)) return false;
            if (WouldOverlapAt(pos, schematic, worldForCollectibleResolve)) return false;

            LastPlacedSchematicLocation.Set(pos.X, pos.Y, pos.Z, pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);
            LastPlacedSchematic = schematic;

            schematic.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, resolvedRockTypeRemaps, replaceblockids);

            return true;
        }


        internal bool TryGenerateAtSurface(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos)
        {
            int num = rand.NextInt(schematicDatas.Length);
            int orient = rand.NextInt(4);
            BlockSchematicStructure schematic = schematicDatas[num][orient];
            
            int wdthalf = (int)Math.Ceiling(schematic.SizeX / 2f);
            int lenhalf = (int)Math.Ceiling(schematic.SizeZ / 2f);
            int wdt = schematic.SizeX;
            int len = schematic.SizeZ;


            tmpPos.Set(pos.X + wdthalf, 0, pos.Z + lenhalf);
            int centerY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            // Probe all 4 corners + center if they are on the same height
            tmpPos.Set(pos.X, 0, pos.Z);
            int topLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(pos.X + wdt, 0, pos.Z);
            int topRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(pos.X, 0, pos.Z + len);
            int botLeftY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            tmpPos.Set(pos.X + wdt, 0, pos.Z + len);
            int botRightY = blockAccessor.GetTerrainMapheightAt(tmpPos);

            // Is the ground flat?
            int diff = GameMath.Max(centerY, topLeftY, topRightY, botLeftY, botRightY) - GameMath.Min(centerY, topLeftY, topRightY, botLeftY, botRightY);
            if (diff > 1) return false;

            pos.Y = centerY + 1 + OffsetY;


            // Ensure not floating on water
            tmpPos.Set(pos.X + wdthalf, pos.Y - 1, pos.Z + lenhalf);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

       
            tmpPos.Set(pos.X, pos.Y - 1, pos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y - 1, pos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X, pos.Y - 1, pos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y - 1, pos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            // Ensure not submerged in water
            tmpPos.Set(pos.X, pos.Y, pos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;


            tmpPos.Set(pos.X + wdt, pos.Y, pos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y, pos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X, pos.Y, pos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y, pos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;



            tmpPos.Set(pos.X, pos.Y + 1, pos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y + 1, pos.Z);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X, pos.Y + 1, pos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y + 1, pos.Z + len);
            if (blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid).IsLiquid()) return false;


            if (!satisfiesMinDistance(pos, worldForCollectibleResolve)) return false;
            if (WouldOverlapAt(pos, schematic, worldForCollectibleResolve)) return false;

            LastPlacedSchematicLocation.Set(pos.X, pos.Y, pos.Z, pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);
            LastPlacedSchematic = schematic;
            schematic.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, resolvedRockTypeRemaps, replaceblockids);
            return true;
        }



        internal bool TryGenerateUnderwater(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos)
        {
            return false;
        }

        internal bool TryGenerateUnderground(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos)
        {
            int num = rand.NextInt(schematicDatas.Length);

            BlockSchematicStructure[] schematicStruc = schematicDatas[num];
            BlockPos targetPos = pos.Copy();
            BlockSchematicStructure schematic;


            if (schematicStruc[0].PathwayStarts.Length > 0)
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
                        targetPos.Up();
                        found = true;
                        break;
                    }
                }

                if (!found) return false;

                // 3. Random pathway
                found = false;
                int pathwayNum = rand.NextInt(schematicStruc[0].PathwayStarts.Length);
                int targetOrientation;
                int targetDistance = -1;
                BlockFacing targetFacing = null;
                BlockPos[] pathway=null;

                // 4. At that position search for a suitable stone wall in any direction
                for (targetOrientation = 0; targetOrientation < 4; targetOrientation++)
                {
                    // Try every rotation
                    pathway = schematicStruc[targetOrientation].PathwayOffsets[pathwayNum];
                    // This is the facing we are currently checking
                    targetFacing = schematicStruc[targetOrientation].PathwaySides[pathwayNum];

                    targetDistance = CanPlacePathwayAt(blockAccessor, pathway, targetFacing, targetPos);
                    if (targetDistance != -1) break;
                }

                if (targetDistance == -1) return false;

                BlockPos pathwayStart = schematicStruc[targetOrientation].PathwayStarts[pathwayNum];

                // Move back the structure so that the door aligns to the cave wall
                targetPos.Add(
                    -pathwayStart.X - targetFacing.Normali.X * targetDistance,
                    -pathwayStart.Y - targetFacing.Normali.Y * targetDistance,
                    -pathwayStart.Z - targetFacing.Normali.Z * targetDistance
                );

                schematic = schematicStruc[targetOrientation];

                if (!TestUndergroundCheckPositions(blockAccessor, targetPos, schematic.UndergroundCheckPositions)) return false;
                if (WouldOverlapAt(targetPos, schematic, worldForCollectibleResolve)) return false;

                LastPlacedSchematicLocation.Set(targetPos.X, targetPos.Y, targetPos.Z, targetPos.X + schematic.SizeX, targetPos.Y + schematic.SizeY, targetPos.Z + schematic.SizeZ);
                schematic.Place(blockAccessor, worldForCollectibleResolve, targetPos);

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

            schematic = schematicStruc[rand.NextInt(4)];

            BlockPos placePos = schematic.AdjustStartPos(targetPos.Copy(), Origin);

            LastPlacedSchematicLocation.Set(placePos.X, placePos.Y, placePos.Z, placePos.X + schematic.SizeX, placePos.Y + schematic.SizeY, placePos.Z + schematic.SizeZ);
            LastPlacedSchematic = schematic;

            if (insideblockids.Count > 0 && !insideblockids.Contains(blockAccessor.GetBlock(targetPos).Id)) return false;
            if (!TestUndergroundCheckPositions(blockAccessor, placePos, schematic.UndergroundCheckPositions)) return false;
            if (!satisfiesMinDistance(pos, worldForCollectibleResolve)) return false;
            if (WouldOverlapAt(pos, schematic, worldForCollectibleResolve)) return false;

            if (resolvedRockTypeRemaps != null)
            {
                schematic.PlaceReplacingBlocks(blockAccessor, worldForCollectibleResolve, placePos, schematic.ReplaceMode, resolvedRockTypeRemaps);
                
            } else
            {
                schematic.Place(blockAccessor, worldForCollectibleResolve, targetPos);
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


        static Cuboidi tmpLoc = new Cuboidi();

        public bool WouldOverlapAt(BlockPos pos, BlockSchematic schematic, IWorldAccessor world)
        {
            int rx = pos.X / world.BlockAccessor.RegionSize;
            int rz = pos.Z / world.BlockAccessor.RegionSize;

            IMapRegion mapregion = world.BlockAccessor.GetMapRegion(rx, rz);
            if (mapregion == null) return false;

            tmpLoc.Set(pos.X, pos.Y, pos.Z, pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);

            foreach (var val in mapregion.GeneratedStructures)
            {
                if (val.Location.Intersects(tmpLoc))
                {
                    return true;
                }
            }

            if (!genStructuresSys.WouldSchematicOverlapAt(pos, tmpLoc)) return false;

            return false;
        }

        public bool satisfiesMinDistance(BlockPos pos, IWorldAccessor world)
        {
            if (MinGroupDistance < 1) return true;

            int regSize = world.BlockAccessor.RegionSize;

            int mapRegionSizeX = world.BlockAccessor.MapSizeX / regSize;
            int mapRegionSizeZ = world.BlockAccessor.MapSizeZ / regSize;

            int x1 = pos.X - MinGroupDistance;
            int z1 = pos.Z - MinGroupDistance; 
            int x2 = pos.X + MinGroupDistance;
            int z2 = pos.Z + MinGroupDistance;

            // Definition: Max structure size is 256x256x256
            //int maxStructureSize = 256;

            int minDistSq = MinGroupDistance * MinGroupDistance;

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
                        if (val.Group == this.Group && val.Location.Center.SquareDistanceTo(pos.X, pos.Y, pos.Z) < minDistSq)
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
