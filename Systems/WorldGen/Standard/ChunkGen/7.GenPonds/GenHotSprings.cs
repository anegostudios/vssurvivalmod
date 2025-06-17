using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.ServerMods
{
    [ProtoContract]
    public class HotSpringGenData
    {
        [ProtoMember(1)]
        public double horRadius;
        [ProtoMember(2)]
        public double verRadiusSq;
    }


    public class GenHotSprings : ModStdWorldGen
    {
        Block[] decorBlocks;
        Block blocksludgygravel;
        int boilingWaterBlockId;

        ICoreServerAPI api;
        IWorldGenBlockAccessor wgenBlockAccessor;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            this.api = api;

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(GenChunkColumn, EnumWorldGenPass.TerrainFeatures, "standard");

                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
                api.Event.InitWorldGenerator(initWorldGen, "standard");
            }
        }
        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            wgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }


        public void initWorldGen()
        {
            LoadGlobalConfig(api);

            decorBlocks = new Block[] {
                api.World.GetBlock(new AssetLocation("hotspringbacteria-87deg")),
                api.World.GetBlock(new AssetLocation("hotspringbacteriasmooth-74deg")),
                api.World.GetBlock(new AssetLocation("hotspringbacteriasmooth-65deg")),
                api.World.GetBlock(new AssetLocation("hotspringbacteriasmooth-55deg"))
            };

            blocksludgygravel = api.World.GetBlock(new AssetLocation("sludgygravel"));
            boilingWaterBlockId = api.World.GetBlock(new AssetLocation("boilingwater-still-7")).Id;
        }



        private void GenChunkColumn(IChunkColumnGenerateRequest request)
        {
            var data = request.Chunks[0].MapChunk.GetModdata<Dictionary<Vec3i, HotSpringGenData>>("hotspringlocations");
            if (data == null) return;

            if (GetIntersectingStructure(request.ChunkX * chunksize + chunksize / 2, request.ChunkZ * chunksize + chunksize / 2, SkipHotSpringsgHashCode) != null) return;

            int baseX = request.ChunkX * chunksize;
            int baseZ = request.ChunkZ * chunksize;

            foreach (var keyval in data)
            {
                var centerPos = keyval.Key;
                var gendata = keyval.Value;

                genHotspring(baseX, baseZ, centerPos, gendata);
            }
        }

        private void genHotspring(int baseX, int baseZ, Vec3i centerPos, HotSpringGenData gendata)
        {
            double radiusMul = 2;
            double doubleRad = radiusMul * gendata.horRadius;

            int mindx = (int)GameMath.Clamp(centerPos.X - doubleRad, -chunksize, 2 * chunksize - 1);
            int maxdx = (int)GameMath.Clamp(centerPos.X + doubleRad + 1, -chunksize, 2 * chunksize - 1);
            int mindz = (int)GameMath.Clamp(centerPos.Z - doubleRad, -chunksize, 2 * chunksize - 1);
            int maxdz = (int)GameMath.Clamp(centerPos.Z + doubleRad + 1, -chunksize, 2 * chunksize - 1);

            double xdistRel, zdistRel;
            double hRadiusSq = doubleRad * doubleRad;

            int minSurfaceY = 99999;
            int maxSurfaceY = 0;
            int checks = 0;
            long sum = 0;

            bool lakeHere = false;
            for (int lx = mindx; lx <= maxdx; lx++)
            {
                xdistRel = (lx - centerPos.X) * (lx - centerPos.X) / hRadiusSq;

                for (int lz = mindz; lz <= maxdz; lz++)
                {
                    zdistRel = (lz - centerPos.Z) * (lz - centerPos.Z) / hRadiusSq;
                    var xzdist = xdistRel + zdistRel;
                    if (xzdist < 1)
                    {
                        var mc = wgenBlockAccessor.GetMapChunk((baseX + lx) / chunksize, (baseZ + lz) / chunksize);
                        if (mc == null) return;
                        int surfaceY = mc.WorldGenTerrainHeightMap[GameMath.Mod(lz, chunksize) * chunksize + GameMath.Mod(lx, chunksize)];
                        minSurfaceY = Math.Min(minSurfaceY, surfaceY);
                        maxSurfaceY = Math.Max(maxSurfaceY, surfaceY);
                        checks++;
                        sum += surfaceY;

                        Block fluidBlock = wgenBlockAccessor.GetBlock(baseX + lx, surfaceY + 1, baseZ + lz, BlockLayersAccess.Fluid);
                        lakeHere |= (fluidBlock.Id != 0 && fluidBlock.LiquidCode != "boilingwater");   // Suppress hot springs also in lakeice, saltwater etc.
                    }
                }
            }

            int avgSurfaceY = (int)Math.Round((double)sum / checks);
            int surfaceRoughness = maxSurfaceY - minSurfaceY;

            // Already a lake here
            if (lakeHere)
            {
                return;
            }
            // Too steep, underwater or in mountains
            if (surfaceRoughness >= 4 || minSurfaceY < api.World.SeaLevel + 1 || minSurfaceY > api.WorldManager.MapSizeY * 0.88f)
            {
                return;
            }

            gendata.horRadius = Math.Min(32, gendata.horRadius);

            for (int lx = mindx; lx <= maxdx; lx++)
            {
                xdistRel = (lx - centerPos.X) * (lx - centerPos.X) / hRadiusSq;

                for (int lz = mindz; lz <= maxdz; lz++)
                {
                    zdistRel = (lz - centerPos.Z) * (lz - centerPos.Z) / hRadiusSq;

                    var xzdist = xdistRel + zdistRel;
                    if (xzdist < 1)
                    {
                        genhotSpringColumn(baseX + lx, avgSurfaceY, baseZ + lz, xzdist);
                    }
                }
            }
        }

        private void genhotSpringColumn(int posx, int posy, int posz, double xzdist)
        {
            var mapchunk = wgenBlockAccessor.GetChunkAtBlockPos(posx, posy, posz)?.MapChunk;
            if (mapchunk == null) return;

            int lx = posx % chunksize;
            int lz = posz % chunksize; ushort[] terrainheightmap = mapchunk.WorldGenTerrainHeightMap;
            int surfaceY = terrainheightmap[lz * chunksize + lx];

            xzdist += (api.World.Rand.NextDouble() / 6 - 1/12.0) * 0.5;

            BlockPos pos = new BlockPos(posx, posy, posz);
            var hereFluid = wgenBlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
            var heredecorBlock = wgenBlockAccessor.GetDecor(pos, new DecorBits(BlockFacing.UP));

            int decorBlockIndex = (int)Math.Max(1, xzdist * 10);
            var decorBlock = decorBlockIndex < decorBlocks.Length ? decorBlocks[decorBlockIndex] : null;
            for (int i = 0; i < Math.Min(decorBlocks.Length-1, decorBlockIndex); i++)
            {
                if (decorBlocks[i] == heredecorBlock)
                {
                    // Already has a hotter temperature decor block here
                    decorBlock = decorBlocks[i];
                    break;
                }
            }

            if (hereFluid.Id != 0)
            {
                // Already boiling water here
                return;
            }

            bool gravelPlaced = false;

            // 100% sludgy gravel for radius<60%, randomized for beyond
            if (api.World.Rand.NextDouble() > xzdist - 0.4)
            {
                prepareHotSpringBase(posx, posy, posz, surfaceY, true, decorBlock);
                wgenBlockAccessor.SetBlock(blocksludgygravel.Id, pos);
                gravelPlaced = true;
            }

            // Boiling water for <= 20% radius
            if (xzdist < 0.1)
            {
                prepareHotSpringBase(posx, posy, posz, surfaceY, false, null);
                wgenBlockAccessor.SetBlock(0, pos, BlockLayersAccess.SolidBlocks);
                wgenBlockAccessor.SetBlock(boilingWaterBlockId, pos);
                wgenBlockAccessor.SetDecor(decorBlocks[0], pos.DownCopy(), BlockFacing.UP);
            }
            // Bacerial mat otherwise
            else if (decorBlock != null)
            {
                prepareHotSpringBase(posx, posy, posz, surfaceY, true, decorBlock);

                var upblock = wgenBlockAccessor.GetBlockAbove(pos, 1, BlockLayersAccess.Solid);
                var upblock2 = wgenBlockAccessor.GetBlockAbove(pos, 2, BlockLayersAccess.Solid);
                if (upblock2.SideSolid[BlockFacing.UP.Index]) pos.Y += 2;
                else if (upblock.SideSolid[BlockFacing.UP.Index]) pos.Y++;

                wgenBlockAccessor.SetDecor(decorBlock, pos, BlockFacing.UP);
            } else
            {
                if (xzdist < 0.8 && !gravelPlaced)
                {
                    prepareHotSpringBase(posx, posy, posz, surfaceY, true, decorBlock);
                }
            }
        }

        private void prepareHotSpringBase(int posx, int posy, int posz, int surfaceY, bool preventLiquidSpill = true, Block sideDecorBlock = null)
        {
            BlockPos pos = new BlockPos(posx, posy, posz);
            // Dig free up
            for (int y = posy + 1; y <= surfaceY + 1; y++)
            {
                pos.Y = y;
                var block = wgenBlockAccessor.GetBlock(pos);
                var lblock = wgenBlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
                if (preventLiquidSpill && (block == blocksludgygravel || lblock.Id == boilingWaterBlockId)) break;

                wgenBlockAccessor.SetBlock(0, pos, BlockLayersAccess.SolidBlocks);
                wgenBlockAccessor.SetBlock(0, pos, BlockLayersAccess.Fluid);
                wgenBlockAccessor.SetDecor(api.World.Blocks[0], pos, BlockFacing.UP);

                for (int i = 0; i < Cardinal.ALL.Length; i++)
                {
                    var card = Cardinal.ALL[i];
                    var npos = new BlockPos(pos.X + card.Normali.X, pos.Y, pos.Z + card.Normali.Z);
                    var nlblock = wgenBlockAccessor.GetBlock(npos, BlockLayersAccess.Fluid);
                    if (nlblock.Id != 0)
                    {
                        wgenBlockAccessor.SetDecor(api.World.Blocks[0], npos.DownCopy(), BlockFacing.UP);
                        wgenBlockAccessor.SetBlock(blocksludgygravel.Id, npos, BlockLayersAccess.SolidBlocks);

                        if (sideDecorBlock != null)
                        {
                            wgenBlockAccessor.SetDecor(sideDecorBlock, npos, BlockFacing.UP);
                        }
                    }
                }
            }

            int lx = posx % chunksize;
            int lz = posz % chunksize;
            var mc = wgenBlockAccessor.GetMapChunk(posx / chunksize, posz / chunksize);
            mc.RainHeightMap[lz * chunksize + lx] = (ushort)posy;
            mc.WorldGenTerrainHeightMap[lz * chunksize + lx] = (ushort)posy;
            int blockRockid = mc.TopRockIdMap[lz * chunksize + lx];

            // Build base down
            for (int y = posy; y >= posy - 2; y--)
            {
                pos.Y = y;
                wgenBlockAccessor.SetDecor(api.World.Blocks[0], pos, BlockFacing.UP);
                wgenBlockAccessor.SetBlock(blocksludgygravel.Id, pos);
            }
        }

    }
}
