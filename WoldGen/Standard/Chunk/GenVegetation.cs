using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenVegetation : ModStdWorldGen
    {
        ICoreServerAPI api;
        Random rnd;
        IBlockAccessor blockAccessor;
        WgenTreeSupplier treeSupplier;
        int worldheight;
        int chunkMapSizeY;
        int regionChunkSize;

        BlockPatchConfig bpc;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override double ExecuteOrder()
        {
            return 0.5;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            treeSupplier = new WgenTreeSupplier(api);
            api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Vegetation);
            api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            api.Event.SaveGameLoaded(GameWorldLoaded);

            

            // Call our loaded method manually if the server is already running (happens when mods are reloaded at runtime)
            if (api.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
            {
                GameWorldLoaded();
            }

            
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }

        Dictionary<string, ushort> RockBlockIdsByType;

        public void GameWorldLoaded()
        {
            LoadGlobalConfig(api);

            rnd = new Random(api.WorldManager.Seed);
            chunksize = api.WorldManager.ChunkSize;

            treeSupplier.LoadTrees();

            worldheight = api.WorldManager.MapSizeY;
            chunkMapSizeY = api.WorldManager.MapSizeY / chunksize;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;

            RockBlockIdsByType = new Dictionary<string, ushort>();
            RockstrataWorldProperty rockstrata = api.Assets.Get("worldgen/terrain/standard/rockstrata.json").ToObject<RockstrataWorldProperty>();
            for (int i = 0; i < rockstrata.Variants.Length; i++)
            {
                RockBlockIdsByType.Add(rockstrata.Variants[i].RockType, api.World.GetBlock(rockstrata.Variants[i].BlockCode).BlockId);
            }
            IAsset asset = api.Assets.Get("worldgen/terrain/standard/blockpatchconfig.json");
            bpc = asset.ToObject<BlockPatchConfig>();
            bpc.ResolveBlockIds(api, rockstrata);
        }


        ushort[] heightmap;
        int forestUpLeft;
        int forestUpRight;
        int forestBotLeft;
        int forestBotRight;

        int shrubUpLeft;
        int shrubUpRight;
        int shrubBotLeft;
        int shrubBotRight;


        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;

        BlockPos tmpPos = new BlockPos();

        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            IntMap forestMap = chunks[0].MapChunk.MapRegion.ForestMap;
            IntMap shrubMap = chunks[0].MapChunk.MapRegion.ShrubMap;
            IntMap climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            float facS = (float)shrubMap.InnerSize / regionChunkSize;
            shrubUpLeft = shrubMap.GetUnpaddedInt((int)(rlX * facS), (int)(rlZ * facS));
            shrubUpRight = shrubMap.GetUnpaddedInt((int)(rlX * facS + facS), (int)(rlZ * facS));
            shrubBotLeft = shrubMap.GetUnpaddedInt((int)(rlX * facS), (int)(rlZ * facS + facS));
            shrubBotRight = shrubMap.GetUnpaddedInt((int)(rlX * facS + facS), (int)(rlZ * facS + facS));


            // A region has 16 chunks
            // Size of the forest map is RegionSize / TerraGenConfig.forestMapScale  => 32*16 / 32  = 16 pixel
            // rlX, rlZ goes from 0..16 pixel
            // facF = 16/16 = 1
            // Get 4 pixels for chunkx, chunkz, chunkx+1 and chunkz+1 inside the map
            float facF = (float)forestMap.InnerSize / regionChunkSize;
            forestUpLeft = forestMap.GetUnpaddedInt((int)(rlX * facF), (int)(rlZ * facF));
            forestUpRight = forestMap.GetUnpaddedInt((int)(rlX * facF + facF), (int)(rlZ * facF));
            forestBotLeft = forestMap.GetUnpaddedInt((int)(rlX * facF), (int)(rlZ * facF + facF));
            forestBotRight = forestMap.GetUnpaddedInt((int)(rlX * facF + facF), (int)(rlZ * facF + facF));

            float facC = (float)climateMap.InnerSize / regionChunkSize;
            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));

            heightmap = chunks[0].MapChunk.RainHeightMap;

            if (TerraGenConfig.GenerateVegetation)
            {
                genPatches(chunkX, chunkZ, false);
                genShrubs(chunkX, chunkZ);
                genTrees(chunkX, chunkZ);
                genPatches(chunkX, chunkZ, true);
            }
        }


        void genPatches(int chunkX, int chunkZ, bool postPass)
        {
            int dx, dz, x, z;
            Block block;
            
            for (int i = 0; i < bpc.Patches.Length; i++)
            {
                BlockPatch blockPatch = bpc.Patches[i];
                if (blockPatch.PostPass != postPass) continue;

                float chance = blockPatch.Chance * bpc.ChanceMultiplier.nextFloat();

                while (chance-- > rnd.NextDouble())
                {
                    dx = rnd.Next(chunksize);
                    dz = rnd.Next(chunksize);
                    x = dx + chunkX * chunksize;
                    z = dz + chunkZ * chunksize;

                    int y = heightmap[dz * chunksize + dx];
                    if (y <= 0 || y >= worldheight - 15) continue;

                    tmpPos.Set(x, y, z);
                    block = blockAccessor.GetBlock(tmpPos);

                    // Place according to forest value
                    float forestRel = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, (float)dx / chunksize, (float)dz / chunksize) / 255f;
                    int climate = GameMath.BiLerpRgbColor((float)dx / chunksize, (float)dz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);
                    float shrubChance = GameMath.BiLerp(shrubUpLeft, shrubUpRight, shrubBotLeft, shrubBotRight, (float)dx / chunksize, (float)dz / chunksize);

                    if (bpc.IsPatchSuitableAt(blockPatch, block, api.WorldManager, climate, y, forestRel))
                    {
                        ushort firstBlockId = 0;
                        bool found = true;
                        if (blockPatch.BlocksByRockType != null)
                        {
                            found = false;
                            int dy = 1;
                            while (dy < 5 && y - dy > 0)
                            {
                                string lastCodePart = blockAccessor.GetBlock(x, y - dy, z).LastCodePart();
                                if (RockBlockIdsByType.TryGetValue(lastCodePart, out firstBlockId)) { found = true; break; }
                                dy++;
                            }
                        }

                        if (found)
                        {
                            blockPatch.Generate(blockAccessor, rnd, x, y, z, firstBlockId);
                        }
                    }
                }
            }
        }



        void genShrubs(int chunkX, int chunkZ)
        {
            int triesShrubs = (int)treeSupplier.treeGenProps.shrubsPerChunk.nextFloat();

            int dx, dz, x, z;
            Block block;

            while (triesShrubs > 0)
            {
                triesShrubs--;

                dx = rnd.Next(chunksize);
                dz = rnd.Next(chunksize);
                x = dx + chunkX * chunksize;
                z = dz + chunkZ * chunksize;

                int y = heightmap[dz * chunksize + dx];
                if (y <= 0 || y >= worldheight - 15) continue;

                tmpPos.Set(x, y, z);
                block = blockAccessor.GetBlock(tmpPos);
                if (block.Fertility == 0) continue;

                // Place according to forest value
                float treeDensity = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, (float)dx / chunksize, (float)dz / chunksize);
                int climate = GameMath.BiLerpRgbColor((float)dx / chunksize, (float)dz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);
                float shrubChance = GameMath.BiLerp(shrubUpLeft, shrubUpRight, shrubBotLeft, shrubBotRight, (float)dx / chunksize, (float)dz / chunksize);

                
                if (rnd.NextDouble() > (shrubChance / 255f) * (shrubChance / 255f)) continue;
                TreeGenForClimate treegenParams = treeSupplier.GetRandomShrubGenForClimate(climate, (int)shrubChance, y);

                if (treegenParams != null)
                {
                    if (blockAccessor.GetBlock(tmpPos.X, tmpPos.Y, tmpPos.Z).Replaceable >= 6000)
                    {
                        tmpPos.Y--;
                    }

                    treegenParams.treeGen.GrowTree(
                        blockAccessor,
                        tmpPos,
                        treegenParams.size,
                        treegenParams.vinesGrowthChance
                    );
                }
            }

        }

        void genTrees(int chunkX, int chunkZ)
        {
            int triesTrees = (int)treeSupplier.treeGenProps.treesPerChunk.nextFloat();
            int dx, dz, x, z;
            Block block;

            while (triesTrees > 0)
            {
                triesTrees--;

                dx = rnd.Next(chunksize);
                dz = rnd.Next(chunksize);
                x = dx + chunkX * chunksize;
                z = dz + chunkZ * chunksize;

                int y = heightmap[dz * chunksize + dx];
                if (y <= 0 || y >= worldheight - 15) continue;

                tmpPos.Set(x, y, z);
                block = blockAccessor.GetBlock(tmpPos);
                if (block.Fertility == 0) continue;

                // Place according to forest value
                float treeDensity = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, (float)dx / chunksize, (float)dz / chunksize);
                int climate = GameMath.BiLerpRgbColor((float)dx / chunksize, (float)dz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);
                float shrubChance = GameMath.BiLerp(shrubUpLeft, shrubUpRight, shrubBotLeft, shrubBotRight, (float)dx / chunksize, (float)dz / chunksize);

                float treeDensityNormalized = treeDensity / 255f;
                
                
                // 1 in 400 chance to always spawn a tree
                // otherwise go by tree density using a quadratic drop off to create clearer forest edges
                if (rnd.NextDouble() > Math.Max(0.0025, treeDensityNormalized * treeDensityNormalized)) continue;
                TreeGenForClimate treegenParams = treeSupplier.GetRandomTreeGenForClimate(climate, (int)treeDensity, y);

                if (treegenParams != null)
                {
                    if (blockAccessor.GetBlock(tmpPos.X, tmpPos.Y, tmpPos.Z).Replaceable >= 6000)
                    {
                        tmpPos.Y--;
                    }

                    treegenParams.treeGen.GrowTree(
                        blockAccessor,
                        tmpPos,
                        treegenParams.size,
                        treegenParams.vinesGrowthChance,
                        treeDensityNormalized
                    );
                }
            }
        }

        


    }
}
