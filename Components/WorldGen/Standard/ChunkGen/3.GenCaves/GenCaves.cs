using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class GenCaves : GenPartial
    {
        

        internal override int chunkRange { get { return 5; } }

        public override double ExecuteOrder() { return 0.3; }

        internal LCGRandom caveRand;
        IWorldGenBlockAccessor worldgenBlockAccessor;

        Random rand = new Random();

        NormalizedSimplexNoise basaltNoise;
        NormalizedSimplexNoise heightvarNoise;


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            if (DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.Terrain, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

                api.RegisterCommand("gencaves", "Cave generator test tool. Deletes all chunks in the area and generates inverse caves around the world middle", "", CmdCaveGenTest, Privilege.controlserver);

                api.Event.MapChunkGeneration(OnMapChunkGen, "standard");
                api.Event.MapChunkGeneration(OnMapChunkGen, "superflat");
                api.Event.InitWorldGenerator(initWorldGen, "superflat");
            }
        }


        private void OnMapChunkGen(IMapChunk mapChunk, int chunkX, int chunkZ)
        {
            mapChunk.CaveHeightDistort = new byte[chunksize * chunksize];

            for (int dx = 0; dx < chunksize; dx++)
            {
                for (int dz = 0; dz < chunksize; dz++)
                {
                     mapChunk.CaveHeightDistort[dz * chunksize + dx] = (byte)(255 * heightvarNoise.Noise(chunksize * chunkX + dx, chunksize * chunkZ + dz));
                }
            }
        }


        private void CmdCaveGenTest(IServerPlayer player, int groupId, CmdArgs args)
        {
            caveRand = new LCGRandom(api.WorldManager.Seed + 123128);
            initWorldGen();


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
                    for (int i = 0; i < chunks.Length; i++)
                    {
                        if (chunks[i] == null)
                        {
                            player.SendMessage(groupId, "Cannot generate 10x10 area of caves, chunks are not loaded that far yet.", EnumChatType.CommandError);
                            return;
                        }
                    }
                }
            }


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
                api.WorldManager.ResendChunk(chunkX, chunkY, chunkZ, true);
            }
        }

        private bool ClearChunkColumn(IServerChunk[] chunks)
        {
            
            for (int i = 0; i < chunks.Length; i++)
            {
                IServerChunk chunk = chunks[i];
                if (chunk == null) return false;

                chunk.Unpack();

                for (int j = 0; j < chunk.Blocks.Length; j++)
                {
                    chunk.Blocks[j] = 0;
                }

                chunk.MarkModified();
            }

            return true;
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }

        public override void initWorldGen()
        {
            base.initWorldGen();
            caveRand = new LCGRandom(api.WorldManager.Seed + 123128);
            basaltNoise = NormalizedSimplexNoise.FromDefaultOctaves(2, 1f / 3.5f, 0.9f, api.World.Seed + 12);
            heightvarNoise = NormalizedSimplexNoise.FromDefaultOctaves(5, 1f / 40f, 0.9f, api.World.Seed + 12);
        }

        public override void GeneratePartial(IServerChunk[] chunks, int chunkX, int chunkZ, int cdx, int cdz)
        {
            int quantityCaves = chunkRand.NextInt(100) > 35 ? 1 : 0;

            while (quantityCaves-- > 0)
            {
                int posX = cdx * chunksize + chunkRand.NextInt(chunksize);
                int posY = chunkRand.NextInt(worldheight - 20) + 8;
                int posZ = cdz * chunksize + chunkRand.NextInt(chunksize);

                float horAngle = chunkRand.NextFloat() * GameMath.TWOPI;
                float vertAngle = (chunkRand.NextFloat() - 0.5f) * 0.25f;
                float horizontalSize = chunkRand.NextFloat() * 2 + chunkRand.NextFloat();
                float verticalSize = 0.75f + chunkRand.NextFloat() * 0.4f;

                if (chunkRand.NextFloat() < 0.04f)
                {
                    horizontalSize = chunkRand.NextFloat() * 2 + chunkRand.NextFloat() + chunkRand.NextFloat();
                    verticalSize = 0.25f + chunkRand.NextFloat() * 0.2f;
                }
                else
                if (chunkRand.NextFloat() < 0.01f)
                {
                    horizontalSize = 0.75f + chunkRand.NextFloat();
                    verticalSize = chunkRand.NextFloat() * 2 + chunkRand.NextFloat();
                }

                bool extraBranchy = chunkRand.NextFloat() < 0.02f;
                bool largeNearLavaLayer = chunkRand.NextFloat() < 0.3f;

                float curviness = chunkRand.NextFloat() < 0.01f ? 0.035f : (chunkRand.NextFloat() < 0.03f ? 0.5f : 0.1f);

                int maxIterations = chunkRange * chunksize - chunksize / 2;
                maxIterations = maxIterations - chunkRand.NextInt(maxIterations / 4);

                caveRand.SetWorldSeed(chunkRand.NextInt(10000000));
                caveRand.InitPositionSeed(chunkX + cdx, chunkZ + cdz);
                CarveTunnel(chunks, chunkX, chunkZ, posX, posY, posZ, horAngle, vertAngle, horizontalSize, verticalSize, 0, maxIterations, 0, extraBranchy, curviness, largeNearLavaLayer);
            }
        }

        ushort blockId;

        private void CarveTunnel(IServerChunk[] chunks, int chunkX, int chunkZ, double posX, double posY, double posZ, float horAngle, float vertAngle, float horizontalSize, float verticalSize, int currentIteration, int maxIterations, int branchLevel, bool extraBranchy = false, float curviness = 0.1f, bool largeNearLavaLayer = false)
        {
            blockId = airBlockId;

            ushort[] terrainheightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;
            ushort[] rainheightmap = chunks[0].MapChunk.RainHeightMap;

            float horAngleChange = 0;
            float vertAngleChange = 0;
            float horRadiusGain = 0;
            float horRadiusLoss = 0;
            float horRadiusGainAccum = 0;
            float horRadiusLossAccum = 0;

            float verHeightGain = 0;
            float verHeightLoss = 0;
            float verHeightGainAccum = 0;
            float verHeightLossAccum = 0;

            float sizeChangeSpeedAccum = 0.15f;
            float sizeChangeSpeedGain = 0f;

            float relPos;

            int branchRand = (branchLevel + 1) * (extraBranchy ? 12 : 25);

            while (currentIteration++ < maxIterations)
            {
                relPos = (float)currentIteration / maxIterations;

                float horRadius = 1.5f + GameMath.FastSin(relPos * GameMath.PI) * horizontalSize + horRadiusGainAccum;
                horRadius = Math.Min(horRadius, Math.Max(1, horRadius - horRadiusLossAccum));

                float vertRadius = 1.5f + GameMath.FastSin(relPos * GameMath.PI) * (verticalSize + horRadiusLossAccum / 4) + verHeightGainAccum; // - horRadiusGainAccum / 2
                vertRadius = Math.Min(vertRadius, Math.Max(0.6f, vertRadius - verHeightLossAccum));

                float advanceHor = GameMath.FastCos(vertAngle);
                float advanceVer = GameMath.FastSin(vertAngle);

                // Caves get bigger near y=12
                if (largeNearLavaLayer)
                {
                    horRadius *= 1 + Math.Max(0, (10 - (float)Math.Abs(posY - 12)) / 10f);
                    vertRadius *= 1 + Math.Max(0, (10 - (float)Math.Abs(posY - 12)) / 10f);
                }

                if (vertRadius < 1) vertAngle *= 0.1f;

                posX += GameMath.FastCos(horAngle) * advanceHor;
                posY += GameMath.Clamp(advanceVer, -vertRadius, vertRadius);
                posZ += GameMath.FastSin(horAngle) * advanceHor;

                vertAngle *= 0.8f;



                if (caveRand.NextInt(80) == 0)
                {
                    sizeChangeSpeedGain = (caveRand.NextFloat() * caveRand.NextFloat())/2;
                }

                int rnd = caveRand.NextInt(10000); // Calls to caveRand are not too cheap, so lets just use one for all those random variation changes
                // 1/330 * 10k = 30
                // 1/130 * 10k = 76
                // 1/100 * 10k = 100
                // 1/120 * 10k = 83
                // 1/800 * 10k = 12

                // Rarely change direction
                if ((rnd -= 30) <= 0)
                {
                    horAngle = caveRand.NextFloat() * GameMath.TWOPI;
                } else
                // Rarely change direction somewhat
                if ((rnd -= 76) <= 0)
                {
                    horAngle += caveRand.NextFloat() * GameMath.PI - GameMath.PIHALF;
                } else
                // Rarely go pretty wide
                if ((rnd -= 60) <= 0)
                {
                    horRadiusGain = caveRand.NextFloat() * caveRand.NextFloat() * 7;
                } else
                // Rarely go thin
                if ((rnd -= 60) <= 0)
                {
                    horRadiusLoss = caveRand.NextFloat() * caveRand.NextFloat() * 10;
                } else
                // Rarely go flat
                if ((rnd -= 50) <= 0)
                {
                    if (posY < TerraGenConfig.seaLevel - 10)
                    {
                        verHeightLoss = caveRand.NextFloat() * caveRand.NextFloat() * 12;
                        horRadiusGain = Math.Max(horRadiusGain, caveRand.NextFloat() * caveRand.NextFloat() * 3);
                    }
                } else
                // Very rarely go really wide
                if ((rnd -= 9) <= 0)
                {
                    if (posY < TerraGenConfig.seaLevel - 20)
                    {
                        horRadiusGain = 2 + caveRand.NextFloat() * caveRand.NextFloat() * 11f;
                    }
                } else
                // Very rarely go really tall
                if ((rnd -= 9) <= 0)
                {
                    verHeightGain = 2 + caveRand.NextFloat() * caveRand.NextFloat() * 7;
                } else
                // Rarely large lava caverns
                if ((rnd -= 100) <= 0)
                {
                    if (posY < 19)
                    {
                        verHeightGain = 2 + caveRand.NextFloat() * caveRand.NextFloat() * 5;
                        horRadiusGain = 4 + caveRand.NextFloat() * caveRand.NextFloat() * 9;
                    }
                }

                sizeChangeSpeedAccum = Math.Max(0.1f, sizeChangeSpeedAccum + sizeChangeSpeedGain * 0.05f);
                sizeChangeSpeedGain -= 0.02f;

                horRadiusGainAccum = Math.Max(0, horRadiusGainAccum + horRadiusGain * sizeChangeSpeedAccum);
                horRadiusGain -= 0.45f;

                horRadiusLossAccum = Math.Max(0, horRadiusLossAccum + horRadiusLoss * sizeChangeSpeedAccum);
                horRadiusLoss -= 0.4f;

                verHeightGainAccum = Math.Max(0, verHeightGainAccum + verHeightGain * sizeChangeSpeedAccum);
                verHeightGain -= 0.45f;

                verHeightLossAccum = Math.Max(0, verHeightLossAccum + verHeightLoss * sizeChangeSpeedAccum);
                verHeightLoss -= 0.4f;

                horAngle += curviness * horAngleChange;
                vertAngle += curviness * vertAngleChange;


                
                vertAngleChange = 0.9f * vertAngleChange + (caveRand.NextFloat() - caveRand.NextFloat()) * caveRand.NextFloat() * 3;
                horAngleChange = 0.9f * horAngleChange + (caveRand.NextFloat() - caveRand.NextFloat()) * caveRand.NextFloat() * 1;
                

                if (caveRand.NextInt(140) == 0)
                {
                    horAngleChange *= caveRand.NextFloat() * 6;
                }

                // Horizontal branch
                if ((vertRadius > 1 || horRadius > 1) && branchLevel < 3 && caveRand.NextInt(branchRand) == 0)
                {
                    CarveTunnel(
                        chunks,
                        chunkX,
                        chunkZ,
                        posX, posY + verHeightGainAccum / 2, posZ,
                        horAngle + (caveRand.NextFloat() + caveRand.NextFloat() - 1) + GameMath.PI,
                        vertAngle + (caveRand.NextFloat() - 0.5f) * (caveRand.NextFloat() - 0.5f),
                        horizontalSize,
                        verticalSize + verHeightGainAccum,
                        currentIteration,
                        maxIterations - (int)(caveRand.NextFloat() * 0.5 * maxIterations),
                        branchLevel + 1
                    );
                }

                // Vertical branch
                if (horRadius > 3 && posY > 60 && branchLevel < 1 && caveRand.NextInt(60) == 0)
                {
                    CarveShaft(
                        chunks,
                        chunkX,
                        chunkZ,
                        posX, posY + verHeightGainAccum / 2, posZ,
                        horAngle + (caveRand.NextFloat() + caveRand.NextFloat() - 1) + GameMath.PI,
                        -GameMath.PI / 2 - 0.1f + 0.2f * caveRand.NextFloat(),
                        Math.Min(3.5f, horRadius - 1),
                        verticalSize + verHeightGainAccum,
                        currentIteration,
                        maxIterations - (int)(caveRand.NextFloat() * 0.5 * maxIterations) + (int)((posY/5) * (0.5f + 0.5f * caveRand.NextFloat())),
                        branchLevel
                    );

                    branchLevel++;
                }


                
                if (horRadius >= 2 && caveRand.NextInt(5) == 0) continue;

                // Check just to prevent unnecessary calculations
                // As long as we are outside the currently generating chunk, we don't need to generate anything
                if (posX <= -horRadius * 2 || posX >= chunksize + horRadius * 2 || posZ <= -horRadius * 2 || posZ >= chunksize + horRadius * 2) continue;

                SetBlocks(chunks, horRadius, vertRadius + verHeightGainAccum, posX, posY + verHeightGainAccum / 2, posZ, terrainheightmap, rainheightmap, chunkX, chunkZ);
            }
        }

        

        private void CarveShaft(IServerChunk[] chunks, int chunkX, int chunkZ, double posX, double posY, double posZ, float horAngle, float vertAngle, float horizontalSize, float verticalSize, int caveCurrentIteration, int maxIterations, int branchLevel)
        {
            blockId = airBlockId;// api.World.GetBlock(new AssetLocation("mantle")).BlockId;
            float vertAngleChange = 0;

            ushort[] terrainheightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;
            ushort[] rainheightmap = chunks[0].MapChunk.RainHeightMap;

            float relPos;
            int currentIteration = 0;

            while (currentIteration++ < maxIterations)
            {
                relPos = (float)currentIteration / maxIterations;

                float horRadius = horizontalSize * (1 - relPos * 0.33f);
                float vertRadius = (horRadius) * verticalSize;

                float advanceHor = GameMath.FastCos(vertAngle);
                float advanceVer = GameMath.FastSin(vertAngle);

                if (vertRadius < 1) vertAngle *= 0.1f;

                posX += GameMath.FastCos(horAngle) * advanceHor;
                posY += GameMath.Clamp(advanceVer, -vertRadius, vertRadius);
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
                            posX, posY, posZ,
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

                SetBlocks(chunks, horRadius, vertRadius, posX, posY, posZ, terrainheightmap, rainheightmap, chunkX, chunkZ);
            }
        }




        private bool SetBlocks(IServerChunk[] chunks, float horRadius, float vertRadius, double centerX, double centerY, double centerZ, ushort[] terrainheightmap, ushort[] rainheightmap, int chunkX, int chunkZ)
        {
            IMapChunk mapchunk = chunks[0].MapChunk;
            int chunkSize = worldgenBlockAccessor.ChunkSize;

            // One extra size for checking if we run into water
            horRadius++;
            vertRadius++;

            int mindx = (int)GameMath.Clamp(centerX - horRadius, 0, chunksize - 1);
            int maxdx = (int)GameMath.Clamp(centerX + horRadius + 1, 0, chunksize - 1);
            int mindy = (int)GameMath.Clamp(centerY - vertRadius * 0.7f, 1, worldheight - 1);
            int maxdy = (int)GameMath.Clamp(centerY + vertRadius + 1, 1, worldheight - 1);
            int mindz = (int)GameMath.Clamp(centerZ - horRadius, 0, chunksize - 1);
            int maxdz = (int)GameMath.Clamp(centerZ + horRadius + 1, 0, chunksize - 1);

            double xdistRel, ydistRel, zdistRel;
            double hRadiusSq = horRadius * horRadius;
            double vRadiusSq = vertRadius * vertRadius;
            double distortStrength = vertRadius < 1.5 ? 1 / 22.0 : 1 / 11.0;


            bool foundWater = false;
            for (int lx = mindx; lx <= maxdx && !foundWater; lx++)
            {
                xdistRel = (lx - centerX) * (lx - centerX) / hRadiusSq;
                
                for (int lz = mindz; lz <= maxdz && !foundWater; lz++)
                {
                    zdistRel = (lz - centerZ) * (lz - centerZ) / hRadiusSq;

                    double heightrnd = (mapchunk.CaveHeightDistort[lz * chunksize + lx] - 127) * distortStrength;

                    for (int y = mindy; y <= maxdy + 10 && !foundWater; y++)
                    {
                        double yDist = y - centerY;
                        double heightOffFac = yDist > 0 ? heightrnd * heightrnd : 0;

                        ydistRel = yDist * yDist / (vRadiusSq + heightOffFac);

                        if (y > worldheight - 1 || xdistRel + ydistRel + zdistRel > 1.0) continue;

                        int ly = y % chunksize;

                        foundWater = chunks[y / chunksize].Blocks[(ly * chunksize + lz) * chunksize + lx] == GlobalConfig.waterBlockId;
                    }
                }
            }

            if (foundWater)
            {
                return false;
            }

            horRadius--;
            vertRadius--;

            mindx = (int)GameMath.Clamp(centerX - horRadius, 0, chunksize - 1);
            maxdx = (int)GameMath.Clamp(centerX + horRadius + 1, 0, chunksize - 1);
            mindz = (int)GameMath.Clamp(centerZ - horRadius, 0, chunksize - 1);
            maxdz = (int)GameMath.Clamp(centerZ + horRadius + 1, 0, chunksize - 1);

            mindy = (int)GameMath.Clamp(centerY - vertRadius * 0.7f, 1, worldheight - 1);
            maxdy = (int)GameMath.Clamp(centerY + vertRadius + 1, 1, worldheight - 1);

            hRadiusSq = horRadius * horRadius;
            vRadiusSq = vertRadius * vertRadius;
            distortStrength = vertRadius < 1.5 ? 1 / 22.0 : 1 / 11.0;

            

            for (int lx = mindx; lx <= maxdx; lx++)
            {
                xdistRel = (lx - centerX) * (lx - centerX) / hRadiusSq;

                for (int lz = mindz; lz <= maxdz; lz++)
                {
                    zdistRel = (lz - centerZ) * (lz - centerZ) / hRadiusSq;

                    double heightrnd = (mapchunk.CaveHeightDistort[lz * chunksize + lx] - 127) * distortStrength;
                    int surfaceY = terrainheightmap[lz * chunksize + lx];

                    for (int y = maxdy + 10; y >= mindy; y--)
                    {
                        double yDist = y - centerY;
                        double heightOffFac = yDist > 0 ? heightrnd * heightrnd * Math.Min(1, Math.Abs(y - surfaceY) /10.0) : 0;

                        ydistRel = yDist * yDist / (vRadiusSq + heightOffFac);

                        if (y > worldheight - 1 || xdistRel + ydistRel + zdistRel > 1.0) continue;

                        ushort[] chunkBlockData = chunks[y / chunksize].Blocks;
                        int ly = y % chunksize;

                        chunkBlockData[(ly * chunksize + lz) * chunksize + lx] = y < 12 ? GlobalConfig.lavaBlockId : blockId;

                        if (terrainheightmap[lz * chunksize + lx] == y)
                        {
                            terrainheightmap[lz * chunksize + lx]--;
                            rainheightmap[lz * chunksize + lx]--;
                        }

                        if (y == 11)
                        {
                            if (basaltNoise.Noise(chunkX * chunkSize + lx, chunkZ * chunkSize + lz) > 0.65)
                            {
                                chunkBlockData[(ly * chunksize + lz) * chunksize + lx] = GlobalConfig.basaltBlockId;
                                terrainheightmap[lz * chunksize + lx] = Math.Max(terrainheightmap[lz * chunksize + lx], (ushort)11);
                                rainheightmap[lz * chunksize + lx] = Math.Max(rainheightmap[lz * chunksize + lx], (ushort)11);
                            }
                            else
                            {
                                worldgenBlockAccessor.ScheduleBlockLightUpdate(new BlockPos(chunkX * chunkSize + lx, y, chunkZ * chunkSize + lz), airBlockId, GlobalConfig.lavaBlockId);
                            }

                        }
                    }
                }
            }

            

            return true;
        }
        
    }
}