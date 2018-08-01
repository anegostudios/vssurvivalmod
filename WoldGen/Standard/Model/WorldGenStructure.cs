using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    public delegate bool TryGenerateHandler(IBlockAccessor blockAcccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos);

    public enum EnumStructurePlacement
    {
        Surface,
        Underwater,
        Underground
    }

    public class WorldGenStructure
    {
        [JsonProperty]
        public string Code;

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
        public EnumStructurePlacement Placement = EnumStructurePlacement.Surface;
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


        internal BlockSchematicStructure[][] schematicDatas;
        internal ushort[] replaceblockids = new ushort[0];

        TryGenerateHandler[] Generators;

        public WorldGenStructure()
        {
            Generators = new TryGenerateHandler[]
            {
                TryGenerateAtSurface,
                TryGenerateUnderwater,
                TryGenerateUnderground
            };
        }

        Random rand;


        public void Init(ICoreServerAPI api, BlockLayerConfig config)
        {
            rand = api.World.Rand;

            List<BlockSchematicStructure[]> schematics = new List<BlockSchematicStructure[]>();

            for (int i = 0; i < Schematics.Length; i++)
            {
                string error = "";
                IAsset[] assets;

                if (Schematics[i].EndsWith("*"))
                {
                    assets = api.Assets.GetMany("worldgen/terrain/standard/schematics/" + Schematics[i].Substring(0, Schematics[i].Length - 1)).ToArray();
                } else
                {
                    assets = new IAsset[] { api.Assets.Get("worldgen/terrain/standard/schematics/" + Schematics[i] + ".json") };
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

                    

                    BlockSchematicStructure[] rotations = new BlockSchematicStructure[4];
                    rotations[0] = schematic;

                    for (int k = 0; k < 4; k++)
                    {
                        if (k > 0)
                        {
                            rotations[k] = rotations[0].Clone();
                            rotations[k].RotateWhilePacked(api.World, EnumOrigin.BottomCenter, k * 90);
                        }
                        rotations[k].blockLayerConfig = config;
                        rotations[k].Init(api.World.BlockAccessor);
                        rotations[k].LoadMetaInformation(api.World.BlockAccessor);
                    }

                    schematics.Add(rotations);
                }
            }

            this.schematicDatas = schematics.ToArray();


            if (ReplaceWithBlocklayers != null)
            {
                replaceblockids = new ushort[ReplaceWithBlocklayers.Length];
                for (int i = 0; i < replaceblockids.Length; i++)
                {
                    replaceblockids[i] = (ushort)api.World.GetBlock(ReplaceWithBlocklayers[i]).Id;
                }
            }

        }


        BlockPos tmpPos = new BlockPos();
        int climateUpLeft, climateUpRight, climateBotLeft, climateBotRight;

        internal bool TryGenerate(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, int climateUpLeft, int climateUpRight, int climateBotLeft, int climateBotRight)
        {
            this.climateUpLeft = climateUpLeft;
            this.climateUpRight = climateUpRight;
            this.climateBotLeft = climateBotLeft;
            this.climateBotRight = climateBotRight;
            
            startPos.Y += OffsetY;

            Generators[(int)Placement](blockAccessor, worldForCollectibleResolve, startPos);

            return true;
        }


        internal bool TryGenerateAtSurface(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos)
        {
            int chunksize = blockAccessor.ChunkSize;
            int climate = GameMath.BiLerpRgbColor((float)(pos.X % chunksize) / chunksize, (float)(pos.Z % chunksize) / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);


            int num = rand.Next(schematicDatas.Length);
            int orient = rand.Next(4);
            BlockSchematicStructure schematic = schematicDatas[num][orient];
            

            int widthHalf = (int)Math.Ceiling(schematic.SizeX / 2f);
            int lengthHalf = (int)Math.Ceiling(schematic.SizeZ / 2f);

            // Ensure not submerged in water

            tmpPos.Set(pos.X - widthHalf, pos.Y + OffsetY, pos.Z - lengthHalf);
            if (blockAccessor.GetBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + widthHalf, pos.Y + OffsetY, pos.Z - lengthHalf);
            if (blockAccessor.GetBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X - widthHalf, pos.Y + OffsetY, pos.Z + lengthHalf);
            if (blockAccessor.GetBlock(tmpPos).IsLiquid()) return false;

            tmpPos.Set(pos.X + widthHalf, pos.Y + OffsetY, pos.Z + lengthHalf);
            if (blockAccessor.GetBlock(tmpPos).IsLiquid()) return false;



            // Probe all 4 corners + center if they either touch the surface or are sightly below ground

            int centerDiff = blockAccessor.GetTerrainMapheightAt(pos) - pos.Y;


            tmpPos.Set(pos.X - widthHalf, 0, pos.Z - lengthHalf);
            int topLeftDiff = blockAccessor.GetTerrainMapheightAt(tmpPos) - pos.Y;

            tmpPos.Set(pos.X + widthHalf, 0, pos.Z - lengthHalf);
            int topRightDiff = blockAccessor.GetTerrainMapheightAt(tmpPos) - pos.Y;

            tmpPos.Set(pos.X - widthHalf, 0, pos.Z + lengthHalf);
            int botLeftDiff = blockAccessor.GetTerrainMapheightAt(tmpPos) - pos.Y;

            tmpPos.Set(pos.X + widthHalf, 0, pos.Z + lengthHalf);
            int botRightDiff = blockAccessor.GetTerrainMapheightAt(tmpPos) - pos.Y;

            bool ok =
                centerDiff >= -3 && centerDiff <= 1 &&
                topLeftDiff >= -3 && topLeftDiff <= 1 &&
                topRightDiff >= -3 && topRightDiff <= 1 &&
                botLeftDiff >= -3 && botLeftDiff <= 1 &&
                botRightDiff >= -3 && botRightDiff <= 1
            ;


            if (ok)
            {
                schematic.PlaceRespectingBlockLayers(blockAccessor, worldForCollectibleResolve, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, replaceblockids);
            }

            return ok;
        }



        internal bool TryGenerateUnderwater(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos)
        {
            return false;
        }

        internal bool TryGenerateUnderground(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos pos)
        {
            int num = rand.Next(schematicDatas.Length);

            BlockSchematicStructure[] schematicStruc = schematicDatas[num];
            BlockPos targetPos = pos.Copy();

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
                int pathwayNum = rand.Next(schematicStruc[0].PathwayStarts.Length);
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
             
                schematicStruc[targetOrientation].Place(blockAccessor, worldForCollectibleResolve, targetPos);

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

            BlockSchematicStructure schematic = schematicStruc[rand.Next(4)];
            if (!TestUndergroundCheckPositions(blockAccessor, targetPos, schematic.UndergroundCheckPositions)) return false;

            schematic.Place(blockAccessor, worldForCollectibleResolve, targetPos);

            //Console.WriteLine("/tp ={0} ={1} ={2}      ({3} - {4})", targetPos.X, targetPos.Y, targetPos.Z, Code, Schematics[num]);

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

            bool oppositeDir = rand.Next(2) > 0;

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
    }
}
