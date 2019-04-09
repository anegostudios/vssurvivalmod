using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class GenRivulets : ModStdWorldGen
    {
        ICoreServerAPI api;
        Random rnd;
        IWorldGenBlockAccessor blockAccessor;

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

            if (DoDecorationPass)
            {
                api.Event.InitWorldGenerator(initWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Vegetation, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }
        }


        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }

        private void initWorldGen()
        {
            LoadGlobalConfig(api);
            rnd = new Random(api.WorldManager.Seed);
        }

        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ)
        {
            IntMap climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            float fac = (float)climateMap.InnerSize / regionChunkSize;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            int climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac));
            int climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac));
            int climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac + fac));
            int climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac + fac));

            int climateMid = GameMath.BiLerpRgbColor(0.5f, 0.5f, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

            // 16-23 bits = Red = temperature
            // 8-15 bits = Green = rain
            // 0-7 bits = Blue = humidity

            int rain = (climateMid >> 8) & 0xff;
            int humidity = climateMid & 0xff;
            int temp = (climateMid >> 16) & 0xff;

            
            int quantityRivulets = (int)(80 * (rain + humidity) / 255f) * (api.WorldManager.MapSizeY / chunksize) - Math.Max(0, 100 - temp);
            int fx, fy, fz;

            while (quantityRivulets-- > 0)
            {
                int dx = 1 + rnd.Next(chunksize - 2);
                int y = 1 + rnd.Next(api.WorldManager.MapSizeY - 2);
                int dz = 1 + rnd.Next(chunksize - 2);

                int quantitySolid = 0;
                int quantityAir = 0;
                for (int i = 0; i < BlockFacing.ALLFACES.Length; i++)
                {
                    BlockFacing facing = BlockFacing.ALLFACES[i];
                    fx = dx + facing.Normali.X;
                    fy = y + facing.Normali.Y;
                    fz = dz + facing.Normali.Z;

                    Block block = api.World.Blocks[
                        chunks[fy / chunksize].Blocks[(chunksize * (fy % chunksize) + fz) * chunksize + fx]
                    ];

                    bool solid = block.BlockMaterial == EnumBlockMaterial.Stone;
                    quantitySolid += solid ? 1 : 0;
                    quantityAir += (block.BlockMaterial == EnumBlockMaterial.Air) ? 1 : 0;

                    if (!solid && facing == BlockFacing.UP) quantitySolid = 0;
                }

                if (quantitySolid != 5 || quantityAir != 1) continue;

                chunks[y / chunksize].Blocks[(chunksize * (y % chunksize) + dz) * chunksize + dx] = y < 24 ? GlobalConfig.lavaBlockId : GlobalConfig.waterBlockId;

                BlockPos pos = new BlockPos(chunkX * chunksize + dx, y, chunkZ * chunksize + dz);
                blockAccessor.ScheduleBlockUpdate(pos);
            }
        }
    }
}
