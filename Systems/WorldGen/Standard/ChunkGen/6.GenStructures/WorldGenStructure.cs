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

    public class WorldGenStructure
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
        public string[] Schematics;

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
        public EnumStructurePlacement Placement = EnumStructurePlacement.SurfaceRuin;
        [JsonProperty]
        public NatFloat Depth = null;
        [JsonProperty]
        public NatFloat OffsetX = NatFloat.createGauss(0, 5);
        [JsonProperty]
        public int OffsetY = 0;
        [JsonProperty]
        public NatFloat OffsetZ = NatFloat.createGauss(0, 5);
        [JsonProperty]
        public NatFloat BlockCodeIndex = null;
        [JsonProperty]
        public NatFloat Quantity = NatFloat.createGauss(7, 7);
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
        public Dictionary<AssetLocation, AssetLocation> ReplaceWithRockType = null;
        [JsonProperty]
        public AssetLocation[] InsideBlockCodes;
        [JsonProperty]
        public EnumOrigin Origin = EnumOrigin.StartPos;


        internal BlockSchematicStructure[][] schematicDatas;
        internal int[] replaceblockids = new int[0];
        internal HashSet<int> insideblockids = new HashSet<int>();

        internal Dictionary<int, Dictionary<int, int>> resolvedReplaceWithRocktype = null;

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


        public void Init(ICoreServerAPI api, BlockLayerConfig config, RockStrataConfig rockstrata, LCGRandom rand)
        {
            this.rand = rand;

            List<BlockSchematicStructure[]> schematics = new List<BlockSchematicStructure[]>();

            for (int i = 0; i < Schematics.Length; i++)
            {
                string error = "";
                IAsset[] assets;

                if (Schematics[i].EndsWith("*"))
                {
                    assets = api.Assets.GetManyInCategory("worldgen", "schematics/" + Schematics[i].Substring(0, Schematics[i].Length - 1)).ToArray();
                } else
                {
                    assets = new IAsset[] { api.Assets.Get("worldgen/schematics/" + Schematics[i] + ".json") };
                }

                for (int j = 0; j < assets.Length; j++)
                {
                    IAsset asset = assets[j];

                    BlockSchematicStructure schematic = asset.ToObject<BlockSchematicStructure>();


                    if (schematic == null)
                    {
                        api.World.Logger.Warning("Could not load {0}: {1}", Schematics[i], error);
                        continue;
                    }


                    schematic.FromFileName = asset.Name;

                    BlockSchematicStructure[] rotations = new BlockSchematicStructure[4];
                    rotations[0] = schematic;

                    for (int k = 0; k < 4; k++)
                    {
                        if (k > 0)
                        {
                            rotations[k] = rotations[0].Clone();
                            rotations[k].TransformWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90);
                        }
                        rotations[k].blockLayerConfig = config;
                        rotations[k].Init(api.World.BlockAccessor);
                        rotations[k].LoadMetaInformationAndValidate(api.World.BlockAccessor, api.World, schematic.FromFileName);
                    }

                    schematics.Add(rotations);
                }
            }

            this.schematicDatas = schematics.ToArray();


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

            if (ReplaceWithRockType != null)
            {
                resolvedReplaceWithRocktype = new Dictionary<int, Dictionary<int, int>>();

                foreach (var val in ReplaceWithRockType)
                {
                    int sourceBlockId = api.World.GetBlock(val.Key).Id;

                    Dictionary<int, int> blockIdByRockId = new Dictionary<int, int>();

                    foreach (var strat in rockstrata.Variants)
                    {
                        Block rockBlock = api.World.GetBlock(strat.BlockCode);
                        AssetLocation resolvedLoc = val.Value.Clone();
                        resolvedLoc.Path = resolvedLoc.Path.Replace("{rock}", rockBlock.LastCodePart());

                        Block resolvedBlock = api.World.GetBlock(resolvedLoc);
                        if (resolvedBlock != null)
                        {
                            blockIdByRockId[rockBlock.Id] = resolvedBlock.Id;

                            Block quartzBlock = api.World.GetBlock(new AssetLocation("ore-quartz-" + rockBlock.LastCodePart()));
                            if (quartzBlock != null)
                            {
                                blockIdByRockId[quartzBlock.Id] = resolvedBlock.Id;
                            }
                        }


                    }

                    resolvedReplaceWithRocktype[sourceBlockId] = blockIdByRockId;
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
            
            startPos.Y += OffsetY;

            rand.InitPositionSeed(startPos.X, startPos.Z);

            return Generators[(int)Placement](blockAccessor, worldForCollectibleResolve, startPos);
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
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y + 1 + OffsetY, pos.Z);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X, pos.Y + 1 + OffsetY, pos.Z + len);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y + 1 + OffsetY, pos.Z + len);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;


            pos.Y--;

            if (!satisfiesMinDistance(pos, worldForCollectibleResolve)) return false;
            if (isStructureAt(pos, worldForCollectibleResolve)) return false;

            LastPlacedSchematicLocation.Set(pos.X, pos.Y, pos.Z, pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);
            LastPlacedSchematic = schematic;
            schematic.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, replaceblockids);

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


            int diff = GameMath.Max(centerY, topLeftY, topRightY, botLeftY, botRightY) - GameMath.Min(centerY, topLeftY, topRightY, botLeftY, botRightY);
            if (diff != 0) return false;

            pos.Y += centerY - pos.Y + 1 + OffsetY;


            // Ensure not floating on water
            tmpPos.Set(pos.X + wdthalf, pos.Y - 1, pos.Z + lenhalf);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

       
            tmpPos.Set(pos.X, pos.Y - 1, pos.Z);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y - 1, pos.Z);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X, pos.Y - 1, pos.Z + len);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y - 1, pos.Z + len);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            // Ensure not submerged in water
            tmpPos.Set(pos.X, pos.Y, pos.Z);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;


            tmpPos.Set(pos.X + wdt, pos.Y - 1, pos.Z + len);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y, pos.Z);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X, pos.Y, pos.Z + len);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y, pos.Z + len);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;



            tmpPos.Set(pos.X, pos.Y + 1, pos.Z);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y + 1, pos.Z);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X, pos.Y + 1, pos.Z + len);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + wdt, pos.Y + 1, pos.Z + len);
            if (blockAccessor.GetLiquidBlock(tmpPos).IsLiquid()) return false;


            if (!satisfiesMinDistance(pos, worldForCollectibleResolve)) return false;
            if (isStructureAt(pos, worldForCollectibleResolve)) return false;

            LastPlacedSchematicLocation.Set(pos.X, pos.Y, pos.Z, pos.X + schematic.SizeX, pos.Y + schematic.SizeY, pos.Z + schematic.SizeZ);
            LastPlacedSchematic = schematic;
            schematic.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, replaceblockids);
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
                int targetOrientation = 0;
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

                if (!TestUndergroundCheckPositions(blockAccessor, targetPos, schematicStruc[targetOrientation].UndergroundCheckPositions)) return false;
                if (isStructureAt(targetPos, worldForCollectibleResolve)) return false;

                schematic = schematicStruc[targetOrientation];
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
            if (isStructureAt(pos, worldForCollectibleResolve)) return false;

            if (resolvedReplaceWithRocktype != null)
            {
                //Console.WriteLine(schematic.FromFileName + " place at " + targetPos +", offseted to " + placePos);

                schematic.PlaceReplacingBlocks(blockAccessor, worldForCollectibleResolve, placePos, schematic.ReplaceMode, resolvedReplaceWithRocktype);
                
            } else
            {
                schematic.Place(blockAccessor, worldForCollectibleResolve, targetPos);
            }

            

            return false;
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
        

        public bool isStructureAt(BlockPos pos, IWorldAccessor world)
        {
            int rx = pos.X / world.BlockAccessor.RegionSize;
            int rz = pos.Z / world.BlockAccessor.RegionSize;

            IMapRegion mapregion = world.BlockAccessor.GetMapRegion(rx, rz);
            if (mapregion == null) return false;

            foreach (var val in mapregion.GeneratedStructures)
            {
                if (val.Location.Contains(pos) || val.Location.Contains(pos.X, pos.Y - 3, pos.Z))
                {
                    return true;
                }
            }

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
