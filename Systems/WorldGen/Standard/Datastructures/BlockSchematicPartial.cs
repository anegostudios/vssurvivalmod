﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class BlockSchematicPartial : BlockSchematicStructure
    {
        public List<Entity> EntitiesDecoded;

        public virtual int PlacePartial(IServerChunk[] chunks, IWorldGenBlockAccessor blockAccessor,
            IWorldAccessor worldForResolve, int chunkX, int chunkZ, BlockPos startPos, EnumReplaceMode mode, EnumStructurePlacement? structurePlacement,
            bool replaceMeta, bool resolveImports, Dictionary<int, Dictionary<int, int>> resolvedRockTypeRemaps = null,
            int[] replaceWithBlockLayersBlockids = null, Block rockBlock = null)
        {
            const int chunksize = GlobalConstants.ChunkSize;
            var rect = new Rectanglei(chunkX * chunksize, chunkZ * chunksize, chunksize, chunksize);

            if (!rect.IntersectsOrTouches(startPos.X, startPos.Z, startPos.X + SizeX, startPos.Z + SizeZ)) return 0;
            var placed = 0;

            var curPos = new BlockPos();

            int climateUpLeft = 0, climateUpRight = 0, climateBotLeft = 0, climateBotRight = 0;
            if(replaceWithBlockLayersBlockids != null)
            {
                var regionChunkSize = blockAccessor.RegionSize / chunksize;
                var region = chunks[0].MapChunk.MapRegion;
                var climateMap = region.ClimateMap;
                var rlX = chunkX % regionChunkSize;
                var rlZ = chunkZ % regionChunkSize;
                var facC = (float)climateMap.InnerSize / regionChunkSize;
                climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
                climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
                climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
                climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));
            }

            if (genBlockLayers == null) genBlockLayers = worldForResolve.Api.ModLoader.GetModSystem<GenBlockLayers>();

            int climate;
            var rockblockid = rockBlock?.BlockId ?? chunks[0].MapChunk.TopRockIdMap[15 * chunksize + 15];


            int i = -1;
            int dy, dx, dz;
            var rainHeightMap = chunks[0].MapChunk.RainHeightMap;
            var terrainHeightMap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

            foreach (uint index in Indices)
            {
                i++;   // increment i first, because we have various continue statements

                dx = (int)(index & PosBitMask);
                int posX = startPos.X + dx;
                dz = (int)((index >> 10) & PosBitMask);
                int posZ = startPos.Z + dz;

                if (!rect.Contains(posX, posZ)) continue;
                dy = (int)((index >> 20) & PosBitMask);
                int posY = startPos.Y + dy;
                int storedBlockid = BlockIds[i];
                AssetLocation blockCode = BlockCodes[storedBlockid];

                Block newBlock = blockAccessor.GetBlock(blockCode);
                if (newBlock == null || (replaceMeta && (newBlock == undergroundBlock || newBlock == abovegroundBlock))) continue;

                int blockId = replaceMeta && (newBlock == fillerBlock || newBlock == pathwayBlock) ? empty : newBlock.BlockId;

                IChunkBlocks chunkData = chunks[posY / chunksize].Data;
                int posIndex = ((posY % chunksize) * chunksize + (posZ % chunksize)) * chunksize + (posX % chunksize);

                if (replaceWithBlockLayersBlockids != null && replaceWithBlockLayersBlockids.Contains(blockId))
                {
                    curPos.Set(posX, posY, posZ);
                    climate = GameMath.BiLerpRgbColor(
                        GameMath.Clamp((posX % chunksize) / (float)chunksize, 0, 1),
                        GameMath.Clamp((posZ % chunksize) / (float)chunksize, 0, 1),
                        climateUpLeft, climateUpRight, climateBotLeft, climateBotRight
                    );
                    var depth = 0;

                    // if we are not the top block we need to get the layerblock for depth 1 -> soil without grass
                    if (dy + 1 < SizeY)
                    {
                        var aboveBlock = blocksByPos[dx, dy + 1, dz];
                        if (aboveBlock != null && aboveBlock.SideSolid[BlockFacing.DOWN.Index] &&
                            aboveBlock.BlockMaterial != EnumBlockMaterial.Wood && aboveBlock.BlockMaterial != EnumBlockMaterial.Snow &&
                            aboveBlock.BlockMaterial != EnumBlockMaterial.Ice)
                        {
                            depth = 1;
                        }
                    }

                    var layerBlock = GetBlockLayerBlock((climate >> 8) & 0xff, (climate >> 16) & 0xff, curPos.Y - 1, rockblockid, depth, null,
                        worldForResolve.Blocks, curPos, -1);
                    blockId = layerBlock?.Id ?? rockblockid;
                    newBlock = blockAccessor.GetBlock(blockId);
                }


                if (resolvedRockTypeRemaps != null)
                {
                    Dictionary<int, int> replaceByBlock;
                    if (resolvedRockTypeRemaps.TryGetValue(blockId, out replaceByBlock))
                    {
                        if (replaceByBlock.TryGetValue(rockblockid, out var newBlockId))
                        {
                            blockId = newBlockId;
                            newBlock = blockAccessor.GetBlock(blockId);
                        }
                    }
                }


                if (structurePlacement == EnumStructurePlacement.Surface)
                {
                    var oldBlock = blockAccessor.GetBlock(chunkData[posIndex]);
                    // this prevents grass from being placed where a solid block was
                    if(oldBlock.BlockMaterial is EnumBlockMaterial.Soil or EnumBlockMaterial.Sand or EnumBlockMaterial.Stone && newBlock.BlockMaterial == EnumBlockMaterial.Plant) continue;

                    var oldRainPermeable = oldBlock.RainPermeable;
                    var newRainPermeable = newBlock.RainPermeable;
                    var posIndexRain = (posZ % chunksize) * chunksize + (posX % chunksize);

                    if (oldRainPermeable && !newRainPermeable)
                    {
                        rainHeightMap[posIndexRain] = Math.Max(rainHeightMap[posIndexRain], (ushort)posY);
                    }
                    if (!oldRainPermeable && newRainPermeable && rainHeightMap[posIndexRain] == posY)
                    {
                        var downY = (ushort)posY;
                        var posIndexBelow = ((downY % chunksize) * chunksize + (posZ % chunksize)) * chunksize + (posX % chunksize);
                        while (blockAccessor.GetBlock(chunkData[posIndexBelow]).RainPermeable && downY > 0)
                        {
                            downY--;
                            posIndexBelow = ((downY % chunksize) * chunksize + (posZ % chunksize)) * chunksize + (posX % chunksize);
                        }

                        rainHeightMap[posIndexRain] = downY;
                    }
                    var oldSolid = oldBlock.SideSolid[BlockFacing.UP.Index];
                    var newSolid = newBlock.SideSolid[BlockFacing.UP.Index];

                    if (!oldSolid && newSolid)
                    {
                        terrainHeightMap[posIndexRain] = Math.Max(terrainHeightMap[posIndexRain], (ushort)posY);
                    }
                    if (oldSolid && !newSolid && terrainHeightMap[posIndexRain] == posY)
                    {
                        var downY = (ushort)(posY - 1);
                        var posIndexBelow = ((downY % chunksize) * chunksize + (posZ % chunksize)) * chunksize + (posX % chunksize);
                        while (blockAccessor.GetBlock(chunkData[posIndexBelow]).RainPermeable && downY > 0)
                        {
                            downY--;
                            posIndexBelow = ((downY % chunksize) * chunksize + (posZ % chunksize)) * chunksize + (posX % chunksize);
                        }

                        terrainHeightMap[posIndexRain] = downY;
                    }
                }

                // if we only have a fluid block we need to clear the previous block so we can place fluids
                // schematics have solid block first and second fluid in the Indices array and the index (pos) is the same
                if (newBlock.ForFluidsLayer && index != Indices[i-1])
                {
                    chunkData[posIndex] = 0;
                }
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
                    blockAccessor.ScheduleBlockLightUpdate(curPos.Copy(), 0, newBlock.BlockId);
                }

                placed++;
            }

            PlaceDecors(blockAccessor, startPos, rect);

            int schematicSeed = worldForResolve.Rand.Next();

            foreach (var val in BlockEntities)
            {
                uint index = val.Key;
                int posX = startPos.X + (int)(index & PosBitMask);
                int posZ = startPos.Z + (int)((index >> 10) & PosBitMask);

                if (!rect.Contains(posX, posZ)) continue;
                int posY = startPos.Y + (int)((index >> 20) & PosBitMask);

                curPos.Set(posX, posY, posZ);
                BlockEntity be = blockAccessor.GetBlockEntity(curPos);

                // Block entities need to be manually initialized for world gen block access
                if (be == null && blockAccessor != null)
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
                    be.OnLoadCollectibleMappings(worldForResolve, BlockCodes, ItemCodes, schematicSeed, resolveImports);
                    be.OnPlacementBySchematic(worldForResolve.Api as ICoreServerAPI, blockAccessor, curPos, resolvedRockTypeRemaps, rockblockid, null, resolveImports);
                }
            }

            if (EntitiesDecoded == null) DecodeEntities(worldForResolve, startPos, worldForResolve as IServerWorldAccessor);

            foreach (Entity entity in EntitiesDecoded)
            {
                if (rect.Contains((int)entity.Pos.X, (int)entity.Pos.Z))
                {
                    // Not ideal but whatever
                    if (blockAccessor is IWorldGenBlockAccessor)
                    {
                        blockAccessor.AddEntity(entity);
                        entity.OnInitialized += () => entity.OnLoadCollectibleMappings(worldForResolve, BlockCodes, ItemCodes, schematicSeed, resolveImports);
                    }
                    else
                    {
                        worldForResolve.SpawnEntity(entity);
                        entity.OnLoadCollectibleMappings(worldForResolve, BlockCodes, ItemCodes, schematicSeed, resolveImports);
                    }
                }
            }

            return placed;
        }

        private void DecodeEntities(IWorldAccessor worldForResolve, BlockPos startPos, IServerWorldAccessor serverWorldAccessor)
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
                    entity.FromBytes(reader, false, serverWorldAccessor.RemappedEntities);
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
            cloned.OffsetY = OffsetY;

            cloned.GameVersion = GameVersion;
            cloned.FromFileName = FromFileName;

            cloned.BlockCodes = new Dictionary<int, AssetLocation>(BlockCodes);
            cloned.ItemCodes = new Dictionary<int, AssetLocation>(ItemCodes);
            cloned.Indices = new List<uint>(Indices);
            cloned.BlockIds = new List<int>(BlockIds);

            cloned.BlockEntities = new Dictionary<uint, string>(BlockEntities);
            cloned.Entities = new List<string>(Entities);

            cloned.DecorIndices = new List<uint>(DecorIndices);
            cloned.DecorIds = new List<long>(DecorIds);

            cloned.ReplaceMode = ReplaceMode;
            cloned.EntranceRotation = EntranceRotation;

            return cloned;
        }
    }
}
