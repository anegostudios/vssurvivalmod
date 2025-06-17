using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class GenDevastationLayer : ModStdWorldGen
    {
        private ICoreServerAPI api;
        StoryStructureLocation devastationLocation;
        public IWorldGenBlockAccessor worldgenBlockAccessor;  // used by the Timeswitch to place the towers
        public SimplexNoise distDistort;
        public NormalizedSimplexNoise devastationDensity;

        byte[] noisemap;
        int cellnoiseWidth;
        int cellnoiseHeight;
        private const float fullHeightDist = 0.3f;
        private const float flatHeightDist = 0.4f;

        public override bool ShouldLoad(EnumAppSide side) => true;
        public override double ExecuteOrder() => 0.399;

        public static int[] DevastationBlockIds;
        int growthBlockId;
        int dim2Size;
        const int Dim2HeightOffset = 9;   // Raises the height of dim2 terrain so that it's not crazy different from devastation above the cracks

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("devastation").RegisterMessageType<DevaLocation>();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            api.Event.InitWorldGenerator(InitWorldGen, "standard");
            api.Event.PlayerJoin += Event_PlayerJoin;

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.ChunkColumnGeneration(OnChunkColumnGeneration, EnumWorldGenPass.Terrain, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }

            distDistort = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 20980);
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            if (devastationLocation != null)
            {
                api.Network.GetChannel("devastation").SendPacket(new DevaLocation() { Pos = devastationLocation.CenterPos, Radius = devastationLocation.GenerationRadius }, byPlayer);
            }
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }

        private void InitWorldGen()
        {
            LoadGlobalConfig(api);

            distDistort = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 20980);
            devastationDensity = new NormalizedSimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 25.0, 1 / 12.5, 1 / 6.25, 1 / 3.25 }, api.World.SeaLevel + 20981);

            modSys.storyStructureInstances.TryGetValue("devastationarea", out devastationLocation);
            if (devastationLocation != null)
            {
                Timeswitch ts = api.ModLoader.GetModSystem<Timeswitch>();
                ts.SetPos(devastationLocation.CenterPos);
                dim2Size = ts.SetupDim2TowerGeneration(devastationLocation, modSys);
            }

            var devastationEffects = api.ModLoader.GetModSystem<ModSystemDevastationEffects>();
            devastationEffects.DevaLocationPresent = devastationLocation?.CenterPos.ToVec3d();
            devastationEffects.DevaLocationPast = devastationLocation?.CenterPos.Copy().SetDimension(Dimensions.AltWorld).ToVec3d();
            devastationEffects.EffectRadius = devastationLocation?.GenerationRadius ?? 0;

            var bmp = BitmapCreateFromPng(api.Assets.TryGet("worldgen/devastationcracks.png"));
            var pixels = bmp.Pixels;
            noisemap = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) { noisemap[i] = (byte)(pixels[i] & 0xFF); }

            cellnoiseWidth = bmp.Width;
            cellnoiseHeight = bmp.Height;

            DevastationBlockIds = new int[]
            {
                GetBlockId("devastatedsoil-0"),
                GetBlockId("devastatedsoil-1"),
                GetBlockId("devastatedsoil-2"),
                GetBlockId("devastatedsoil-3"),
                GetBlockId("devastatedsoil-4"),
                GetBlockId("devastatedsoil-5"),
                GetBlockId("devastatedsoil-6"),
                GetBlockId("devastatedsoil-7"),
                GetBlockId("devastatedsoil-8"),
                GetBlockId("devastatedsoil-9"),
                GetBlockId("devastatedsoil-10")
            };

            growthBlockId = GetBlockId("devastationgrowth-normal");
            api.ModLoader.GetModSystem<GenStructures>().OnPreventSchematicPlaceAt += OnPreventSchematicPlaceAt;
        }

        private int GetBlockId(string code)
        {
            Block b = api.World.GetBlock(new AssetLocation(code));
            return b == null ? GlobalConfig.defaultRockId : b.BlockId;
        }

        public BitmapRef BitmapCreateFromPng(IAsset asset)
        {
            return new BitmapExternal(asset.Data, asset.Data.Length, api.Logger);
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
            if (distancesq >= (devastationRadius + 200) * (devastationRadius + 200)) return;

            var rnd = api.World.Rand;

            var chunks = request.Chunks;
            var mapchunk = chunks[0].MapChunk;
            if (ShouldGenerateDim2Terrain(request.ChunkX, request.ChunkZ))
            {
                // We generate Dim2 terrain first, before the WorldGenTerrainHeightMap gets altered by the cracks
                api.WorldManager.CreateChunkColumnForDimension(request.ChunkX, request.ChunkZ, Dimensions.AltWorld);
                GenerateDim2ChunkColumn(request.ChunkX, request.ChunkZ, mapchunk.WorldGenTerrainHeightMap);
            }

            float noiseMax = DevastationBlockIds.Length - 1.01f;
            float noiseSub = DevastationBlockIds.Length;
            float noiseMul = DevastationBlockIds.Length * 2f;

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
                        dy = noisemap[nmapz * cellnoiseWidth + nmapx];

                    var height = (int)Math.Round(offset - dy / 30f);

                    var start = height - 10;
                    for (var i = start; i <= height; i++)
                    {
                        var chunkY = (wgenheight + i) / chunksize;
                        var lY = (wgenheight + i) % chunksize;
                        var index3d = (chunksize * lY + dz) * chunksize + dx;

                        chunks[chunkY].Data.SetBlockUnsafe(index3d, DevastationBlockIds[(int)Math.Round(density)]);
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

                    var newWgenHeigt = (ushort)(wgenheight + height);
                    mapchunk.WorldGenTerrainHeightMap[index2d] = newWgenHeigt;
                    var rainHeight = Math.Max(newWgenHeigt, mapchunk.RainHeightMap[index2d]);
                    mapchunk.RainHeightMap[index2d] = rainHeight;

                    if (rnd.NextDouble() - 0.1 < density && rainHeight == newWgenHeigt)
                    {
                        var chunkY = (wgenheight + height + 1) / chunksize;
                        var lY = (wgenheight + height + 1) % chunksize;
                        var index3d = (chunksize * lY + dz) * chunksize + dx;
                        chunks[chunkY].Data.SetBlockUnsafe(index3d, growthBlockId);
                    }
                }
            }

            api.ModLoader.GetModSystem<Timeswitch>().AttemptGeneration(worldgenBlockAccessor);
        }

        private bool OnPreventSchematicPlaceAt(IBlockAccessor blockAccessor, BlockPos pos, Cuboidi schematicLocation, string locationCode)
        {
            if (locationCode == "devastationarea")
            {
                if (!HasDevastationSoil(blockAccessor, pos, schematicLocation.SizeX, schematicLocation.SizeZ))
                    return true;
            }

            return false;
        }

        private BlockPos tmpPos = new BlockPos();
        private bool HasDevastationSoil(IBlockAccessor blockAccessor, BlockPos startPos, int wdt, int len)
        {
            tmpPos.Set(startPos.X, startPos.Y + 1, startPos.Z);
            var height = blockAccessor.GetTerrainMapheightAt(tmpPos);
            tmpPos.Y = height;
            if (!DevastationBlockIds.Contains(blockAccessor.GetBlockId(tmpPos))) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y + 1, startPos.Z);
            height = blockAccessor.GetTerrainMapheightAt(tmpPos);
            tmpPos.Y = height;
            if (!DevastationBlockIds.Contains(blockAccessor.GetBlockId(tmpPos))) return false;

            tmpPos.Set(startPos.X, startPos.Y + 1, startPos.Z + len);
            height = blockAccessor.GetTerrainMapheightAt(tmpPos);
            tmpPos.Y = height;
            if (!DevastationBlockIds.Contains(blockAccessor.GetBlockId(tmpPos))) return false;

            tmpPos.Set(startPos.X + wdt, startPos.Y + 1, startPos.Z + len);
            height = blockAccessor.GetTerrainMapheightAt(tmpPos);
            tmpPos.Y = height;
            if (!DevastationBlockIds.Contains(blockAccessor.GetBlockId(tmpPos))) return false;

            tmpPos.Set(startPos.X + wdt/2, startPos.Y + 1, startPos.Z + len/2);
            height = blockAccessor.GetTerrainMapheightAt(tmpPos);
            tmpPos.Y = height;
            if (!DevastationBlockIds.Contains(blockAccessor.GetBlockId(tmpPos))) return false;
            return true;
        }

        public override void Dispose()
        {
            DevastationBlockIds = null;
        }


        private bool ShouldGenerateDim2Terrain(int cx, int cz)
        {
            int radius = dim2Size;
            int baseCx = devastationLocation.CenterPos.X / GlobalConstants.ChunkSize;
            int baseCz = devastationLocation.CenterPos.Z / GlobalConstants.ChunkSize;
            int dx = Math.Abs(cx - baseCx);
            int dz = Math.Abs(cz - baseCz);
            if (dx + dz == 0)   // Tower center chunk: if we are generating/regenerating it, then we must have destroyed any previous tower
            {
                devastationLocation.DidGenerateAdditional = false;
            }
            return (dx <= radius && dz <= radius);
        }


        /// <summary>
        /// Used to generate "past" terrain around the tower in dim2
        /// </summary>
        /// <param name="cx"></param>
        /// <param name="cz"></param>
        /// <param name="heightmap">The original world heightmap (before placement of devastation soil and cracks)</param>
        private void GenerateDim2ChunkColumn(int cx, int cz, ushort[] heightmap)
        {
            int rockId = GlobalConfig.defaultRockId;
            int soilId = GetBlockId("soil-medium-none");
            int topsoilId = GetBlockId("soil-medium-normal");
            int grassId1 = GetBlockId("tallgrass-medium-free");
            int grassId2 = GetBlockId("tallgrass-tall-free");

            int miny = api.World.BlockAccessor.MapSizeY - 1;
            int yTop = 0;
            for (int i = 0; i < heightmap.Length; i++)
            {
                int height = heightmap[i] + Dim2HeightOffset;
                if (height < miny) miny = height;
                if (height > yTop) yTop = height;
            }

            int cy = Dimensions.AltWorld * GlobalConstants.DimensionSizeInChunks;
            IWorldChunk chunk = api.World.BlockAccessor.GetChunk(cx, cy, cz);
            if (chunk == null) return;
            chunk.Unpack();
            IChunkBlocks chunkBlockData = chunk.Data;

            // Simplified version of the algorithm in GenTerra

            // First set all the fully solid layers in bulk, as much as possible
            chunkBlockData.SetBlockBulk(0, chunksize, chunksize, GlobalConfig.mantleBlockId);
            int yBase = 1;
            for (; yBase < miny - 3; yBase++)
            {
                if (yBase % chunksize == 0)
                {
                    cy++;
                    chunk = api.World.BlockAccessor.GetChunk(cx, cy, cz);
                    if (chunk == null) break;
                    chunkBlockData = chunk.Data;
                }

                chunkBlockData.SetBlockBulk((yBase % chunksize) * chunksize * chunksize, chunksize, chunksize, rockId);
            }

            // Now call SetBlock layer by layer rising through the layers
            yTop++;  // allow for tallgrass above
            for (int posY = yBase; posY <= yTop; posY++)
            {
                if (posY % chunksize == 0)
                {
                    cy++;
                    chunk = api.World.BlockAccessor.GetChunk(cx, cy, cz);
                    if (chunk == null) break;
                    chunkBlockData = chunk.Data;
                }

                for (int lZ = 0; lZ < chunksize; lZ++)
                {
                    for (int lX = 0; lX < chunksize; lX++)
                    {
                        int terrainY = heightmap[lZ * chunksize + lX] + Dim2HeightOffset;    /// we add a height offset because the devastation soil layer is several blocks thicker than natural worldgen
                        int lY = posY % chunksize;

                        if (posY < terrainY - 2) chunkBlockData[ChunkIndex3D(lX, lY, lZ)] = rockId;
                        else if (posY < terrainY) chunkBlockData[ChunkIndex3D(lX, lY, lZ)] = soilId;
                        else if (posY == terrainY) chunkBlockData[ChunkIndex3D(lX, lY, lZ)] = topsoilId;
                        else if (posY == terrainY + 1)
                        {
                            // random tallgrass
                            int rand = GameMath.oaatHash(lX + cx * chunksize, lZ + cz * chunksize);
                            if (rand % 21 < 3) chunkBlockData[ChunkIndex3D(lX, lY, lZ)] = (rand % 21 == 0) ? grassId2 : grassId1;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ChunkIndex3D(int lx, int ly, int lz)
        {
            return (ly * chunksize + lz) * chunksize + lx;
        }
    }
}
