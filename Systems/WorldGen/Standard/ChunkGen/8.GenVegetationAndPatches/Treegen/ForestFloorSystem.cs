using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class ForestFloorSystem
    {
        private ICoreServerAPI sapi;
        IServerWorldAccessor worldAccessor;
        private IBlockAccessor api;

        // Used by forest floor system: reusable array, and various blockIDs for tall grass etc.

        // Make the reusable array threadsafe, in case worldgen is ever called from 2 different threads (?background gen, and command execution for regen?)
        ThreadLocal<short[]> outlineThreadSafe = new ThreadLocal<short[]>(() => new short[1089]);

        int[] forestBlocks;

        List<BlockPatch> underTreePatches;
        List<BlockPatch> onTreePatches;

        GenVegetationAndPatches genPatchesSystem;

        public ForestFloorSystem(ICoreServerAPI api)
        {
            this.sapi = api;
            this.worldAccessor = sapi.World;

            genPatchesSystem = sapi.ModLoader.GetModSystem<GenVegetationAndPatches>();
            
        }

        internal short[] GetOutline()
        {
            return outlineThreadSafe.Value;
        }

        public void SetBlockPatches(BlockPatchConfig bpc)
        {
            forestBlocks = BlockForestFloor.InitialiseForestBlocks(worldAccessor);

            underTreePatches = new List<BlockPatch>();
            onTreePatches = new List<BlockPatch>();

            for (int i = 0; i < bpc.Patches.Length; i++)
            {
                BlockPatch blockPatch = bpc.Patches[i];
                if (blockPatch.Placement == EnumBlockPatchPlacement.UnderTrees || blockPatch.Placement == EnumBlockPatchPlacement.OnSurfacePlusUnderTrees)
                {
                    underTreePatches.Add(blockPatch);
                }
                if (blockPatch.Placement == EnumBlockPatchPlacement.OnTrees)
                {
                    onTreePatches.Add(blockPatch);
                }
            }
        }

        internal void ClearOutline()
        {
            // Clear the re-usable outline array
            short[] outline = outlineThreadSafe.Value;
            for (int i = 0; i < outline.Length; i++) outline[i] = 0;
        }

        internal void CreateForestFloor(IBlockAccessor blockAccessor, TreeGenConfig config, BlockPos pos, LCGRandom rnd, int treesInChunkGenerated)
        {
            int grassLevelOffset = 0;
            // More grass coverage for jungles
            ClimateCondition climate = blockAccessor.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues);
            if (climate.Temperature > 24 && climate.Rainfall > 160) grassLevelOffset = 2;

            short[] outline = outlineThreadSafe.Value;
            this.api = blockAccessor;

            float forestness = climate.ForestDensity * climate.ForestDensity * 4 * (climate.Fertility + 0.25f);


            // Only replace soil with forestFloor in certain climate conditions
            if (climate.Fertility <= 0.25 || forestness <= 0.4) return;
           
            // Otherwise adjust the strength of the effect according to forest density and fertility (fertility is higher for tropical forests)
            for (int i = 0; i < outline.Length; i++) outline[i] = (short)(outline[i] * forestness + 0.3f);

            // Blend the canopy outline outwards from the center in a way that ensures smoothness
            for (int pass = 0; pass < 7; pass++)
            {
                bool noChange = true;

                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (x == 0 && z == 0) continue;
                        int o = Math.Min((int)outline[(16 + z) * 33 + (16 + x)], 18 * 9);
                        if (o == 0) continue;

                        int n1 = (17 + z) * 33 + (16 + x);
                        int n2 = (16 + z) * 33 + (17 + x);
                        if (outline[n1] < o - 18)
                        {
                            outline[n1] = (short)(o - 18);
                            noChange = false;
                        }
                        if (outline[n2] < o - 18)
                        {
                            outline[n2] = (short)(o - 18);
                            noChange = false;
                        }

                        o = Math.Min((int)outline[(16 - z) * 33 + (16 + x)], 18 * 9);
                        n1 = (15 - z) * 33 + (16 + x);
                        n2 = (16 - z) * 33 + (17 + x);
                        if (outline[n1] < o - 18)
                        {
                            outline[n1] = (short)(o - 18);
                            noChange = false;
                        }
                        if (outline[n2] < o - 18)
                        {
                            outline[n2] = (short)(o - 18);
                            noChange = false;
                        }
                    }

                    for (int z = 0; z < 16; z++)
                    {
                        if (x == 0 && z == 0) continue;
                        int o = Math.Min((int)outline[(16 + z) * 33 + (16 - x)], 18 * 9);
                        int n1 = (17 + z) * 33 + (16 - x);
                        int n2 = (16 + z) * 33 + (15 - x);
                        if (outline[n1] < o - 18)
                        {
                            outline[n1] = (short)(o - 18);
                            noChange = false;
                        }
                        if (outline[n2] < o - 18)
                        {
                            outline[n2] = (short)(o - 18);
                            noChange = false;
                        }

                        o = Math.Min((int)outline[(16 - z) * 33 + (16 - x)], 18 * 9);
                        n1 = (15 - z) * 33 + (16 - x);
                        n2 = (16 - z) * 33 + (15 - x);
                        if (outline[n1] < o - 18)
                        {
                            outline[n1] = (short)(o - 18);
                            noChange = false;
                        }
                        if (outline[n2] < o - 18)
                        {
                            outline[n2] = (short)(o - 18);
                            noChange = false;
                        }
                    }
                }
                if (noChange) break;
            }
            

            BlockPos currentPos = new BlockPos();
            for (int canopyIndex = 0; canopyIndex < outline.Length; canopyIndex++)
            {
                int intensity = outline[canopyIndex];
                if (intensity == 0) continue;

                int dz = canopyIndex / 33 - 16;
                int dx = canopyIndex % 33 - 16;
                currentPos.Set(pos.X + dx, pos.Y, pos.Z + dz);
                currentPos.Y = blockAccessor.GetTerrainMapheightAt(currentPos);

                if (currentPos.Y - pos.Y < 4)  //Don't place forest floor above approximate height of the canopy of this tree
                {
                    CheckAndReplaceForestFloor(currentPos, intensity, grassLevelOffset);
                }
            }

            GenPatches(blockAccessor, pos, forestness, config.Treetype, rnd);
        }


        BlockPos tmpPos = new BlockPos();
        private void GenPatches(IBlockAccessor blockAccessor, BlockPos pos, float forestNess, EnumTreeType treetype, LCGRandom rnd)
        {
            var bpc = genPatchesSystem.bpc;
            int radius = 5;
            int worldheight = blockAccessor.MapSizeY;

            int cnt = underTreePatches?.Count ?? 0;
            for (int i = 0; i < cnt; i++)
            {
                BlockPatch bPatch = underTreePatches[i];
                if (bPatch.TreeType != EnumTreeType.Any && bPatch.TreeType != treetype)
                {
                    continue;
                }

                float chance = 0.003f * forestNess * bPatch.Chance * bpc.ChanceMultiplier.nextFloat();

                //if (bPatch.blockCodes[0].Path.Contains("mushroom")) chance *= 20; - for debugging

                while (chance-- > rnd.NextDouble())
                {
                    int dx = rnd.NextInt(2 * radius) - radius;
                    int dz = rnd.NextInt(2 * radius) - radius;

                    tmpPos.Set(pos.X + dx, 0, pos.Z + dz);

                    int y = blockAccessor.GetTerrainMapheightAt(tmpPos);
                    if (y <= 0 || y >= worldheight - 8) continue;

                    tmpPos.Y = y;
                    
                    var climate = blockAccessor.GetClimateAt(tmpPos, EnumGetClimateMode.WorldGenValues);
                    if (climate == null)
                    {
                        continue;
                    }

                    if (bpc.IsPatchSuitableUnderTree(bPatch, worldheight, climate, y))
                    {
                        int regionX = pos.X / blockAccessor.RegionSize;
                        int regionZ = pos.Z / blockAccessor.RegionSize;
                        if (bPatch.MapCode != null && rnd.NextInt(255) > genPatchesSystem.GetPatchDensity(bPatch.MapCode, tmpPos.X, tmpPos.Z, blockAccessor.GetMapRegion(regionX, regionZ)))
                        {
                            continue;
                        }

                        int firstBlockId = 0;
                        bool found = true;

                        if (bPatch.BlocksByRockType != null)
                        {
                            found = false;
                            int dy = 1;
                            while (dy < 5 && y - dy > 0)
                            {
                                string lastCodePart = blockAccessor.GetBlock(tmpPos.X, y - dy, tmpPos.Z).LastCodePart();
                                if (genPatchesSystem.RockBlockIdsByType.TryGetValue(lastCodePart, out firstBlockId)) { found = true; break; }
                                dy++;
                            }
                        }

                        if (found)
                        {
                            bPatch.Generate(blockAccessor, rnd, tmpPos.X, tmpPos.Y, tmpPos.Z, firstBlockId);
                        }
                    }
                }
            }

            cnt = onTreePatches?.Count ?? 0;
            for (int i = 0; i < cnt; i++)
            {
                BlockPatch blockPatch = onTreePatches[i];

                float chance = 3 * forestNess * blockPatch.Chance * bpc.ChanceMultiplier.nextFloat();

                while (chance-- > rnd.NextDouble())
                {
                    int dx = 1 - rnd.NextInt(2) * 2;
                    int dy = rnd.NextInt(5);
                    int dz = 1 - rnd.NextInt(2) * 2;

                    tmpPos.Set(pos.X + dx, pos.Y + dy, pos.Z + dz);

                    var block = api.GetBlock(tmpPos);
                    if (block.Id != 0) continue;
                    BlockFacing facing = null;

                    for (int j = 0; j < 4; j++)
                    {
                        var f = BlockFacing.HORIZONTALS[j];
                        var nblock = api.GetBlock(tmpPos.X + f.Normali.X, tmpPos.Y, tmpPos.Z + f.Normali.Z);
                        if (nblock is BlockLog && nblock.Variant["type"] != "resin")
                        {
                            facing = f;
                            break;
                        }
                    }
                    if (facing == null) break;

                    var climate = blockAccessor.GetClimateAt(tmpPos, EnumGetClimateMode.WorldGenValues);
                    if (climate == null)
                    {
                        continue;
                    }

                    if (bpc.IsPatchSuitableUnderTree(blockPatch, worldheight, climate, tmpPos.Y))
                    {
                        int regionX = pos.X / blockAccessor.RegionSize;
                        int regionZ = pos.Z / blockAccessor.RegionSize;
                        if (blockPatch.MapCode != null && rnd.NextInt(255) > genPatchesSystem.GetPatchDensity(blockPatch.MapCode, tmpPos.X, tmpPos.Z, blockAccessor.GetMapRegion(regionX, regionZ)))
                        {
                            continue;
                        }

                        int index = rnd.NextInt(blockPatch.Blocks.Length);
                        blockPatch.Blocks[index].TryPlaceBlockForWorldGen(blockAccessor, tmpPos, facing, rnd);
                    }
                }
            }
        }



        /// <summary>
        /// Potentially replace grassy soil with sparser variant<br/>
        /// Remove all plants (flowers and tallgrass)
        /// </summary>
        /// <returns>True if a plant was removed (indicating nothing more to do in this x,z position), otherwise false</returns>
        private void CheckAndReplaceForestFloor(BlockPos pos, int intensity, int grassLevelOffset)
        {
            if (forestBlocks == null) return;

            Block soilBlock = api.GetBlock(pos);

            if (soilBlock is BlockForestFloor || soilBlock is BlockSoil)
            {
                // Update any existing forest floor blocks to which this tree also spreads - intensify forest level
                if (soilBlock is BlockForestFloor bff)
                {
                    int existingLevel = bff.CurrentLevel();
                    intensity += existingLevel * 18 - 9;
                    intensity = Math.Min(intensity, Math.Max(existingLevel * 18, (BlockForestFloor.MaxStage - 1) * 18));   // aim not to have it completely brown between trees
                }

                // Set forest soil blocks according to canopy thickness and therefore shade level
                int forestFloorBlockId;
                int level = grassLevelOffset + intensity / 18;
                if (level >= forestBlocks.Length - 1)
                {
                    forestFloorBlockId = forestBlocks[level > forestBlocks.Length ? 0 : 1];
                }
                else
                {
                    if (level == 0) level = 1;   //0 and 1 have the same look, for smoother edges
                    forestFloorBlockId = forestBlocks[forestBlocks.Length - level];
                }

                api.SetBlock(forestFloorBlockId, pos);
            }
        }



        private int GetRandomBlock(BlockPatch blockPatch)
        {
            return blockPatch.Blocks[0].Id;
        }

        private float GetDistance(ClimateCondition climate, BlockPatch variant)
        {
            float fertDist, rainDist, tempDist, forestDist;

            tempDist = Math.Abs(climate.Temperature * 2 - variant.MaxTemp - variant.MinTemp) / (variant.MaxTemp - variant.MinTemp);
            if (tempDist > 1f) return 5f;
            fertDist = Math.Abs(climate.Fertility * 2 - variant.MaxFertility - variant.MinFertility) / (variant.MaxFertility - variant.MinFertility);
            if (fertDist > 1f) return 5f;
            rainDist = Math.Abs(climate.Rainfall * 2 - variant.MaxRain - variant.MinRain) / (variant.MaxRain - variant.MinRain);
            if (rainDist > 1.3f) return 5f;
            forestDist = Math.Abs((climate.ForestDensity + 0.2f) * 2 - variant.MaxForest - variant.MinForest) / (variant.MaxForest - variant.MinForest);

            return tempDist * tempDist + fertDist * fertDist + rainDist * rainDist + forestDist * forestDist;
        }
    }
}

