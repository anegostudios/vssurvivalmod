using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    enum EnumTunnelType
    {
        Cave,
        VerticalDownThenCaves
    }

    public class GenCaves : GenPartial
    {
        

        internal override int chunkRange { get { return 5; } }

        public override double ExecuteOrder() { return 0.3; }

        internal FastRandom caveRand;
        IWorldGenBlockAccessor worldgenBlockAccessor;

        Random rand = new Random();

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.Terrain);
            api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

            api.RegisterCommand("gencaves", "Cave generator test tool. Deletes all chunks in the area and generates inverse caves around the world middle", "", CmdCaveGenTest, Privilege.controlserver);
        }

        private void CmdCaveGenTest(IServerPlayer player, int groupId, CmdArgs args)
        {
            chunkRand = new FastRandom(api.WorldManager.Seed);

            airBlockId = api.World.GetBlock(new AssetLocation("rock-granite")).BlockId;

            int baseChunkX = api.World.BlockAccessor.MapSizeX / 2 / chunksize;
            int baseChunkZ = api.World.BlockAccessor.MapSizeZ / 2 / chunksize;

            for (int dx = -5; dx <= 5; dx++)
            {
                for (int dz = -5; dz <= 5; dz++)
                {
                    int chunkX = baseChunkX + dx;
                    int chunkZ = baseChunkZ + dz;
                    
                    IServerChunk[] chunks = GetChunkColumn(chunkX, chunkZ);
                    ClearChunkColumn(chunks);


                    for (int gdx = -chunkRange; gdx <= chunkRange; gdx++)
                    {
                        for (int gdz = -chunkRange; gdz <= chunkRange; gdz++)
                        {
                            chunkRand.InitPositionSeed(chunkX + gdx, chunkZ + gdz);
                            GeneratePartial(chunks, chunkX, chunkZ, gdx, gdz);
                        }
                    }

                    MarkDirty(chunkX, chunkZ, chunks);
                }
            }

            airBlockId = 0;

            player.SendMessage(groupId, "Generated and chunks force resend flags set", EnumChatType.Notification);
        }

        private IServerChunk[] GetChunkColumn(int chunkX, int chunkZ)
        {
            int size = api.World.BlockAccessor.MapSizeY / chunksize;
            IServerChunk[] chunks = new IServerChunk[size];
            for (int chunkY = 0; chunkY < size; chunkY++)
            {
                chunks[chunkY] = api.WorldManager.GetChunk(chunkX, chunkY, chunkZ);
            }

            return chunks;
        }

        private void MarkDirty(int chunkX, int chunkZ, IServerChunk[] chunks)
        {
            for (int chunkY = 0; chunkY < chunks.Length; chunkY++) {
                chunks[chunkY].MarkModified();
                api.WorldManager.ForceSendChunk(chunkX, chunkY, chunkZ, true);
            }
        }

        private void ClearChunkColumn(IServerChunk[] chunks)
        {
            
            for (int i = 0; i < chunks.Length; i++)
            {
                IServerChunk chunk = chunks[i];
                chunk.Unpack();

                for (int j = 0; j < chunk.Blocks.Length; j++)
                {
                    chunk.Blocks[j] = 0;
                }

                chunk.MarkModified();
            }
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }

        internal override void OnGameWorldLoaded()
        {
            base.OnGameWorldLoaded();
            caveRand = new FastRandom(api.WorldManager.Seed + 123128);
        }

        internal override void GeneratePartial(IServerChunk[] chunks, int chunkX, int chunkZ, int cdx, int cdz)
        {
            int quantityCaves = chunkRand.NextInt(81) / 60;

            while (quantityCaves-- > 0)
            {
                int posX = cdx * chunksize + chunkRand.NextInt(chunksize);
                int posY = chunkRand.NextInt(worldheight - 20) + 8;
                int posZ = cdz * chunksize + chunkRand.NextInt(chunksize);


                float horAngle = chunkRand.NextFloat() * GameMath.TWOPI;
                float vertAngle = (chunkRand.NextFloat() - 0.5f) * 0.25f;
                float horizontalSize = chunkRand.NextFloat() * 2 + chunkRand.NextFloat();

                int maxIterations = chunkRange * chunksize - chunksize / 2;
                maxIterations = maxIterations - chunkRand.NextInt(maxIterations / 4);

                caveRand.SetWorldSeed(chunkRand.NextInt(10000000));
                caveRand.InitPositionSeed(chunkX + cdx, chunkZ + cdz);
                CarveTunnel(chunks, chunkX, chunkZ, posX, posY, posZ, horAngle, vertAngle, horizontalSize, 1f, 0, maxIterations, 0);
            }
        }

        ushort blockId;

        private void CarveTunnel(IServerChunk[] chunks, int chunkX, int chunkZ, double posX, double posY, double posZ, float horAngle, float vertAngle, float horizontalSize, float verticalSize, int currentIteration, int maxIterations, int branchLevel)
        {
            blockId = airBlockId;

            ushort[] heightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;


            float horAngleChange = 0;
            float vertAngleChange = 0;
            float horRadiusChange = 0;
            float horRadiusChangeAccum = 0;

            float relPos;

            while (currentIteration++ < maxIterations)
            {
                relPos = (float)currentIteration / maxIterations;

                float horRadius = 1.5f + GameMath.FastSin(relPos * GameMath.PI) * horizontalSize + horRadiusChangeAccum;
                float vertRadius = (horRadius - horRadiusChangeAccum / 2) * verticalSize;


                float advanceHor = GameMath.FastCos(vertAngle);
                float advanceVer = GameMath.FastSin(vertAngle);



                posX += GameMath.FastCos(horAngle) * advanceHor;
                posY += advanceVer;
                posZ += GameMath.FastSin(horAngle) * advanceHor;

                vertAngle *= 0.8f;

                if (caveRand.NextInt(50) == 0)
                {
                    horRadiusChange = caveRand.NextFloat() * caveRand.NextFloat() * 7;
                }

                horRadiusChangeAccum = Math.Max(0, horRadiusChangeAccum + horRadiusChange * 0.15f);
                horRadiusChange -= 0.45f;

                horAngle += 0.1f * horAngleChange;
                vertAngle += 0.1f * vertAngleChange;


                vertAngleChange = 0.9f * vertAngleChange + (caveRand.NextFloat() - caveRand.NextFloat()) * caveRand.NextFloat() * 3;
                horAngleChange = 0.9f * horAngleChange + (caveRand.NextFloat() - caveRand.NextFloat()) * caveRand.NextFloat() * 1;

                // Horizontal branch
                if (caveRand.NextInt(25 * (branchLevel + 1)) == 0 && branchLevel < 3)
                {
                    CarveTunnel(
                        chunks,
                        chunkX,
                        chunkZ,
                        posX,
                        posY,
                        posZ,
                        horAngle + (caveRand.NextFloat() + caveRand.NextFloat() - 1) + GameMath.PI,
                        vertAngle + (caveRand.NextFloat() - 0.5f) * (caveRand.NextFloat() - 0.5f),
                        horizontalSize,
                        verticalSize,
                        currentIteration,
                        maxIterations - (int)(caveRand.NextFloat() * 0.5 * maxIterations),
                        branchLevel + 1
                    );
                }

                // Vertical branch
                if (horRadius > 3 && posY > 60 && caveRand.NextInt(40) == 0 && branchLevel < 1)
                {
                    CarveShaft(
                        chunks,
                        chunkX,
                        chunkZ,
                        posX,
                        posY,
                        posZ,
                        horAngle + (caveRand.NextFloat() + caveRand.NextFloat() - 1) + GameMath.PI,
                        -GameMath.PI / 2 - 0.1f + 0.2f * caveRand.NextFloat(),
                        Math.Min(3.5f, horRadius - 1),
                        verticalSize,
                        currentIteration,
                        maxIterations - (int)(caveRand.NextFloat() * 0.5 * maxIterations) + (int)((posY/5) * (0.5f + 0.5f * caveRand.NextFloat())),
                        branchLevel
                    );

                    branchLevel++;
                }


                // Lake
               /* bool end = currentIteration == maxIterations;

                if (end && caveRand.NextInt(4) == 0)
                {
                    CarveLake(chunks, horRadius, vertRadius, posX, posY, posZ, heightmap, chunkX, chunkZ);
                    continue;
                }*/
                
                if (caveRand.NextInt(5) == 0 && horRadius >= 2) continue;

                // Check just to prevent unnecessary calculations
                // As long as we are outside the currently generating chunk, we don't need to generate anything
                if (posX <= -horRadius * 2 || posX >= chunksize + horRadius * 2 || posZ <= -horRadius * 2 || posZ >= chunksize + horRadius * 2) continue;

                SetBlocks(chunks, horRadius, vertRadius, posX, posY, posZ, heightmap, chunkX, chunkZ);
            }
        }

        /*private void CarveLake(IServerChunk[] chunks, float caveHorRadius, float caveVertRadius, double posX, double posY, double posZ, ushort[] heightmap, int chunkX, int chunkZ)
        {
            float horRadius = caveHorRadius + 1 + chunkRand.NextFloat() * 3;
            float vertRadius = caveVertRadius + 1 + chunkRand.NextFloat() * 2;

            // Check just to prevent unnecessary calculations
            // As long as we are outside the currently generating chunk, we don't need to generate anything
            if (posX <= -horRadius * 2 || posX >= chunksize + horRadius * 2 || posZ <= -horRadius * 2 || posZ >= chunksize + horRadius * 2) return;

            if (SetBlocks(chunks, horRadius, vertRadius, posX, posY, posZ, heightmap, chunkX, chunkZ)) {
                SetLakeBlocks(chunks, horRadius, vertRadius, posX, posY, posZ, heightmap, chunkX, chunkZ);
            }
        }*/

        private void CarveShaft(IServerChunk[] chunks, int chunkX, int chunkZ, double posX, double posY, double posZ, float horAngle, float vertAngle, float horizontalSize, float verticalSize, int caveCurrentIteration, int maxIterations, int branchLevel)
        {
            blockId = airBlockId;// api.World.GetBlock(new AssetLocation("mantle")).BlockId;
            float vertAngleChange = 0;

            ushort[] heightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

            float relPos;
            int currentIteration = 0;

            while (currentIteration++ < maxIterations)
            {
                relPos = (float)currentIteration / maxIterations;

                float horRadius = horizontalSize * (1 - relPos * 0.33f);
                float vertRadius = (horRadius) * verticalSize;

                float advanceHor = GameMath.FastCos(vertAngle);
                float advanceVer = GameMath.FastSin(vertAngle);


                posX += GameMath.FastCos(horAngle) * advanceHor;
                posY += advanceVer;
                posZ += GameMath.FastSin(horAngle) * advanceHor;

                vertAngle += 0.1f * vertAngleChange;
                vertAngleChange = 0.9f * vertAngleChange + (caveRand.NextFloat() - caveRand.NextFloat()) * caveRand.NextFloat() / 3;



                // Horizontal branch
                if (maxIterations - currentIteration < 10)
                {
                    int num = 3 + caveRand.NextInt(4);
                    for (int i = 0; i < num; i++)
                    {
                        CarveTunnel(
                            chunks,
                            chunkX,
                            chunkZ,
                            posX,
                            posY,
                            posZ,
                            chunkRand.NextFloat() * GameMath.TWOPI,
                            (chunkRand.NextFloat() - 0.5f) * 0.25f,
                            horizontalSize + 1,
                            verticalSize,
                            caveCurrentIteration,
                            maxIterations,
                            1
                        );
                    }
                    return;
                }

                if (caveRand.NextInt(5) == 0 && horRadius >= 2) continue;

                // Check just to prevent unnecessary calculations
                // As long as we are outside the currently generating chunk, we don't need to generate anything
                if (posX <= -horRadius * 2 || posX >= chunksize + horRadius * 2 || posZ <= -horRadius * 2 || posZ >= chunksize + horRadius * 2) continue;

                SetBlocks(chunks, horRadius, vertRadius, posX, posY, posZ, heightmap, chunkX, chunkZ);
            }
        }




        private bool SetBlocks(IServerChunk[] chunks, float horRadius, float vertRadius, double posX, double posY, double posZ, ushort[] heightmap, int chunkX, int chunkZ)
        {
            // One extra size for checking if we run into water
            horRadius++;
            vertRadius++;

            int mindx = (int)GameMath.Clamp(posX - horRadius, 0, chunksize - 1);
            int maxdx = (int)GameMath.Clamp(posX + horRadius + 1, 0, chunksize - 1);
            int mindy = (int)GameMath.Clamp(posY - vertRadius * 0.7f, 1, worldheight - 1);
            int maxdy = (int)GameMath.Clamp(posY + vertRadius + 1, 1, worldheight - 1);
            int mindz = (int)GameMath.Clamp(posZ - horRadius, 0, chunksize - 1);
            int maxdz = (int)GameMath.Clamp(posZ + horRadius + 1, 0, chunksize - 1);

            double xdist, ydist, zdist;
            double hRadiusSq = horRadius * horRadius;
            double vRadiusSq = vertRadius * vertRadius;

            bool foundWater = false;
            for (int dy = mindy; dy <= maxdy && !foundWater; dy++)
            {
                ushort[] chunkBlockData = chunks[dy / chunksize].Blocks;
                int ly = dy % chunksize;

                ydist = (dy - posY) * (dy - posY) / vRadiusSq;

                for (int dx = mindx; dx <= maxdx && !foundWater; dx++)
                {
                    xdist = (dx - posX) * (dx - posX) / hRadiusSq;

                    for (int dz = mindz; dz <= maxdz && !foundWater; dz++)
                    {
                        zdist = (dz - posZ) * (dz - posZ) / hRadiusSq;

                        if (xdist + ydist + zdist > 1.0) continue;

                        foundWater = chunkBlockData[(ly * chunksize + dz) * chunksize + dx] == GlobalConfig.waterBlockId;
                    }
                }
            }

            if (foundWater) return false;

            horRadius--;
            vertRadius--;

            mindx = (int)GameMath.Clamp(posX - horRadius, 0, chunksize - 1);
            maxdx = (int)GameMath.Clamp(posX + horRadius + 1, 0, chunksize - 1);
            mindy = (int)GameMath.Clamp(posY - vertRadius * 0.7f, 1, worldheight - 1);
            maxdy = (int)GameMath.Clamp(posY + vertRadius + 1, 1, worldheight - 1);
            mindz = (int)GameMath.Clamp(posZ - horRadius, 0, chunksize - 1);
            maxdz = (int)GameMath.Clamp(posZ + horRadius + 1, 0, chunksize - 1);

            hRadiusSq = horRadius * horRadius;
            vRadiusSq = vertRadius * vertRadius;

            for (int dy = mindy; dy <= maxdy; dy++)
            {
                ushort[] chunkBlockData = chunks[dy / chunksize].Blocks;
                int ly = dy % chunksize;

                ydist = (dy - posY) * (dy - posY) / vRadiusSq;

                for (int dx = mindx; dx <= maxdx; dx++)
                {
                    xdist = (dx - posX) * (dx - posX) / hRadiusSq;

                    for (int dz = mindz; dz <= maxdz; dz++)
                    {
                        zdist = (dz - posZ) * (dz - posZ) / hRadiusSq;

                        if (xdist + ydist + zdist > 1.0) continue;

                        chunkBlockData[(ly * chunksize + dz) * chunksize + dx] = dy < 12 ? GlobalConfig.lavaBlockId : blockId;
                        if (heightmap[dz * chunksize + dx] == dy) heightmap[dz * chunksize + dx]--;

                        if (dy == 11)
                        {
                            int chunkSize = worldgenBlockAccessor.ChunkSize;
                            worldgenBlockAccessor.ScheduleBlockLightUpdate(new BlockPos(chunkX * chunkSize + dx, ly, chunkZ * chunkSize + dz), airBlockId, GlobalConfig.lavaBlockId);
                        }
                    }
                }
            }

            return true;
        }







        private void SetLakeBlocks(IServerChunk[] chunks, float horRadius, float vertRadius, double posX, double posY, double posZ, ushort[] heightmap, int chunkX, int chunkZ)
        {
            int mindx = (int)GameMath.Clamp(posX - horRadius, 0, chunksize - 1);
            int maxdx = (int)GameMath.Clamp(posX + horRadius + 1, 0, chunksize - 1);
            int mindy = (int)GameMath.Clamp(posY - vertRadius, 1, worldheight - 1);
            int maxdy = (int)GameMath.Clamp(posY - vertRadius * 0.7f, 1, worldheight - 1);
            int mindz = (int)GameMath.Clamp(posZ - horRadius, 0, chunksize - 1);
            int maxdz = (int)GameMath.Clamp(posZ + horRadius + 1, 0, chunksize - 1);

            float hRadiusSq = horRadius * horRadius;
            float vRadiusSq = vertRadius * vertRadius;
            double xdist, ydist, zdist;

            for (int dy = mindy; dy <= maxdy; dy++)
            {
                ushort[] chunkBlockData = chunks[dy / chunksize].Blocks;
                int ly = dy % chunksize;

                ydist = (dy - posY) * (dy - posY) / vRadiusSq;

                for (int dx = mindx; dx <= maxdx; dx++)
                {
                    xdist = (dx - posX) * (dx - posX) / hRadiusSq;

                    for (int dz = mindz; dz <= maxdz; dz++)
                    {
                        zdist = (dz - posZ) * (dz - posZ) / hRadiusSq;

                        if (xdist + ydist + zdist > 1.0) continue;

                        chunkBlockData[(ly * chunksize + dz) * chunksize + dx] = GlobalConfig.waterBlockId;
                    }
                }
            }
        }


    }
}