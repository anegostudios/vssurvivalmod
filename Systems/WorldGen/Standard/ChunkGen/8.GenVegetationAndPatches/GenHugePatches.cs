/*using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenHugePatches : GenPartial
    {
        ICoreServerAPI api;
        LCGRandom rnd;
        IBlockAccessor blockAccessor;
        WgenTreeSupplier treeSupplier;
        int worldheight;
        int chunkMapSizeY;
        int regionChunkSize;
        Dictionary<string, int> RockBlockIdsByType;
        BlockPatchConfig bpc;

        public override double ExecuteOrder()
        {
            return 0.49;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            treeSupplier = new WgenTreeSupplier(api);

            if (DoDecorationPass)
            {
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }

            base.StartServerSide(api);
        }


        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }


        public override void initWorldGen()
        {
            base.initWorldGen();

            LoadGlobalConfig(api);

            rnd = new LCGRandom(api.WorldManager.Seed - 87698);
            chunksize = api.WorldManager.ChunkSize;

            treeSupplier.LoadTrees();

            worldheight = api.WorldManager.MapSizeY;
            chunkMapSizeY = api.WorldManager.MapSizeY / chunksize;
            regionChunkSize = api.WorldManager.RegionSize / chunksize;

            RockBlockIdsByType = new Dictionary<string, int>();
            RockStrataConfig rockstrata = api.Assets.Get("worldgen/rockstrata.json").ToObject<RockStrataConfig>();
            for (int i = 0; i < rockstrata.Variants.Length; i++)
            {
                Block block = api.World.GetBlock(rockstrata.Variants[i].BlockCode);
                RockBlockIdsByType[block.LastCodePart()] = block.BlockId;
            }
            IAsset asset = api.Assets.Get("worldgen/hugeblockpatches.json");
            bpc = asset.ToObject<BlockPatchConfig>();
            bpc.ResolveBlockIds(api, rockstrata);
        }

        internal override int chunkRange => 5;

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

        BlockPos chunkBase = new BlockPos();
        BlockPos chunkend = new BlockPos();

        public override void GeneratePartial(IServerChunk[] chunks, int chunkX, int chunkZ, int basePosX, int basePosZ)
        {
            if (!TerraGenConfig.GenerateVegetation) return;

            rnd.InitPositionSeed(chunkX, chunkZ);

            IMapChunk mapChunk = chunks[0].MapChunk;

            IntMap forestMap = mapChunk.MapRegion.ForestMap;
            IntMap shrubMap = mapChunk.MapRegion.ShrubMap;
            IntMap climateMap = mapChunk.MapRegion.ClimateMap;
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



            int dx, dz, x, z;
            Block block;

            for (int i = 0; i < bpc.Patches.Length; i++)
            {
                BlockPatch blockPatch = bpc.Patches[i];

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

                    float forestRel = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, (float)dx / chunksize, (float)dz / chunksize) / 255f;
                    int climate = GameMath.BiLerpRgbColor((float)dx / chunksize, (float)dz / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

                    if (bpc.IsPatchSuitableAt(blockPatch, block, api.WorldManager, climate, y, forestRel))
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
                            blockPatch.GenerateHuge(chunks, blockAccessor, rnd, x, y, z, firstBlockId);
                        }
                    }
                }
            }
        }
    }
}
*/