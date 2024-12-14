using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent;

public class BlockBituCoal: BlockOre
{
    private static Block clay;
    private static RockStrataConfig rockStrata;
    private static LCGRandom rand;
    private const int chunksize = GlobalConstants.ChunkSize;
    private static int regionChunkSize;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (clay == null && api is ICoreServerAPI sapi)
        {
            regionChunkSize = sapi.WorldManager.RegionSize / chunksize;
            rockStrata= BlockLayerConfig.GetInstance(sapi).RockStrata;
            clay = api.World.BlockAccessor.GetBlock(new AssetLocation("rawclay-fire-none"));
            rand = new LCGRandom(api.World.Seed);
        }
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);
        rockStrata = null;
        clay = null;
        rand = null;
    }

    public float GetDepositDistortTop(BlockPos pos, int lx, int lz, IMapChunk heremapchunk)
    {
        var rdx = (pos.X / chunksize) % regionChunkSize;
        var rdz = (pos.Z / chunksize) % regionChunkSize;
        var reg = heremapchunk.MapRegion;
        var step = (float)heremapchunk.MapRegion.OreMapVerticalDistortTop.InnerSize / regionChunkSize;
        return reg.OreMapVerticalDistortTop.GetIntLerpedCorrectly(rdx * step + step * ((float)lx / chunksize), rdz * step + step * ((float)lz / chunksize));
    }

    public float GetDepositDistortBot(BlockPos pos, int lx, int lz, IMapChunk heremapchunk)
    {
        var rdx = (pos.X / chunksize) % regionChunkSize;
        var rdz = (pos.Z / chunksize) % regionChunkSize;
        var reg = heremapchunk.MapRegion;
        var step = (float)heremapchunk.MapRegion.OreMapVerticalDistortBottom.InnerSize / regionChunkSize;
        return reg.OreMapVerticalDistortBottom.GetIntLerpedCorrectly(rdx * step + step * ((float)lx / chunksize), rdz * step + step * ((float)lz / chunksize));
    }

    public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldgenRandom, BlockPatchAttributes attributes = null)
    {
        var mapChunk = blockAccessor.GetMapChunk(pos.X / chunksize, pos.Z / chunksize);
        var posX = pos.X % chunksize;
        var posZ = pos.Z % chunksize;
        var extraDistX = (int)GetDepositDistortTop(pos, posX, posZ, mapChunk) / 7;
        var extraDistZ = (int)GetDepositDistortBot(pos, posX, posZ, mapChunk) / 7;
        rand.InitPositionSeed(pos.X / 100 + extraDistX, pos.Z / 100 + extraDistZ);

        var beloPos = pos.DownCopy();
        var blockBelow = blockAccessor.GetBlock(beloPos);
        for (int i = 0; i < rockStrata.Variants.Length; i++)
        {
            if (rockStrata.Variants[i].RockGroup == EnumRockGroup.Sedimentary && rockStrata.Variants[i].BlockCode == blockBelow.Code)
            {
                if (rand.NextDouble() > 0.6)
                {
                    blockAccessor.SetBlock(clay.BlockId, beloPos);
                }
                break;
            }
        }
        blockAccessor.SetBlock(BlockId, pos);

        return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldgenRandom, attributes);
    }
}
