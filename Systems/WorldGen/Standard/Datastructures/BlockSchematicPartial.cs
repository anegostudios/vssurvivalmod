using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class BlockSchematicPartial : BlockSchematicStructure
    {
        public List<Entity> EntitiesDecoded;

        public virtual int PlacePartial(IServerChunk[] chunks, IWorldGenBlockAccessor blockAccessor, IWorldAccessor worldForResolve, int chunkX, int chunkZ, BlockPos startPos, EnumReplaceMode mode, bool replaceMeta)
        {
            int chunksize = blockAccessor.ChunkSize;
            Rectanglei rect = new Rectanglei(chunkX * chunksize, chunkZ * chunksize, chunksize, chunksize);

            if (!rect.IntersectsOrTouches(startPos.X, startPos.Z, startPos.X + SizeX, startPos.Z + SizeZ)) return 0;

            int placed = 0;
            BlockPos curPos = new BlockPos();

            int i = -1;
            foreach (uint index in Indices)
            {
                i++;   // increment i first, because we have various continue statements

                int posX = startPos.X + (int)(index & 0x1ff);
                int posZ = startPos.Z + (int)((index >> 10) & 0x1ff);

                if (!rect.Contains(posX, posZ)) continue;
                int posY = startPos.Y + (int)((index >> 20) & 0x1ff);

                int storedBlockid = BlockIds[i];
                AssetLocation blockCode = BlockCodes[storedBlockid];

                Block newBlock = blockAccessor.GetBlock(blockCode);
                if (newBlock == null || (replaceMeta && newBlock == undergroundBlock)) continue;

                int blockId = replaceMeta && (newBlock == fillerBlock || newBlock == pathwayBlock) ? empty : newBlock.BlockId;

                IChunkBlocks chunkData = chunks[posY / chunksize].Data;
                int posIndex = ((posY % chunksize) * chunksize + (posZ % chunksize)) * chunksize + (posX % chunksize);

                if (newBlock.ForFluidsLayer)
                {
                    chunkData.SetFluid(posIndex, blockId);
                }
                else
                {
                    chunkData.SetFluid(posIndex, 0);
                    chunkData[posIndex] = blockId;
                }

                if (newBlock.LightHsv[2] > 0)
                {
                    curPos.Set(posX, posY, posZ);
                    Block oldBlock = blockAccessor.GetBlock(curPos);
                    blockAccessor.ScheduleBlockLightUpdate(curPos.Copy(), oldBlock.BlockId, newBlock.BlockId);
                }

                placed++;
            }

            PlaceDecors(blockAccessor, startPos, rect);

            int schematicSeed = worldForResolve.Rand.Next();

            foreach (var val in BlockEntities)
            {
                uint index = val.Key;
                int posX = startPos.X + (int)(index & 0x1ff);
                int posZ = startPos.Z + (int)((index >> 10) & 0x1ff);

                if (!rect.Contains(posX, posZ)) continue;
                int posY = startPos.Y + (int)((index >> 20) & 0x1ff);

                curPos.Set(posX, posY, posZ);
                BlockEntity be = blockAccessor.GetBlockEntity(curPos);

                // Block entities need to be manually initialized for world gen block access
                if (be == null && blockAccessor is IWorldGenBlockAccessor)
                {
                    Block block = blockAccessor.GetBlock(curPos, BlockLayersAccess.Solid);

                    if (block.EntityClass != null)
                    {
                        blockAccessor.SpawnBlockEntity(block.EntityClass, curPos);
                        be = blockAccessor.GetBlockEntity(curPos);
                    }
                }

                if (be != null)
                {
                    Block block = blockAccessor.GetBlock(curPos, BlockLayersAccess.Solid);
                    if (block.EntityClass != worldForResolve.ClassRegistry.GetBlockEntityClass(be.GetType()))
                    {
                        worldForResolve.Logger.Warning("Could not import block entity data for schematic at {0}. There is already {1}, expected {2}. Probably overlapping ruins.", curPos, be.GetType(), block.EntityClass);
                        continue;
                    }

                    ITreeAttribute tree = DecodeBlockEntityData(val.Value);
                    tree.SetInt("posx", curPos.X);
                    tree.SetInt("posy", curPos.Y);
                    tree.SetInt("posz", curPos.Z);

                    be.FromTreeAttributes(tree, worldForResolve);
                    be.OnLoadCollectibleMappings(worldForResolve, BlockCodes, ItemCodes, schematicSeed);
                    be.OnPlacementBySchematic(worldForResolve.Api as ICoreServerAPI, blockAccessor, curPos);
                }
            }

            if (EntitiesDecoded == null) DecodeEntities(worldForResolve, startPos);

            foreach (Entity entity in EntitiesDecoded)
            {
                if (rect.Contains((int)entity.Pos.X, (int)entity.Pos.Z))
                {
                    // Not ideal but whatever
                    if (blockAccessor is IWorldGenBlockAccessor)
                    {
                        blockAccessor.AddEntity(entity);
                        entity.OnInitialized += () => entity.OnLoadCollectibleMappings(worldForResolve, BlockCodes, ItemCodes, schematicSeed);
                    }
                    else
                    {
                        worldForResolve.SpawnEntity(entity);
                        entity.OnLoadCollectibleMappings(worldForResolve, BlockCodes, ItemCodes, schematicSeed);
                    }
                }
            }

            return placed;
        }

        private void DecodeEntities(IWorldAccessor worldForResolve, BlockPos startPos)
        {
            EntitiesDecoded = new List<Entity>(Entities.Count);
            foreach (string entityData in Entities)
            {
                using (MemoryStream ms = new MemoryStream(Ascii85.Decode(entityData)))
                {
                    BinaryReader reader = new BinaryReader(ms);

                    string className = reader.ReadString();
                    Entity entity = worldForResolve.ClassRegistry.CreateEntity(className);
                    entity.Api = worldForResolve.Api;   // FromBytes works better for some entities (e.g. EntityArmorStand) if the Api is already set
                    entity.FromBytes(reader, false);
                    entity.DidImportOrExport(startPos);

                    EntitiesDecoded.Add(entity);
                }
            }
        }

        public override BlockSchematic ClonePacked()
        {
            BlockSchematicPartial cloned = new BlockSchematicPartial();

            cloned.SizeX = SizeX;
            cloned.SizeY = SizeY;
            cloned.SizeZ = SizeZ;
            cloned.BlockCodes = new Dictionary<int, AssetLocation>(BlockCodes);
            cloned.ItemCodes = new Dictionary<int, AssetLocation>(ItemCodes);
            cloned.Indices = new List<uint>(Indices);
            cloned.BlockIds = new List<int>(BlockIds);
            cloned.BlockEntities = new Dictionary<uint, string>(BlockEntities);
            cloned.ReplaceMode = ReplaceMode;
            cloned.FromFileName = FromFileName;

            return cloned;
        }


    }
}
