﻿using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.ServerMods
{
    public class GenDevastationLayer : ModStdWorldGen
    {
        private ICoreServerAPI api;
        StoryStructureLocation devastationLocation;
        public SimplexNoise distDistort;
        public NormalizedSimplexNoise devastationDensity;

        byte[] noisemap;
        int cellnoiseWidth;
        int cellnoiseHeight;
        private const float fullHeightDist = 0.3f;
        private const float flatHeightDist = 0.4f;

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
        public override double ExecuteOrder() => 0.399;

        int[] devastationBlockIds;
        int growthBlockId;


        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            api.Event.InitWorldGenerator(InitWorldGen, "standard");
            api.Event.PlayerJoin += Event_PlayerJoin;

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Terrain, "standard");
            }

            distDistort = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 20980);

            api.Network.RegisterChannel("devastation").RegisterMessageType<DevaLocation>();
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            if (devastationLocation != null)
            {
                api.Network.GetChannel("devastation").SendPacket(new DevaLocation() { Pos = devastationLocation.CenterPos, Radius = devastationLocation.GenerationRadius }, byPlayer);
            }
        }

        private void InitWorldGen()
        {
            distDistort = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 20980);
            devastationDensity = new NormalizedSimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 25.0, 1 / 12.5, 1 / 6.25, 1 / 3.25 }, api.World.SeaLevel + 20981);

            api.ModLoader.GetModSystem<GenStoryStructures>().storyStructureInstances.TryGetValue("devastationarea", out devastationLocation);
            if (devastationLocation != null)
            {
                api.ModLoader.GetModSystem<Timeswitch>().SetPos(devastationLocation.CenterPos);
            }

            var devastationEffects = api.ModLoader.GetModSystem<DevastationEffects>();
            devastationEffects.DevaLocation = devastationLocation?.CenterPos.ToVec3d();
            devastationEffects.Radius = devastationLocation?.GenerationRadius ?? 0;

            var bmp = BitmapCreateFromPng(api.Assets.TryGet("worldgen/devastationcracks.png"));
            var pixels = bmp.Pixels;
            noisemap = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) { noisemap[i] = (byte)(pixels[i] & 0xFF); }

            cellnoiseWidth = bmp.Width;
            cellnoiseHeight = bmp.Height;

            devastationBlockIds = new int[]
            {
                api.World.GetBlock(new AssetLocation("devastatedsoil-0")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-1")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-2")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-3")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-4")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-5")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-6")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-7")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-8")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-9")).Id,
                api.World.GetBlock(new AssetLocation("devastatedsoil-10")).Id
            };

            growthBlockId = api.World.GetBlock(new AssetLocation("devastationgrowth-normal")).Id;
        }

        public BitmapRef BitmapCreateFromPng(IAsset asset)
        {
            return new BitmapExternal(new MemoryStream(asset.Data));
        }

        private void OnChunkColumnGeneration(IChunkColumnGenerateRequest request)
        {
            if (devastationLocation == null) return;

            var cpos = devastationLocation.CenterPos;
            var devastationRadius = devastationLocation.GenerationRadius;
            var rposx = request.ChunkX * chunksize + chunksize / 2;
            var rposz = request.ChunkZ * chunksize + chunksize / 2;
            double distancesq = cpos.HorDistanceSqTo(rposx, rposz);
            // add 100 and early exit if not within the area
            if (distancesq >= (devastationRadius + 100) * (devastationRadius + 100)) return;

            var rnd = api.World.Rand;

            var chunks = request.Chunks;
            var mapchunk = chunks[0].MapChunk;

            float noiseMax = devastationBlockIds.Length - 1.01f;
            float noiseSub = devastationBlockIds.Length;
            float noiseMul = devastationBlockIds.Length * 2f;

            for (int dx = 0; dx < chunksize; dx++)
            {
                for (int dz = 0; dz < chunksize; dz++)
                {
                    int x = request.ChunkX * chunksize + dx;
                    int z = request.ChunkZ * chunksize + dz;

                    double density = GameMath.Clamp(devastationDensity.Noise(x, z) * noiseMul - noiseSub, 0, noiseMax);

                    double extraDist = distDistort.Noise(x, z);
                    double distsq = cpos.HorDistanceSqTo(x, z);
                    // distance 0 - 1
                    var distance = distsq / (devastationRadius * devastationRadius);
                    // extra dist that allows too smoothly transition between deva layer and normal terrain
                    double distrel = distance + extraDist / 30.0;

                    if (distrel > 1) continue;

                    // generate vlues between 0-1 where we keep it 0 till fullHeightDist and then go from 0-1 and then from fullHeightDist keep it at 1
                    var heightMod = GameMath.Clamp(distance + extraDist / 1000.0, fullHeightDist, flatHeightDist);
                    double heightModMapped = GameMath.Map(heightMod, fullHeightDist, flatHeightDist, 0, 1);

                    // height offset in the inner circle between fullHeightDist <-> flatHeightDist
                    // flip it so the further out we got we approach 0
                    double offset = GameMath.Clamp((1 - heightModMapped) * 10, 0, 10);
                    // offset that allows for more height variation (small bumps) outside of the flatHeightDist
                    double offset2 = GameMath.Clamp((0.60f - distrel) * 20, 0, 10);
                    // smoothly transition between the two
                    // offset +0.2 the second offset to only appear after flatHeightDist was reached
                    offset = GameMath.Max(offset, offset2 * GameMath.Clamp(heightModMapped + 0.2, 0, 0.8));

                    int index2d = dz * chunksize + dx;
                    int wgenheight = mapchunk.WorldGenTerrainHeightMap[index2d];

                    int nmapx = x - cpos.X + cellnoiseWidth / 2;
                    int nmapz = z - cpos.Z + cellnoiseHeight / 2;
                    int dy = 0;

                    if (nmapx >= 0 && nmapz >= 0 && nmapx < cellnoiseWidth && nmapz < cellnoiseHeight)
                        dy = noisemap[(nmapz) * cellnoiseWidth + nmapx];

                    var height = (int)Math.Round(offset - dy / 30f);

                    var start = height - 10;
                    for (var i = start; i <= height; i++)
                    {
                        var chunkY = (wgenheight + i) / chunksize;
                        var lY = (wgenheight + i) % chunksize;
                        var index3d = (chunksize * lY + dz) * chunksize + dx;

                        chunks[chunkY].Data.SetBlockUnsafe(index3d, devastationBlockIds[(int)Math.Round(density)]);
                        chunks[chunkY].Data.SetFluid(index3d, 0);
                    }

                    // if height lower we are below surface
                    if (height < 0)
                    {
                        for (var i = height; i <= 0; i++)
                        {
                            var chunkY = (wgenheight + i) / chunksize;
                            var lY = (wgenheight + i) % chunksize;
                            var index3d = (chunksize * lY + dz) * chunksize + dx;
                            chunks[chunkY].Data.SetBlockUnsafe(index3d, 0);
                            chunks[chunkY].Data.SetFluid(index3d, 0);
                        }
                    }

                    mapchunk.WorldGenTerrainHeightMap[index2d] = (ushort)(wgenheight + height);
                    mapchunk.RainHeightMap[index2d] = (ushort)(wgenheight + height);

                    if (rnd.NextDouble() - 0.1 < density)
                    {
                        var chunkY = (wgenheight + height + 1) / chunksize;
                        var lY = (wgenheight + height + 1) % chunksize;
                        var index3d = (chunksize * lY + dz) * chunksize + dx;
                        chunks[chunkY].Data.SetBlockUnsafe(index3d, growthBlockId);
                    }
                }
            }
        }
    }
}
