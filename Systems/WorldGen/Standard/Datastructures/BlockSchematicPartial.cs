using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class BlockSchematicPartial : BlockSchematicStructure
    {
        public List<Entity> EntitiesDecoded;

        static BlockPos Zero = new BlockPos(0, 0, 0);

        public virtual int PlacePartial(IServerChunk[] chunks, IWorldGenBlockAccessor blockAccessor,
            IWorldAccessor worldForResolve, int chunkX, int chunkZ, BlockPos startPos, EnumReplaceMode mode, EnumStructurePlacement? structurePlacement,
            bool replaceMeta, bool resolveImports, Dictionary<int, Dictionary<int, int>> resolvedRockTypeRemaps = null,
            int[] replaceWithBlockLayersBlockids = null, Block rockBlock = null, bool disableSurfaceTerrainBlending = false)
        {
            Unpack(worldForResolve.Api);
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

            var rockblockid = rockBlock?.BlockId ?? chunks[0].MapChunk.TopRockIdMap[15 * chunksize + 15];


            int i = -1;
            int dy, dx, dz;
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
                if (newBlock == null || (replaceMeta && (newBlock.Id == UndergroundBlockId || newBlock.Id == AbovegroundBlockId))) continue;

                int blockId = replaceMeta && IsFillerOrPath(newBlock) ? empty : newBlock.BlockId;

                IChunkBlocks chunkData = chunks[posY / chunksize].Data;
                int posIndex = ((posY % chunksize) * chunksize + (posZ % chunksize)) * chunksize + (posX % chunksize);

                // update blocks that will be exposed to air to their top surface variant ex soil with grass
                if (structurePlacement is EnumStructurePlacement.SurfaceRuin && newBlock.Id == FillerBlockId)
                {
                    var belowIndex = (uint)(((dy-1) << 20) | (dz << 10) | dx);
                    if (!Indices.Contains(belowIndex))
                    {
                        var belowPos = (((posY - 1) % chunksize) * chunksize + (posZ % chunksize)) * chunksize + (posX % chunksize);
                        var belowBlock = blockAccessor.GetBlock(chunkData[belowPos]);

                        if (belowBlock.BlockMaterial == EnumBlockMaterial.Soil)
                        {
                            var belowBlockId = GetReplaceLayerBlockId(blockAccessor, worldForResolve, replaceWithBlockLayersBlockids, belowBlock.Id, curPos, posX, posY, posZ, chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, dy, dx, dz, rockblockid, ref belowBlock, true);

                            chunkData[belowPos] = belowBlockId;
                        }
                    }
                }

                blockId = GetReplaceLayerBlockId(blockAccessor, worldForResolve, replaceWithBlockLayersBlockids, blockId, curPos, posX, posY, posZ, chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, dy, dx, dz, rockblockid, ref newBlock);

                blockId = GetRocktypeBlockId(blockAccessor, resolvedRockTypeRemaps, blockId, rockblockid, ref newBlock);

                // surface sits on top of terrain and so needs to blend with existing one better
                // do not place grass or loose rocks below terrain if there was a solid blocks
                if (!disableSurfaceTerrainBlending && structurePlacement is EnumStructurePlacement.Surface)
                {
                    var oldBlock = blockAccessor.GetBlock(chunkData[posIndex]);
                    // if we have solid blocks at the same pos keep the old one to blend in better with the terrain
                    if((newBlock.Replaceable >= 5500 || newBlock.BlockMaterial == EnumBlockMaterial.Plant) && oldBlock.Replaceable < newBlock.Replaceable && !newBlock.IsLiquid()) continue;
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
                    blockAccessor.ScheduleBlockLightUpdate(curPos, 0, newBlock.BlockId);
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

                    var climate = GameMath.BiLerpRgbColor(
                        GameMath.Clamp((posX % chunksize) / (float)chunksize, 0, 1),
                        GameMath.Clamp((posZ % chunksize) / (float)chunksize, 0, 1),
                        climateUpLeft, climateUpRight, climateBotLeft, climateBotRight
                    );
                    var layerBlock = GetBlockLayerBlock((climate >> 8) & 0xff, (climate >> 16) & 0xff, curPos.Y, rockblockid, 0, null,
                        worldForResolve.Blocks, curPos, -1);

                    be.FromTreeAttributes(tree, worldForResolve);
                    be.OnLoadCollectibleMappings(worldForResolve, BlockCodes, ItemCodes, schematicSeed, resolveImports);
                    be.OnPlacementBySchematic(worldForResolve.Api as ICoreServerAPI, blockAccessor, curPos, resolvedRockTypeRemaps, rockblockid, layerBlock, resolveImports);
                }
            }

            if (EntitiesDecoded == null) DecodeEntities(worldForResolve, startPos, worldForResolve as IServerWorldAccessor);

            foreach (Entity entity in EntitiesDecoded)
            {
                if (rect.Contains((int)entity.Pos.X, (int)entity.Pos.Z))
                {
                    if (OriginalPos != null)
                    {
                        var prevOffset = entity.WatchedAttributes.GetBlockPos("importOffset", Zero);
                        entity.WatchedAttributes.SetBlockPos("importOffset", startPos - OriginalPos + prevOffset);
                    }
                    // Not ideal but whatever
                    if (blockAccessor is IWorldGenBlockAccessor)
                    {
                        blockAccessor.AddEntity(entity);
                        if (!entity.TryEarlyLoadCollectibleMappings(worldForResolve, BlockCodes, ItemCodes, schematicSeed, resolveImports))
                        {
                            entity.OnInitialized += () => entity.OnLoadCollectibleMappings(worldForResolve, BlockCodes, ItemCodes, schematicSeed, resolveImports);
                        }
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

        private int GetReplaceLayerBlockId(IWorldGenBlockAccessor blockAccessor, IWorldAccessor worldForResolve, int[] replaceWithBlockLayersBlockids,
            int blockId, BlockPos curPos, int posX, int posY, int posZ, int chunksize, int climateUpLeft, int climateUpRight, int climateBotLeft,
            int climateBotRight, int dy, int dx, int dz, int rockblockid, ref Block newBlock, bool topBlockOnly = false)
        {
            if (replaceWithBlockLayersBlockids != null && replaceWithBlockLayersBlockids.Contains(blockId))
            {
                curPos.Set(posX, posY, posZ);
                var climate = GameMath.BiLerpRgbColor(
                    GameMath.Clamp((posX % chunksize) / (float)chunksize, 0, 1),
                    GameMath.Clamp((posZ % chunksize) / (float)chunksize, 0, 1),
                    climateUpLeft, climateUpRight, climateBotLeft, climateBotRight
                );
                var depth = 0;

                // if we are not the top block we need to get the layerblock for depth 1 -> soil without grass
                if (dy + 1 < SizeY && !topBlockOnly)
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

            return blockId;
        }

        private static int GetRocktypeBlockId(IWorldGenBlockAccessor blockAccessor, Dictionary<int, Dictionary<int, int>> resolvedRockTypeRemaps, int blockId, int rockblockid,
            ref Block newBlock)
        {
            if (resolvedRockTypeRemaps != null)
            {
                if (resolvedRockTypeRemaps.TryGetValue(blockId, out Dictionary<int, int> replaceByBlock))
                {
                    if (replaceByBlock.TryGetValue(rockblockid, out var newBlockId))
                    {
                        blockId = newBlockId;
                        newBlock = blockAccessor.GetBlock(blockId);
                    }
                }
            }

            return blockId;
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
            cloned.OriginalPos = OriginalPos;


            return cloned;
        }
    }
}
