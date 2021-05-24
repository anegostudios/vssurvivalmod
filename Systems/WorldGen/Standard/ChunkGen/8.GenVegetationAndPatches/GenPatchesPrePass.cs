using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenPatchesPrePass : ModStdWorldGen
    {
        ICoreServerAPI api;
        LCGRandom rnd;
        IBlockAccessor blockAccessor;
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
            return 0;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            //api.Event.InitWorldGenerator(initWorldGen, "standard");
            //api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
            //api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
        }


        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }

        Dictionary<string, int> RockBlockIdsByType;

        

        public void initWorldGen()
        {
            LoadGlobalConfig(api);

            rnd = new LCGRandom(api.WorldManager.Seed);
            chunksize = api.WorldManager.ChunkSize;

            worldheight = api.WorldManager.MapSizeY;
            chunkMapSizeY = api.WorldManager.MapSizeY / chunksize;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;

            RockBlockIdsByType = new Dictionary<string, int>();
            RockStrataConfig rockstrata = api.Assets.Get("worldgen/rockstrata.json").ToObject<RockStrataConfig>();
            for (int i = 0; i < rockstrata.Variants.Length; i++)
            {
                Block block = api.World.GetBlock(rockstrata.Variants[i].BlockCode);
                RockBlockIdsByType.Add(block.LastCodePart(), block.BlockId);
            }
            IAsset asset = api.Assets.Get("worldgen/blockpatches.json");
            bpc = asset.ToObject<BlockPatchConfig>();
            bpc.ResolveBlockIds(api, rockstrata);
        }


        ushort[] heightmap;
        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;

        BlockPos tmpPos = new BlockPos();

        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            rnd.InitPositionSeed(chunkX, chunkZ);
            IMapChunk mapChunk = chunks[0].MapChunk;

            IntDataMap2D climateMap = mapChunk.MapRegion.ClimateMap;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            float facC = (float)climateMap.InnerSize / regionChunkSize;
            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));

            heightmap = chunks[0].MapChunk.RainHeightMap;

            genPatches(chunkX, chunkZ);
        }


        void genPatches(int chunkX, int chunkZ)
        {
            int dx, dz, x, z;
            Block block;

            for (int i = 0; i < bpc.Patches.Length; i++)
            {
                BlockPatch blockPatch = bpc.Patches[i];
                if (!blockPatch.PrePass) continue;

                float chance = blockPatch.Chance * bpc.ChanceMultiplier.nextFloat();

                while (chance-- > rnd.NextDouble())
                {
                    dx = rnd.NextInt(chunksize);
                    dz = rnd.NextInt(chunksize);
                    x = dx + chunkX * chunksize;
                    z = dz + chunkZ * chunksize;

                    int y = heightmap[dz * chunksize + dx];
                    if (y <= 0 || y >= worldheight - 15) continue;

                    tmpPos.Set(x, y, z);
                    block = blockAccessor.GetBlock(tmpPos);

                    // Place according to forest value
                    int climate = GameMath.BiLerpRgbColor((float)dx / chunksize, (float)dz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

                    if (bpc.IsPatchSuitableAt(blockPatch, block, api.WorldManager, climate, y, 0, 0))
                    {
                        int firstBlockId = 0;
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


        

    }
}
