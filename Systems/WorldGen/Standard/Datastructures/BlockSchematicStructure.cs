using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class BlockSchematicStructure : BlockSchematic
    {
        public Dictionary<int, AssetLocation> BlockCodesTmpForRemap = new Dictionary<int, AssetLocation>();

        public string FromFileName;
        public Dictionary<AssetLocation, AssetLocation> Remaps;

        public Block[,,] blocksByPos;
        public BlockLayerConfig blockLayerConfig;
        int mapheight;

        PlaceBlockDelegate handler = null;
        GenBlockLayers genBlockLayers;

        public int OffsetY { get; set; }

        public override void Init(IBlockAccessor blockAccessor)
        {
            base.Init(blockAccessor);

            mapheight = blockAccessor.MapSizeY;

            blocksByPos = new Block[SizeX + 1, SizeY + 1, SizeZ + 1];

            if (Remaps != null && Remaps.Count > 0)
            {
                foreach (var storedId in BlockCodes.Keys.ToArray())
                {
                    foreach (var remap in Remaps)
                    {
                        if (remap.Equals(BlockCodes[storedId].Path))
                        {
                            BlockCodes[storedId] = remap.Value;
                        }
                    }
                }
            }

            for (int i = 0; i < Indices.Count; i++)
            {
                uint index = Indices[i];
                int storedBlockid = BlockIds[i];

                int dx = (int)(index & 0x1ff);
                int dy = (int)((index >> 20) & 0x1ff);
                int dz = (int)((index >> 10) & 0x1ff);

                Block block = blockAccessor.GetBlock(BlockCodes[storedBlockid]);
                if (block == null) continue;

                blocksByPos[dx, dy, dz] = block;
            }

            handler = null;
            switch (ReplaceMode)
            {
                case EnumReplaceMode.ReplaceAll:
                    handler = PlaceReplaceAll;
                    break;

                case EnumReplaceMode.Replaceable:
                    handler = PlaceReplaceable;
                    break;

                case EnumReplaceMode.ReplaceAllNoAir:
                    handler = PlaceReplaceAllNoAir;
                    break;

                case EnumReplaceMode.ReplaceOnlyAir:
                    handler = PlaceReplaceOnlyAir;
                    break;
            }
        }

        /// <summary>
        /// For placement of ruins during worldgen, replaces the topsoil with the area specific soil (e.g. sand)
        /// </summary>
        /// <param name="blockAccessor"></param>
        /// <param name="worldForCollectibleResolve"></param>
        /// <param name="startPos"></param>
        /// <param name="climateUpLeft"></param>
        /// <param name="climateUpRight"></param>
        /// <param name="climateBotLeft"></param>
        /// <param name="climateBotRight"></param>
        /// <param name="replaceBlocks"></param>
        /// <param name="replaceWithBlockLayersBlockids"></param>
        /// <param name="replaceMetaBlocks"></param>
        /// <param name="replaceBlockEntities">If true, deletes any existing block entities at that location</param>
        /// <param name="suppressSoilIfAirBelow"></param>
        /// <returns></returns>
        public int PlaceRespectingBlockLayers(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, int climateUpLeft, int climateUpRight, int climateBotLeft, int climateBotRight, Dictionary<int, Dictionary<int, int>> replaceBlocks, int[] replaceWithBlockLayersBlockids, bool replaceMetaBlocks = true, bool replaceBlockEntities = false, bool suppressSoilIfAirBelow = false)
        {
            if (genBlockLayers == null) genBlockLayers = worldForCollectibleResolve.Api.ModLoader.GetModSystem<GenBlockLayers>();

            BlockPos curPos = new BlockPos();
            int placed = 0;
            int chunksize = blockAccessor.ChunkSize;

            int chunkBaseX = (startPos.X / chunksize) * chunksize;
            int chunkBaseZ = (startPos.Z / chunksize) * chunksize;

            curPos.Set(SizeX / 2 + startPos.X, startPos.Y, SizeZ / 2 + startPos.Z);
            IMapChunk mapchunk = blockAccessor.GetMapChunkAtBlockPos(curPos);
            int centerrockblockid = mapchunk.TopRockIdMap[(curPos.Z % chunksize) * chunksize + curPos.X % chunksize];

            IWorldAccessor worldgenWorldAccessor = blockAccessor is IWorldGenBlockAccessor wgba ? wgba.WorldgenWorldAccessor : worldForCollectibleResolve;

            resolveReplaceRemapsForBlockEntities(blockAccessor, worldForCollectibleResolve, replaceBlocks, centerrockblockid);

            Dictionary<BlockPos, Block> layerBlockForBlockEntities = new Dictionary<BlockPos, Block>();

            for (int x = 0; x < SizeX; x++)
            {
                for (int z = 0; z < SizeZ; z++)
                {
                    curPos.Set(x + startPos.X, startPos.Y, z + startPos.Z);
                    

                    mapchunk = blockAccessor.GetMapChunkAtBlockPos(curPos);
                    int rockblockid = mapchunk.TopRockIdMap[(curPos.Z % chunksize) * chunksize + curPos.X % chunksize];
                    int depth = mapchunk.WorldGenTerrainHeightMap[(curPos.Z % chunksize) * chunksize + curPos.X % chunksize] - (SizeY + startPos.Y); //aboveLiqBlock.Id != 0 ? 1 : 0;

                    int maxY = -1;
                    int underWaterDepth = -1;

                    bool highestBlockinCol = true;
                    for (int y = SizeY - 1; y >= 0; y--)
                    {
                        depth++;
                        curPos.Set(x + startPos.X, y + startPos.Y, z + startPos.Z);
                        Block block = blocksByPos[x, y, z];
                        Block origBlock = block;
                        if (block == null) continue;

                        var aboveLiqBlock = blockAccessor.GetBlock(curPos.X, curPos.Y + y, curPos.Z, BlockLayersAccess.Fluid);
                        if (aboveLiqBlock != null && aboveLiqBlock.IsLiquid()) underWaterDepth++;

                        if (replaceMetaBlocks && block == undergroundBlock) continue;

                        if (block.Replaceable < 1000 && depth >= 0)
                        {
                            if (replaceWithBlockLayersBlockids.Contains(block.BlockId) || block.CustomBlockLayerHandler)
                            {
                                if (suppressSoilIfAirBelow && (y == 0 || blocksByPos[x, y - 1, z] == null)) // only check this on the bottom layer and if current newBlock is soil: any block in replaceblockids is assumed to be soil
                                {
                                    Block belowBlock = blockAccessor.GetBlock(curPos.X, curPos.Y - 1, curPos.Z, BlockLayersAccess.SolidBlocks);
                                    if (belowBlock.Replaceable > 3000)
                                    {
                                        int yy = y + 1;
                                        while (yy < SizeY)    // clear any soil in the column above this
                                        {
                                            Block placedBlock = blocksByPos[x, yy, z];
                                            if (placedBlock == null || !replaceWithBlockLayersBlockids.Contains(placedBlock.BlockId)) break;
                                            blockAccessor.SetBlock(0, new BlockPos(curPos.X, startPos.Y + yy, curPos.Z), BlockLayersAccess.Solid);
                                            yy++;
                                        }
                                        continue;
                                    }
                                }
                                if (depth == 0 && replaceWithBlockLayersBlockids.Length > 1)   // do not place top surface (typically grassy soil) directly beneath solid blocks other than logs, snow, ice
                                {
                                    Block aboveBlock = blockAccessor.GetBlock(curPos.X, curPos.Y + 1, curPos.Z, BlockLayersAccess.SolidBlocks);
                                    if (aboveBlock.SideSolid[BlockFacing.DOWN.Index] && aboveBlock.BlockMaterial != EnumBlockMaterial.Wood && aboveBlock.BlockMaterial != EnumBlockMaterial.Snow && aboveBlock.BlockMaterial != EnumBlockMaterial.Ice) depth++;
                                }

                                int climate = GameMath.BiLerpRgbColor(
                                    (float)GameMath.Clamp((curPos.X - chunkBaseX) / (float)chunksize, 0, 1), 
                                    (float)GameMath.Clamp((curPos.Z - chunkBaseZ) / (float)chunksize, 0, 1),
                                    climateUpLeft, climateUpRight, climateBotLeft, climateBotRight
                                );
                                var layerBlock = GetBlockLayerBlock((climate >> 8) & 0xff, (climate >> 16) & 0xff, curPos.Y - 1, rockblockid, depth, block, worldForCollectibleResolve.Blocks, curPos, underWaterDepth);

                                if (block.CustomBlockLayerHandler && layerBlock != block)
                                {
                                    layerBlockForBlockEntities[curPos.Copy()] = layerBlock;
                                }
                                else
                                {
                                    block = layerBlock;
                                }
                            }
                        }                 

                        if (replaceBlocks != null)
                        {
                            Dictionary<int, int> replaceByBlock;
                            if (replaceBlocks.TryGetValue(block.Id, out replaceByBlock))
                            {
                                int newBlockId;
                                if (replaceByBlock.TryGetValue(centerrockblockid, out newBlockId))
                                {
                                    block = blockAccessor.GetBlock(newBlockId);
                                }
                            }
                        }

                        int p = handler(blockAccessor, curPos, block, true);

                        if (block.Id != 0 && !block.SideSolid.All)
                        {
                            aboveLiqBlock = blockAccessor.GetBlock(curPos.X, curPos.Y + 1, curPos.Z, BlockLayersAccess.Fluid);
                            if (aboveLiqBlock.Id != 0)
                            {
                                blockAccessor.SetBlock(aboveLiqBlock.BlockId, curPos, BlockLayersAccess.Fluid);
                            }
                        }

                        if (p > 0)
                        {
                            if (highestBlockinCol)
                            {
                                // Make any plants, tallgrass etc above this schematic fall, but do not do this test in lower blocks in the schematic (e.g. tables in trader caravan)
                                Block aboveBlock = blockAccessor.GetBlock(curPos.X, curPos.Y + 1, curPos.Z, BlockLayersAccess.Solid);
                                if (aboveBlock.Id > 0)
                                {
                                    aboveBlock.OnNeighbourBlockChange(worldgenWorldAccessor, curPos.UpCopy(), curPos);
                                }
                                highestBlockinCol = false;
                            }
                            placed += p;

                            if (!block.RainPermeable)
                            {
                                if (block == fillerBlock || block == pathwayBlock)
                                {
                                    int lx = curPos.X % chunksize;
                                    int lz = curPos.Z % chunksize;
                                    if (mapchunk.RainHeightMap[lz * chunksize + lx] == curPos.Y) mapchunk.RainHeightMap[lz * chunksize + lx]--;
                                }
                                else
                                {
                                    maxY = Math.Max(curPos.Y, maxY);
                                }
                            }
                        }


                        byte[] lightHsv = block.GetLightHsv(blockAccessor, curPos);

                        if (lightHsv[2] > 0 && blockAccessor is IWorldGenBlockAccessor)
                        {
                            Block oldBlock = blockAccessor.GetBlock(curPos);
                            ((IWorldGenBlockAccessor)blockAccessor).ScheduleBlockLightUpdate(curPos.Copy(), oldBlock.BlockId, block.BlockId);
                        }
                    }

                    // In the post pass the rain map does not update, so let's set it ourselves
                    if (maxY >= 0)
                    {
                        int lx = curPos.X % chunksize;
                        int lz = curPos.Z % chunksize;
                        int y = mapchunk.RainHeightMap[lz * chunksize + lx];
                        mapchunk.RainHeightMap[lz * chunksize + lx] = (ushort)Math.Max(y, maxY);
                    }
                }
            }

            PlaceDecors(blockAccessor, startPos);
            PlaceEntitiesAndBlockEntities(blockAccessor, worldForCollectibleResolve, startPos, BlockCodesTmpForRemap, ItemCodes, replaceBlockEntities, replaceBlocks, centerrockblockid, layerBlockForBlockEntities);

            return placed;
        }

        private void resolveReplaceRemapsForBlockEntities(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid)
        {
            if (replaceBlocks == null)
            {
                BlockCodesTmpForRemap = BlockCodes;
                return;
            }

            foreach (var val in BlockCodes)
            {
                Block origBlock = worldForCollectibleResolve.GetBlock(val.Value);
                if (origBlock == null) continue; // Invalid blocks

                BlockCodesTmpForRemap[val.Key] = val.Value;

                if (replaceBlocks.TryGetValue(origBlock.Id, out var replaceByBlock))
                {
                    int newBlockId;
                    if (replaceByBlock.TryGetValue(centerrockblockid, out newBlockId))
                    {
                        BlockCodesTmpForRemap[val.Key] = blockAccessor.GetBlock(newBlockId).Code;
                    }
                }
            }
        }




        /// <summary>
        /// Will place all blocks using the supplied replace mode. Note: If you use a revertable or bulk block accessor you will have to call PlaceBlockEntities() after the Commit()
        /// </summary>
        /// <param name="blockAccessor"></param>
        /// <param name="worldForCollectibleResolve"></param>
        /// <param name="startPos"></param>
        /// <param name="mode"></param>
        /// <param name="replaceMetaBlocks"></param>
        /// <returns></returns>
        public virtual int PlaceReplacingBlocks(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, EnumReplaceMode mode, Dictionary<int, Dictionary<int, int>> replaceBlocks, bool replaceMetaBlocks = true)
        {
            BlockPos curPos = new BlockPos();
            int placed = 0;

            int chunksize = blockAccessor.ChunkSize;
            curPos.Set(SizeX / 2 + startPos.X, startPos.Y, SizeZ / 2 + startPos.Z);
            IMapChunk mapchunk = blockAccessor.GetMapChunkAtBlockPos(curPos);
            int centerrockblockid = mapchunk.TopRockIdMap[(curPos.Z % chunksize) * chunksize + curPos.X % chunksize];
            resolveReplaceRemapsForBlockEntities(blockAccessor, worldForCollectibleResolve, replaceBlocks, centerrockblockid);


            PlaceBlockDelegate handler = null;
            switch (ReplaceMode)
            {
                case EnumReplaceMode.ReplaceAll:
                    handler = PlaceReplaceAll;
                    break;

                case EnumReplaceMode.Replaceable:
                    handler = PlaceReplaceable;
                    break;

                case EnumReplaceMode.ReplaceAllNoAir:
                    handler = PlaceReplaceAllNoAir;
                    break;

                case EnumReplaceMode.ReplaceOnlyAir:
                    handler = PlaceReplaceOnlyAir;
                    break;
            }

            for (int i = 0; i < Indices.Count; i++)
            {
                uint index = Indices[i];
                int storedBlockid = BlockIds[i];

                int dx = (int)(index & 0x1ff);
                int dy = (int)((index >> 20) & 0x1ff);
                int dz = (int)((index >> 10) & 0x1ff);

                AssetLocation blockCode = BlockCodes[storedBlockid];

                Block newBlock = blockAccessor.GetBlock(blockCode);

                if (newBlock == null || (replaceMetaBlocks && newBlock == undergroundBlock)) continue;

                curPos.Set(dx + startPos.X, dy + startPos.Y, dz + startPos.Z);

                Block oldBlock = blockAccessor.GetBlock(curPos);
                Dictionary<int, int> replaceByBlock;
                if (replaceBlocks.TryGetValue(newBlock.Id, out replaceByBlock))
                {
                    int newBlockId;
                    if (replaceByBlock.TryGetValue(oldBlock.Id, out newBlockId))
                    {
                        newBlock = blockAccessor.GetBlock(newBlockId);
                    }
                }

                placed += handler(blockAccessor, curPos, newBlock, replaceMetaBlocks);

                if (newBlock.LightHsv[2] > 0 && blockAccessor is IWorldGenBlockAccessor)
                {
                    ((IWorldGenBlockAccessor)blockAccessor).ScheduleBlockLightUpdate(curPos.Copy(), oldBlock.BlockId, newBlock.BlockId);
                }
            }

            if (!(blockAccessor is IBlockAccessorRevertable))
            {
                PlaceEntitiesAndBlockEntities(blockAccessor, worldForCollectibleResolve, startPos, BlockCodesTmpForRemap, ItemCodes);
            }

            return placed;
        }





        private Block GetBlockLayerBlock(int unscaledRain, int unscaledTemp, int posY, int rockBlockId, int forDepth, Block defaultBlock, IList<Block> blocks, BlockPos pos, int underWaterDepth)
        {
            if (blockLayerConfig == null) return defaultBlock;

            posY -= forDepth;
            float distx = (float)genBlockLayers.distort2dx.Noise(pos.X, pos.Z);
            float temperature = TerraGenConfig.GetScaledAdjustedTemperatureFloat(unscaledTemp, posY - TerraGenConfig.seaLevel + (int)(distx / 5));
            float rainRel = TerraGenConfig.GetRainFall(unscaledRain, posY) / 255f;
            float heightRel = ((float)posY - TerraGenConfig.seaLevel) / ((float)mapheight - TerraGenConfig.seaLevel);
            float fertilityRel = TerraGenConfig.GetFertilityFromUnscaledTemp((int)(rainRel * 255), unscaledTemp, heightRel) / 255f;

            double posRand = (double)GameMath.MurmurHash3(pos.X, 1, pos.Z) / int.MaxValue;
            posRand = (posRand + 1) * blockLayerConfig.blockLayerTransitionSize;

            for (int j = 0; j < blockLayerConfig.Blocklayers.Length; j++)
            {
                if (underWaterDepth < 0)
                {
                    BlockLayer bl = blockLayerConfig.Blocklayers[j];
                    float yDist = bl.CalcYDistance(posY, mapheight);
                    float trfDist = bl.CalcTrfDistance(temperature, rainRel, fertilityRel);

                    if (trfDist + yDist > posRand) continue;

                    int blockId = bl.GetBlockId(posRand, temperature, rainRel, fertilityRel, rockBlockId, pos);
                    if (blockId != 0)
                    {
                        if (forDepth-- > 0) continue;
                        return blocks[blockId];
                    }
                } else {

                    if (j >= blockLayerConfig.LakeBedLayer.BlockCodeByMin.Length) continue;
                    LakeBedBlockCodeByMin lbbc = blockLayerConfig.LakeBedLayer.BlockCodeByMin[j];
                    if (!lbbc.Suitable(temperature, rainRel, (float)posY / mapheight, (float)posRand)) continue;
                    if (underWaterDepth-- > 0) continue;

                    return blocks[lbbc.GetBlockForMotherRock(rockBlockId)];
                }
                
            }

            return defaultBlock;
        }

        public override BlockSchematic ClonePacked()
        {
            BlockSchematicStructure cloned = new BlockSchematicStructure();
            cloned.SizeX = SizeX;
            cloned.SizeY = SizeY;
            cloned.SizeZ = SizeZ;
            cloned.OffsetY = OffsetY;
            cloned.GameVersion = GameVersion;
            cloned.BlockCodes = new Dictionary<int, AssetLocation>(BlockCodes);
            cloned.ItemCodes = new Dictionary<int, AssetLocation>(ItemCodes);
            cloned.Indices = new List<uint>(Indices);
            cloned.BlockIds = new List<int>(BlockIds);
            cloned.BlockEntities = new Dictionary<uint, string>(BlockEntities);
            cloned.Entities = new List<string>(Entities);
            cloned.ReplaceMode = ReplaceMode;
            cloned.FromFileName = FromFileName;
            cloned.EntranceRotation = EntranceRotation;
            cloned.DecorIndices = new List<uint>(DecorIndices);
            cloned.DecorIds = new List<int>(DecorIds);

            return cloned;
        }

    }
}
