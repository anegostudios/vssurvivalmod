using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class GenCaves : GenPartial
    {
        protected override int chunkRange { get { return 5; } }

        public override double ExecuteOrder() { return 0.3; }

        internal LCGRandom caveRand;
        IWorldGenBlockAccessor worldgenBlockAccessor;

        NormalizedSimplexNoise basaltNoise;
        NormalizedSimplexNoise heightvarNoise;
        int regionsize;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.Terrain, "standard");

                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

                api.ChatCommands.GetOrCreate("dev")
                    .BeginSubCommand("gencaves")
                    .WithDescription(
                        "Cave generator test tool. Deletes all chunks in the area 10х10 and generates inverse caves around the player.")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(CmdCaveGenTest)
                    .EndSubCommand();

                api.Event.MapChunkGeneration(OnMapChunkGen, "standard");
                api.Event.MapChunkGeneration(OnMapChunkGen, "superflat");
                api.Event.InitWorldGenerator(initWorldGen, "superflat");
            }
        }


        public override void initWorldGen()
        {
            base.initWorldGen();
            caveRand = new LCGRandom(api.WorldManager.Seed + 123128);
            basaltNoise = NormalizedSimplexNoise.FromDefaultOctaves(2, 1f / 3.5f, 0.9f, api.World.Seed + 12);
            heightvarNoise = NormalizedSimplexNoise.FromDefaultOctaves(3, 1f / 20f, 0.9f, api.World.Seed + 12);

            regionsize = api.World.BlockAccessor.RegionSize;
        }


        private void OnMapChunkGen(IMapChunk mapChunk, int chunkX, int chunkZ)
        {
            mapChunk.CaveHeightDistort = new byte[chunksize * chunksize];

            for (int dx = 0; dx < chunksize; dx++)
            {
                for (int dz = 0; dz < chunksize; dz++)
                {
                    double val = heightvarNoise.Noise(chunksize * chunkX + dx, chunksize * chunkZ + dz) - 0.5;
                    val = val > 0 ? Math.Max(0, val - 0.07) : Math.Min(0, val + 0.07);

                    mapChunk.CaveHeightDistort[dz * chunksize + dx] = (byte)(128 * val + 127);
                }
            }
        }


        // command to test cave generation
        private TextCommandResult CmdCaveGenTest(TextCommandCallingArgs args)
        {
            caveRand = new LCGRandom(api.WorldManager.Seed + 123128);
            initWorldGen();

            // replace the air block with granite to generate inverted caves
            airBlockId = api.World.GetBlock(new AssetLocation("rock-granite")).BlockId;

            // take the player as the center
            var player = args.Caller.Player as IServerPlayer;
            int baseChunkX = (int)player.Entity.Pos.X / chunksize;
            int baseChunkZ = (int)player.Entity.Pos.Z / chunksize;

            // first check that all chunks are loaded
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
                            return TextCommandResult.Success( "Cannot generate 10x10 area of caves, chunks are not loaded that far yet.");
                        }
                    }

                    OnMapChunkGen(chunks[0].MapChunk, chunkX, chunkZ);
                }
            }

            // clear all chunks and start cave generation
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

                    // Run lighting recalculation (like in GenLightSurvival)
                    worldgenBlockAccessor.BeginColumn();
                    api.WorldManager.SunFloodChunkColumnForWorldGen(chunks, chunkX, chunkZ);
                    worldgenBlockAccessor.RunScheduledBlockLightUpdates(chunkX, chunkZ);


                    MarkDirty(chunkX, chunkZ, chunks);
                }
            }

            
            // restore the air block id back
            airBlockId = 0;

            return TextCommandResult.Success("Generated and chunks force resend flags set");
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
                api.WorldManager.BroadcastChunk(chunkX, chunkY, chunkZ, true);
            }
        }

        private bool ClearChunkColumn(IServerChunk[] chunks)
        {
            for (int i = 0; i < chunks.Length; i++)
            {
                IServerChunk chunk = chunks[i];
                if (chunk == null) return false;

                chunk.Unpack();
                chunk.Data.ClearBlocks();
                chunk.MarkModified();
            }

            return true;
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }

        public override void GeneratePartial(IServerChunk[] chunks, int chunkX, int chunkZ, int cdx, int cdz)
        {
            if (GetIntersectingStructure(chunkX * chunksize + chunksize / 2, chunkZ * chunksize + chunksize / 2, SkipCavesgHashCode) != null)
            {
                return;
            }

            worldgenBlockAccessor.BeginColumn();
            LCGRandom chunkRand = this.chunkRand;
            int quantityCaves = chunkRand.NextInt(100) < TerraGenConfig.CavesPerChunkColumn*100 ? 1 : 0;

            int rndSize = chunksize * chunksize * (worldheight - 20);
            while (quantityCaves-- > 0)
            {
                int rnd = chunkRand.NextInt(rndSize);
                int posX = cdx * chunksize + rnd % chunksize;
                rnd /= chunksize;
                int posZ = cdz * chunksize + rnd % chunksize;
                rnd /= chunksize;
                int posY = rnd + 8;

                float horAngle = chunkRand.NextFloat() * GameMath.TWOPI;
                float vertAngle = (chunkRand.NextFloat() - 0.5f) * 0.25f;
                float horizontalSize = chunkRand.NextFloat() * 2 + chunkRand.NextFloat();
                float verticalSize = 0.75f + chunkRand.NextFloat() * 0.4f;

                rnd = chunkRand.NextInt(100 * 50 * 1000 * 100);
                if (rnd % 100 < 4)    // 4% chance
                {
                    horizontalSize = chunkRand.NextFloat() * 2 + chunkRand.NextFloat() + chunkRand.NextFloat();
                    verticalSize = 0.25f + chunkRand.NextFloat() * 0.2f;
                }
                else
                if (rnd % 100 == 4)   // 1% chance
                {
                    horizontalSize = 0.75f + chunkRand.NextFloat();
                    verticalSize = chunkRand.NextFloat() * 2 + chunkRand.NextFloat();
                }
                rnd /= 100;   // range of rnd is now 0 - 4,999,999

                bool extraBranchy = (posY < TerraGenConfig.seaLevel / 2) ? rnd % 50 == 0 : false;     // 2% chance
                rnd /= 50;   // range of rnd is now 0 - 99,999
                int rnd1000 = rnd % 1000;
                rnd /= 1000;  // range of rnd is  now 0 - 99
                bool largeNearLavaLayer = rnd1000 % 10 < 3;   // 0.3 chance

                float curviness = rnd == 0 ? 0.035f : (rnd1000 < 30 ? 0.5f : 0.1f);    // 0.01 chance;  0.03 chance)

                int maxIterations = chunkRange * chunksize - chunksize / 2;
                maxIterations = maxIterations - chunkRand.NextInt(maxIterations / 4);

                caveRand.SetWorldSeed(chunkRand.NextInt(10000000));
                caveRand.InitPositionSeed(chunkX + cdx, chunkZ + cdz);
                CarveTunnel(chunks, chunkX, chunkZ, posX, posY, posZ, horAngle, vertAngle, horizontalSize, verticalSize, 0, maxIterations, 0, extraBranchy, curviness, largeNearLavaLayer);
            }
        }

        private void CarveTunnel(IServerChunk[] chunks, int chunkX, int chunkZ, double posX, double posY, double posZ, float horAngle, float vertAngle, float horizontalSize, float verticalSize, int currentIteration, int maxIterations, int branchLevel, bool extraBranchy = false, float curviness = 0.1f, bool largeNearLavaLayer = false)
        {
            LCGRandom caveRand = this.caveRand;

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

                float vertRadius = 1.5f + GameMath.FastSin(relPos * GameMath.PI) * (verticalSize + horRadiusLossAccum / 4f) + verHeightGainAccum; // - horRadiusGainAccum / 2
                vertRadius = Math.Min(vertRadius, Math.Max(0.6f, vertRadius - verHeightLossAccum));

                float advanceHor = GameMath.FastCos(vertAngle);
                float advanceVer = GameMath.FastSin(vertAngle);

                // Caves get bigger near y=12
                if (largeNearLavaLayer)
                {
                    float factor = 1f + Math.Max(0f, 1f - (float)Math.Abs(posY - 12d) / 10f);
                    horRadius *= factor;
                    vertRadius *= factor;
                }

                if (vertRadius < 1f) vertAngle *= 0.1f;

                posX += GameMath.FastCos(horAngle) * advanceHor;
                posY += GameMath.Clamp(advanceVer, -vertRadius, vertRadius);
                posZ += GameMath.FastSin(horAngle) * advanceHor;

                vertAngle *= 0.8f;

                int rrnd = caveRand.NextInt(800000);
                if (rrnd / 10000 == 0)   // chance 1/80
                {
                    sizeChangeSpeedGain = (caveRand.NextFloat() * caveRand.NextFloat())/2;
                }

                bool genHotSpring=false;

                int rnd = rrnd % 10000; // Calls to caveRand are not too cheap, so lets just use one for all those random variation changes
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
                    horRadiusGain = caveRand.NextFloat() * caveRand.NextFloat() * 3.5f;
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
                        verHeightLoss = caveRand.NextFloat() * caveRand.NextFloat() * 12f;
                        horRadiusGain = Math.Max(horRadiusGain, caveRand.NextFloat() * caveRand.NextFloat() * 3f);
                    }
                } else
                // Very rarely go really wide
                if ((rnd -= 9) <= 0)
                {
                    if (posY < TerraGenConfig.seaLevel - 20)
                    {
                        horRadiusGain = 1 + caveRand.NextFloat() * caveRand.NextFloat() * 5f;
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

                if (posY > -5 && posY < 16 && horRadius > 4 && vertRadius > 2)
                {
                    genHotSpring = true;
                }

                sizeChangeSpeedAccum = Math.Max(0.1f, sizeChangeSpeedAccum + sizeChangeSpeedGain * 0.05f);
                sizeChangeSpeedGain -= 0.02f;

                horRadiusGainAccum = Math.Max(0f, horRadiusGainAccum + horRadiusGain * sizeChangeSpeedAccum);
                horRadiusGain -= 0.45f;

                horRadiusLossAccum = Math.Max(0f, horRadiusLossAccum + horRadiusLoss * sizeChangeSpeedAccum);
                horRadiusLoss -= 0.4f;

                verHeightGainAccum = Math.Max(0f, verHeightGainAccum + verHeightGain * sizeChangeSpeedAccum);
                verHeightGain -= 0.45f;

                verHeightLossAccum = Math.Max(0f, verHeightLossAccum + verHeightLoss * sizeChangeSpeedAccum);
                verHeightLoss -= 0.4f;

                horAngle += curviness * horAngleChange;
                vertAngle += curviness * vertAngleChange;


                // somewhat costly
                vertAngleChange = 0.9f * vertAngleChange + caveRand.NextFloatMinusToPlusOne() * caveRand.NextFloat() * 3f;
                horAngleChange = 0.9f * horAngleChange + caveRand.NextFloatMinusToPlusOne() * caveRand.NextFloat();

                if (rrnd % 140 == 0)
                {
                    horAngleChange *= caveRand.NextFloat() * 6;
                }

                // Horizontal branch
                int brand = branchRand + 2 * Math.Max(0, (int)posY - (TerraGenConfig.seaLevel - 20)); // Lower chance of branches above sealevel because Saraty does not like strongly cut out mountains
                if (branchLevel < 3 && (vertRadius > 1f || horRadius > 1f) && caveRand.NextInt(brand) == 0)
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
                if (branchLevel < 1 && horRadius > 3f && posY > 60 && caveRand.NextInt(60) == 0)
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



                if (horRadius >= 2 && rrnd % 5 == 0) continue;

                // Check just to prevent unnecessary calculations
                // As long as we are outside the currently generating chunk, we don't need to generate anything
                if (posX <= -horRadius * 2 || posX >= chunksize + horRadius * 2 || posZ <= -horRadius * 2 || posZ >= chunksize + horRadius * 2) continue;

                SetBlocks(chunks, horRadius, vertRadius + verHeightGainAccum, posX, posY + verHeightGainAccum / 2, posZ, terrainheightmap, rainheightmap, chunkX, chunkZ, genHotSpring);
            }
        }



        private void CarveShaft(IServerChunk[] chunks, int chunkX, int chunkZ, double posX, double posY, double posZ, float horAngle, float vertAngle, float horizontalSize, float verticalSize, int caveCurrentIteration, int maxIterations, int branchLevel)
        {
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
                            caveRand.NextFloat() * GameMath.TWOPI,
                            (caveRand.NextFloat() - 0.5f) * 0.25f,
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

                SetBlocks(chunks, horRadius, vertRadius, posX, posY, posZ, terrainheightmap, rainheightmap, chunkX, chunkZ, false);
            }
        }




        private bool SetBlocks(IServerChunk[] chunks, float horRadius, float vertRadius, double centerX, double centerY, double centerZ, ushort[] terrainheightmap, ushort[] rainheightmap, int chunkX, int chunkZ, bool genHotSpring)
        {
            IMapChunk mapchunk = chunks[0].MapChunk;

            // Regular generation radius
            float genHorRadius = horRadius;
            float genVertRadius = vertRadius;

            // Increased radius for fluid checks
            float checkHorRadius = horRadius + 1;
            float checkVertRadius = vertRadius + 2;

            // Compute common bounds for both radii
            int mindx = (int)GameMath.Clamp(centerX - checkHorRadius, 0, chunksize - 1);
            int maxdx = (int)GameMath.Clamp(centerX + checkHorRadius + 1, 0, chunksize - 1);
            int mindz = (int)GameMath.Clamp(centerZ - checkHorRadius, 0, chunksize - 1);
            int maxdz = (int)GameMath.Clamp(centerZ + checkHorRadius + 1, 0, chunksize - 1);

            // For Y use the check radius because it is larger
            int mindy = (int)GameMath.Clamp(centerY - checkVertRadius * 0.7f, 1, worldheight - 1);
            int maxdy = (int)GameMath.Clamp(centerY + checkVertRadius + 1, 1, worldheight - 1);

            // Precompute squared radii for fast comparisons
            double genHorRadiusSq = genHorRadius * genHorRadius;
            double genVertRadiusSq = genVertRadius * genVertRadius;
            double checkHorRadiusSq = checkHorRadius * checkHorRadius;
            double checkVertRadiusSq = checkVertRadius * checkVertRadius;

            // Get geologic activity once
            int geoActivity = getGeologicActivity(chunkX * chunksize + (int)centerX, chunkZ * chunksize + (int)centerZ);
            genHotSpring &= geoActivity > 128;

            if (genHotSpring && centerX >= 0 && centerX < 32 && centerZ >= 0 && centerZ < 32)
            {
                var data = mapchunk.GetModdata<Dictionary<Vec3i, HotSpringGenData>>("hotspringlocations");
                if (data == null) data = new Dictionary<Vec3i, HotSpringGenData>();
                data[new Vec3i((int)centerX, (int)centerY, (int)centerZ)] = new HotSpringGenData() { horRadius = genHorRadius };
                mapchunk.SetModdata("hotspringlocations", data);
            }

            int yLavaStart = (geoActivity * 16) / 128;
            int chunksizeSq = chunksize * chunksize;

            // Main loop - single pass
            for (int lx = mindx; lx <= maxdx; lx++)
            {
                double dx = lx - centerX;
                double dxSq = dx * dx;

                for (int lz = mindz; lz <= maxdz; lz++)
                {
                    double dz = lz - centerZ;
                    double dzSq = dz * dz;

                    int idx2d = lz * chunksize + lx;

                    // Compute distortion once for the column
                    double distortStrength = GameMath.Clamp(genVertRadius / 4.0, 0, 0.1);
                    double heightrnd = (mapchunk.CaveHeightDistort[idx2d] - 127) * distortStrength;
                    int surfaceY = terrainheightmap[idx2d];

                    // Compute maximum XZ distance for the check radius
                    double checkXZDist = dxSq / checkHorRadiusSq + dzSq / checkHorRadiusSq;
                    if (checkXZDist > 1.0) continue;

                    // Compute Y range for the check radius
                    double maxCheckDist = 1.0 - checkXZDist;
                    double maxCheckYDist = Math.Sqrt(maxCheckDist * checkVertRadiusSq);
                    int checkMindy = (int)Math.Max(mindy, centerY - maxCheckYDist);
                    int checkMaxdy = (int)Math.Min(maxdy, centerY + maxCheckYDist);

                    // Variables for tracking state
                    bool hasLiquidInCheckRadius = false;
                    bool needsGenInGenRadius = false;
                    int firstGenY = -1;

                    // Walk Y once from top down
                    for (int y = checkMaxdy; y >= checkMindy; y--)
                    {
                        double dy = y - centerY;
                        double dySq = dy * dy;

                        // Check inclusion in check radius (for fluids)
                        double checkYDistSq = dySq / checkVertRadiusSq;
                        if (dxSq / checkHorRadiusSq + checkYDistSq + dzSq / checkHorRadiusSq <= 1.0)
                        {
                            // Fluid check
                            if (!hasLiquidInCheckRadius && y >= 1 && y < worldheight)
                            {
                                int chunkY = y / chunksize;
                                int localY = y % chunksize;
                                var block = api.World.Blocks[chunks[chunkY].Data.GetFluid((localY * chunksize + lz) * chunksize + lx)];
                                if (block.LiquidCode != null)
                                {
                                    return false; // Found fluid - exit immediately
                                }
                            }
                        }

                        // Check inclusion in generation radius (for setting blocks)
                        if (!needsGenInGenRadius)
                        {
                            double heightOffFac = dy > 0 ? heightrnd * heightrnd * Math.Min(1, Math.Abs(y - surfaceY) / 10.0) : 0;
                            double genYDistSq = dySq / (genVertRadiusSq + heightOffFac);

                            if (dxSq / genHorRadiusSq + genYDistSq + dzSq / genHorRadiusSq <= 1.0 && y < worldheight)
                            {
                                needsGenInGenRadius = true;
                                firstGenY = y;
                            }
                        }
                    }

                    // If we need to generate in this column
                    if (needsGenInGenRadius)
                    {
                        // Compute precise range for generation
                        double genXZDist = dxSq / genHorRadiusSq + dzSq / genHorRadiusSq;
                        double maxGenDist = 1.0 - genXZDist;

                        if (maxGenDist > 0)
                        {
                            // For positive Y account for distortion
                            double vertRadiusSqWithDistort = genVertRadiusSq;
                            if (firstGenY > centerY)
                            {
                                double heightOffFac = heightrnd * heightrnd * Math.Min(1, Math.Abs(firstGenY - surfaceY) / 10.0);
                                vertRadiusSqWithDistort += heightOffFac;
                            }

                            double maxGenYDist = Math.Sqrt(maxGenDist * vertRadiusSqWithDistort);
                            int genMindy = (int)Math.Max(mindy, centerY - maxGenYDist);
                            int genMaxdy = (int)Math.Min(maxdy, centerY + maxGenYDist);

                            // Generate blocks in the column
                            for (int y = genMaxdy; y >= genMindy; y--)
                            {
                                double dy = y - centerY;
                                double dySq = dy * dy;

                                // Check inclusion in ellipsoid with distortion
                                double heightOffFac = dy > 0 ? heightrnd * heightrnd * Math.Min(1, Math.Abs(y - surfaceY) / 10.0) : 0;
                                if (dxSq / genHorRadiusSq + dySq / (genVertRadiusSq + heightOffFac) + dzSq / genHorRadiusSq > 1.0)
                                    continue;

                                if (y > worldheight - 1) continue;

                                // Update heightmaps
                                if (surfaceY == y)
                                {
                                    terrainheightmap[idx2d] = (ushort)(y - 1);
                                    rainheightmap[idx2d]--;
                                }

                                int chunkY = y / chunksize;
                                int localY = y % chunksize;
                                IChunkBlocks chunkBlockData = chunks[chunkY].Data;
                                int index3d = localY * chunksizeSq + idx2d;

                                // Set block
                                if (y == 11)
                                {
                                    if (basaltNoise.Noise(chunkX * chunksize + lx, chunkZ * chunksize + lz) > 0.65)
                                    {
                                        chunkBlockData[index3d] = basaltBlockId;
                                        terrainheightmap[idx2d] = Math.Max(terrainheightmap[idx2d], (ushort)11);
                                        rainheightmap[idx2d] = Math.Max(rainheightmap[idx2d], (ushort)11);
                                    }
                                    else
                                    {
                                        if (y > yLavaStart)
                                        {
                                            chunkBlockData[index3d] = basaltBlockId;
                                        }
                                        else
                                        {
                                            chunkBlockData.SetFluid(index3d, lavaBlockId);
                                        }

                                        if (y <= yLavaStart)
                                            worldgenBlockAccessor.ScheduleBlockLightUpdate(
                                                new BlockPos(chunkX * chunksize + lx, y, chunkZ * chunksize + lz),
                                                airBlockId, lavaBlockId);
                                    }
                                }
                                else if (y < 12)
                                {
                                    if (y > yLavaStart)
                                    {
                                        chunkBlockData[index3d] = basaltBlockId;
                                    }
                                    else
                                    {
                                        chunkBlockData.SetFluid(index3d, lavaBlockId);
                                    }
                                }
                                else
                                {
                                    chunkBlockData[index3d] = airBlockId != 0 ? (ushort)airBlockId : (ushort)0;
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }



        private int getGeologicActivity(int posx, int posz)
        {
            var climateMap = worldgenBlockAccessor.GetMapRegion(posx / regionsize, posz / regionsize)?.ClimateMap;
            if (climateMap == null) return 0;
            int regionChunkSize = regionsize / chunksize;
            float fac = (float)climateMap.InnerSize / regionChunkSize;
            int rlX = (posx / chunksize) % regionChunkSize;
            int rlZ = (posz / chunksize) % regionChunkSize;

            return climateMap.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac)) & 0xff;
        }

    }
}
