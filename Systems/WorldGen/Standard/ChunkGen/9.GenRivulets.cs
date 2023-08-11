using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class GenRivulets : ModStdWorldGen
    {
        ICoreServerAPI api;
        Random rnd;
        IWorldGenBlockAccessor blockAccessor;
        int regionsize;
        int chunkMapSizeY;
        BlockPos chunkBase = new BlockPos();
        BlockPos chunkend = new BlockPos();
        List<Cuboidi> structuresIntersectingChunk = new List<Cuboidi>();

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override double ExecuteOrder()
        {
            return 0.9;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(initWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Vegetation, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }
        }


        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
            regionsize = blockAccessor.RegionSize;
        }

        private void initWorldGen()
        {
            LoadGlobalConfig(api);
            rnd = new Random(api.WorldManager.Seed);
            chunkMapSizeY = api.WorldManager.MapSizeY / chunksize;
        }

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;

            blockAccessor.BeginColumn();
            var mapChunk = chunks[0].MapChunk;
            IntDataMap2D climateMap = mapChunk.MapRegion.ClimateMap;
            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            float fac = (float)climateMap.InnerSize / regionChunkSize;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            int climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac));
            int climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac));
            int climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac + fac));
            int climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac + fac));

            int climateMid = GameMath.BiLerpRgbColor(0.5f, 0.5f, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

            structuresIntersectingChunk.Clear();
            api.World.BlockAccessor.WalkStructures(chunkBase.Set(chunkX * chunksize, 0, chunkZ * chunksize), chunkend.Set(chunkX * chunksize + chunksize, chunkMapSizeY * chunksize, chunkZ * chunksize + chunksize), (struc) =>
            {
                if (struc.SuppressRivulets)
                {
                    structuresIntersectingChunk.Add(struc.Location.Clone().GrowBy(1, 1, 1));
                }
            });

            // 16-23 bits = Red = temperature
            // 8-15 bits = Green = rain
            // 0-7 bits = Blue = humidity
            int rain = (climateMid >> 8) & 0xff;
            int humidity = climateMid & 0xff;
            int temp = (climateMid >> 16) & 0xff;

            int geoActivity = getGeologicActivity(chunkX * chunksize + chunksize / 2, chunkZ * chunksize + chunksize / 2);
            float geoActivityYThreshold = getGeologicActivity(chunkX * chunksize + chunksize / 2, chunkZ * chunksize + chunksize / 2) / 2f * api.World.BlockAccessor.MapSizeY / 256f;


            int quantityWaterRivulets = 2 * ((int)(160 * (rain + humidity) / 255f) * (api.WorldManager.MapSizeY / chunksize) - Math.Max(0, 100 - temp));
            int quantityLavaRivers = (int)(500 * geoActivity/255f * (api.WorldManager.MapSizeY / chunksize));
            
            float sealeveltemp = TerraGenConfig.GetScaledAdjustedTemperatureFloat(temp, 0);
            if (sealeveltemp >= -15)
            {
                while (quantityWaterRivulets-- > 0)
                {
                    tryGenRivulet(chunks, chunkX, chunkZ, geoActivityYThreshold, false);
                }
            }

            while (quantityLavaRivers-- > 0)
            {
                tryGenRivulet(chunks, chunkX, chunkZ, geoActivityYThreshold + 10, true);
            }
        }

        private void tryGenRivulet(IServerChunk[] chunks, int chunkX, int chunkZ, float geoActivityYThreshold, bool lava)
        {
            var mapChunk = chunks[0].MapChunk;
            int fx, fy, fz;
            int surfaceY = (int)(TerraGenConfig.seaLevel * 1.1f);
            int aboveSurfaceHeight = api.WorldManager.MapSizeY - surfaceY;

            int dx = 1 + rnd.Next(chunksize - 2);
            int y = Math.Min(1 + rnd.Next(surfaceY) + rnd.Next(aboveSurfaceHeight) * rnd.Next(aboveSurfaceHeight), api.WorldManager.MapSizeY - 2);
            int dz = 1 + rnd.Next(chunksize - 2);

            ushort hereSurfaceY = mapChunk.WorldGenTerrainHeightMap[dz * chunksize + dx];
            if (y > hereSurfaceY && rnd.Next(2) == 0) return; // Half as common overground

            // Water only above y-threshold, Lava only below y-threshold
            if (y < geoActivityYThreshold && !lava || y > geoActivityYThreshold && lava) return;

            int quantitySolid = 0;
            int quantityAir = 0;
            for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
            {
                BlockFacing facing = BlockFacing.ALLFACES[i];
                fx = dx + facing.Normali.X;
                fy = y + facing.Normali.Y;
                fz = dz + facing.Normali.Z;

                Block block = api.World.Blocks[
                    chunks[fy / chunksize].Data.GetBlockIdUnsafe((chunksize * (fy % chunksize) + fz) * chunksize + fx)
                ];

                bool solid = block.BlockMaterial == EnumBlockMaterial.Stone;
                quantitySolid += solid ? 1 : 0;
                quantityAir += (block.BlockMaterial == EnumBlockMaterial.Air) ? 1 : 0;

                if (!solid)
                {
                    if (facing == BlockFacing.UP) quantitySolid = 0;   // We don't place rivulets on flat ground!
                    else if (facing == BlockFacing.DOWN)   // Nor in 1-block thick ceilings
                    {
                        fy = y + 1;
                        block = api.World.Blocks[chunks[fy / chunksize].Data.GetBlockIdUnsafe((chunksize * (fy % chunksize) + fz) * chunksize + fx)];
                        if (block.BlockMaterial != EnumBlockMaterial.Stone) quantitySolid = 0;
                    }
                }
            }

            if (quantitySolid != 5 || quantityAir != 1) return;
            
            BlockPos pos = new BlockPos(chunkX * chunksize + dx, y, chunkZ * chunksize + dz);
            for (int i = 0; i < structuresIntersectingChunk.Count; i++)
            {
                if (structuresIntersectingChunk[i].Contains(pos)) return;
            }
            if (SkipGenerationAt(pos, EnumWorldGenPass.Vegetation)) return;

            var chunk = chunks[y / chunksize];
            var index = (chunksize * (y % chunksize) + dz) * chunksize + dx;
            Block existing = api.World.GetBlock(chunk.Data.GetBlockId(index, BlockLayersAccess.Solid));
            if (existing.EntityClass != null)
            {
                chunk.RemoveBlockEntity(pos);
            }
            chunk.Data.SetBlockAir(index);
            chunk.Data.SetFluid(index, y < geoActivityYThreshold ? GlobalConfig.lavaBlockId : GlobalConfig.waterBlockId);

            
            

            blockAccessor.ScheduleBlockUpdate(pos);
        }

        private int getGeologicActivity(int posx, int posz)
        {
            var climateMap = blockAccessor.GetMapRegion(posx / regionsize, posz / regionsize)?.ClimateMap;
            if (climateMap == null) return 0;
            int regionChunkSize = regionsize / chunksize;
            float fac = (float)climateMap.InnerSize / regionChunkSize;
            int rlX = (posx / chunksize) % regionChunkSize;
            int rlZ = (posz / chunksize) % regionChunkSize;

            return climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac)) & 0xff;
        }

    }
}
