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
    public class BlockSchematicStructure : BlockSchematic
    {
        public Block[,,] blocksByPos;
        public Dictionary<ushort, Block> blockRemap = new Dictionary<ushort, Block>();
        RockStrataVariant dummyRock = new RockStrataVariant() { SoilpH = 6.5f, WeatheringFactor = 1f };
        Random rnd = new Random();
        public BlockLayerConfig blockLayerConfig;
        int mapheight;

        PlaceBlockDelegate handler = null;

        public override void Init(IBlockAccessor blockAccessor)
        {
            base.Init(blockAccessor);

            mapheight = blockAccessor.MapSizeY;

            blocksByPos = new Block[SizeX + 1, SizeY + 1, SizeZ + 1];

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
                    handler = PlaceReplaceAllReplaceMeta;
                    break;

                case EnumReplaceMode.Replaceable:
                    handler = PlaceReplaceableReplaceMeta;
                    break;

                case EnumReplaceMode.ReplaceAllNoAir:
                    handler = PlaceReplaceAllNoAirReplaceMeta;
                    break;

                case EnumReplaceMode.ReplaceOnlyAir:
                    handler = PlaceReplaceOnlyAirReplaceMeta;
                    break;
            }
        }

        /// <summary>
        /// For placement of ruins during worldgen, replaces the topsoil with the area specific soil (e.g. sand)
        /// </summary>
        /// <param name="blockAccessor"></param>
        /// <param name="blocks"></param>
        /// <param name="startPos"></param>
        /// <param name="climateUpLeft"></param>
        /// <param name="climateUpRight"></param>
        /// <param name="climateBotLeft"></param>
        /// <param name="climateBotRight"></param>
        /// <param name="replaceblockids"></param>
        /// <returns></returns>
        public int PlaceRespectingBlockLayers(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, int climateUpLeft, int climateUpRight, int climateBotLeft, int climateBotRight, ushort[] replaceblockids)
        {
            BlockPos curPos = new BlockPos();
            int placed = 0;
            int chunksize = blockAccessor.ChunkSize;
            

            for (int x = 0; x < SizeX; x++)
            {
                for (int z = 0; z < SizeZ; z++)
                {
                    curPos.Set(x + startPos.X, startPos.Y, z + startPos.Z);
                    ushort rockblockid = blockAccessor.GetMapChunkAtBlockPos(curPos).TopRockIdMap[(curPos.Z % chunksize) * chunksize + curPos.X % chunksize];                    
                    int depth = 0;

                    for (int y = SizeY - 1; y >= 0; y--)
                    {
                        curPos.Set(x + startPos.X, y + startPos.Y, z + startPos.Z);
                        Block newBlock = blocksByPos[x, y, z];
                        if (newBlock == null) continue;

                        if (newBlock.Replaceable < 1000)
                        {
                            if (replaceblockids.Length > depth && newBlock.BlockId == replaceblockids[depth])
                            {
                                int climate = GameMath.BiLerpRgbColor((float)x / chunksize, (float)z / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

                                newBlock = GetBlockLayerBlock((climate >> 8) & 0xff, (climate >> 16) & 0xff, startPos.Y, rockblockid, depth, newBlock, worldForCollectibleResolve.Blocks);
                            }
                            depth++;
                        }

                        Block oldBlock = blockAccessor.GetBlock(curPos);
                        placed += handler(blockAccessor, curPos, oldBlock, newBlock);

                        byte[] lightHsv = newBlock.GetLightHsv(blockAccessor, curPos);

                        if (lightHsv[2] > 0 && blockAccessor is IWorldGenBlockAccessor)
                        {
                            int chunkSize = blockAccessor.ChunkSize;
                            ((IWorldGenBlockAccessor)blockAccessor).ScheduleBlockLightUpdate(curPos.Copy(), oldBlock.BlockId, newBlock.BlockId);
                        }
                    }
                }
            }

            foreach (var val in BlockEntities)
            {
                uint index = val.Key;
                int dx = (int)(index & 0x1ff);
                int dy = (int)((index >> 20) & 0x1ff);
                int dz = (int)((index >> 10) & 0x1ff);

                curPos.Set(startPos.X + dx, startPos.Y + dy, startPos.Z + dz);

                BlockEntity be = blockAccessor.GetBlockEntity(curPos);
                if (be != null)
                {
                    be.FromTreeAtributes(DecodeBlockEntityData(val.Value), worldForCollectibleResolve);
                    be.OnLoadCollectibleMappings(worldForCollectibleResolve, BlockCodes, ItemCodes);
                    be.pos = curPos.Copy();
                }
            }

            return placed;
        }




        private Block GetBlockLayerBlock(int unscaledRain, int unscaledTemp, int posY, ushort firstBlockId, int forDepth, Block defaultBlock, Block[] blocks)
        {
            float temperature = TerraGenConfig.GetScaledAdjustedTemperatureFloat(unscaledTemp, posY - TerraGenConfig.seaLevel);
            float rainRel = TerraGenConfig.GetRainFall(unscaledRain, posY) / 255f;
            float heightRel = ((float)posY - TerraGenConfig.seaLevel) / ((float)mapheight - TerraGenConfig.seaLevel);
            float fertilityRel = TerraGenConfig.GetFertility2((int)(rainRel * 255), unscaledTemp, heightRel) / 255f;

            for (int j = forDepth; j < blockLayerConfig.Blocklayers.Length; j++)
            {
                BlockLayer bl = blockLayerConfig.Blocklayers[j];

                if (
                    temperature >= bl.MinTemp && temperature <= bl.MaxTemp &&
                    rainRel >= bl.MinRain && rainRel <= bl.MaxRain &&
                    fertilityRel >= bl.MinFertility && fertilityRel <= bl.MaxFertility &&
                    (float)posY / mapheight <= bl.MaxY
                )
                {
                    ushort blockId = bl.GetBlockId(temperature, rainRel, fertilityRel, firstBlockId);
                    if (blockId != 0)
                    {
                        return blocks[blockId];
                    }
                }
            }

            return defaultBlock;
        }


        public new BlockSchematicStructure Clone()
        {
            BlockSchematicStructure cloned = new BlockSchematicStructure();

            cloned.SizeX = SizeX;
            cloned.SizeY = SizeY;
            cloned.SizeZ = SizeZ;
            cloned.BlockCodes = new Dictionary<int, AssetLocation>(BlockCodes);
            cloned.ItemCodes = new Dictionary<int, AssetLocation>(ItemCodes);
            cloned.Indices = new List<uint>(Indices);
            cloned.BlockIds = new List<int>(BlockIds);
            cloned.BlockEntities = new Dictionary<uint, string>(BlockEntities);
            cloned.ReplaceMode = ReplaceMode;

            return cloned;
        }

    }
}
