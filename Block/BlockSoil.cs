using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

#nullable disable

namespace Vintagestory.GameContent
{
    internal class GrassTick
    {
        public Block Grass;
        public Block TallGrass;
    }

    /// <summary>
    /// Handles grass growth on top of soil via random server ticks. Grass will grow from none->verysparse->sparse->normal.
    /// It only grows on soil that has a sun light level of 7 or higher when there is an adjacent grass block near it. The
    /// adjacent grass block can be at a y level between 1 below it to 1 above it. 
    /// </summary>
    public class BlockSoil : BlockWithGrassOverlay, IBlockSoil
    {
        protected List<AssetLocation> tallGrassCodes = new List<AssetLocation>();
        protected string[] growthStages = new string[] { "none", "verysparse", "sparse", "normal" };
        protected string[] tallGrassGrowthStages = new string[] { "veryshort", "short", "mediumshort", "medium", "tall", "verytall" };

        protected int growthLightLevel;
        protected string growthBlockLayer;
        protected float tallGrassGrowthChance;
        protected BlockLayerConfig blocklayerconfig;
        protected const int chunksize = GlobalConstants.ChunkSize;

        protected float growthChanceOnTick = 0.16f;

        public bool growOnlyWhereRainfallExposed = false;

        protected virtual int MaxStage => 3;
        GenBlockLayers genBlockLayers;

        private const int FullyGrownStage = 3;
        int GrowthStage(string stage)
        {
            if (stage == "normal") return FullyGrownStage;
            if (stage == "sparse") return 2;
            if (stage == "verysparse") return 1;
            return 0;
        }

        protected int currentStage;



        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            growthLightLevel = Attributes?["growthLightLevel"] != null ? Attributes["growthLightLevel"].AsInt(7) : 7;
            growthBlockLayer = Attributes?["growthBlockLayer"]?.AsString("l1soilwithgrass");
            tallGrassGrowthChance = Attributes?["tallGrassGrowthChance"] != null ? Attributes["tallGrassGrowthChance"].AsFloat(0.3f) : 0.3f;
            growthChanceOnTick = Attributes?["growthChanceOnTick"] != null ? Attributes["growthChanceOnTick"].AsFloat(0.33f) : 0.33f;
            growOnlyWhereRainfallExposed = Attributes?["growOnlyWhereRainfallExposed"] != null ? Attributes["growOnlyWhereRainfallExposed"].AsBool(false) : false;

            tallGrassCodes.Add(new AssetLocation("tallgrass-veryshort-free"));
            tallGrassCodes.Add(new AssetLocation("tallgrass-short-free"));
            tallGrassCodes.Add(new AssetLocation("tallgrass-mediumshort-free"));
            tallGrassCodes.Add(new AssetLocation("tallgrass-medium-free"));
            tallGrassCodes.Add(new AssetLocation("tallgrass-tall-free"));
            tallGrassCodes.Add(new AssetLocation("tallgrass-verytall-free"));
            
            if (api.Side == EnumAppSide.Server)
            {
                (api as ICoreServerAPI).Event.ServerRunPhase(EnumServerRunPhase.RunGame, () =>
                {
                    genBlockLayers = api.ModLoader.GetModSystem<GenBlockLayers>();
                    blocklayerconfig = genBlockLayers.blockLayerConfig;
                });
            }

            currentStage = GrowthStage(Variant["grasscoverage"]);
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            base.OnServerGameTick(world, pos, extra);

            GrassTick tick = extra as GrassTick;
            world.BlockAccessor.ExchangeBlock(tick.Grass.BlockId, pos);
            BlockPos upPos = pos.UpCopy();

            if (tick.TallGrass != null && world.BlockAccessor.GetBlock(upPos).BlockId == 0)
            {
                world.BlockAccessor.SetBlock(tick.TallGrass.BlockId, upPos);
            }
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;

            if (offThreadRandom.NextDouble() > growthChanceOnTick) return false;

            if (growOnlyWhereRainfallExposed && world.BlockAccessor.GetRainMapHeightAt(pos) > pos.Y + 1)
            {
                return false;
            }

            bool isGrowing = false;

            Block grass = null;
            BlockPos upPos = pos.UpCopy();
            
            bool lowLightLevel = world.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.MaxLight) < growthLightLevel && (world.BlockAccessor.GetLightLevel(upPos, EnumLightLevelType.MaxLight) < growthLightLevel || world.BlockAccessor.GetBlock(upPos).SideSolid[BlockFacing.DOWN.Index]);
            bool smothering = isSmotheringBlock(world, upPos);

            int overheatingAmount = 0;
            world.BlockAccessor.WalkBlocks(pos.AddCopy(-3, 0, -3), pos.AddCopy(3, 1, 3), (block, x, y, z) =>
            {
                if (block.Attributes == null) return;
                overheatingAmount = Math.Max(overheatingAmount, (block.Attributes["killPlantRadius"].AsInt(0) - Math.Max(0, (int)pos.DistanceTo(x, y, z)-1)));
            });

            bool die =
                (overheatingAmount >= 1 && currentStage == 3) ||
                (overheatingAmount >= 2 && currentStage == 2) ||
                (overheatingAmount >= 3 && currentStage == 1)
            ;

            
            if ((lowLightLevel || smothering || die) && currentStage > 0)
            {
                grass = tryGetBlockForDying(world);
            }
            else
            {
                if (overheatingAmount <= 0 && !smothering && !lowLightLevel && currentStage < MaxStage)
                {
                    isGrowing = true;
                    grass = tryGetBlockForGrowing(world, pos);
                }
            }

            if (grass != null)
            {
                extra = new GrassTick()
                {
                    Grass = grass,
                    TallGrass = isGrowing ? getTallGrassBlock(world, upPos, offThreadRandom) : null
                };
            }
            return extra != null;
        }

        protected bool isSmotheringBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);

            if (block is BlockLakeIce || block.LiquidLevel > 1) return true;
            block = world.BlockAccessor.GetBlock(pos);
            return block.SideSolid[BlockFacing.DOWN.Index] && block.SideOpaque[BlockFacing.DOWN.Index] || block is BlockLava;
        }

        protected Block tryGetBlockForGrowing(IWorldAccessor world, BlockPos pos)
        {
            int targetStage;

            ClimateCondition conds = GetClimateAt(world.BlockAccessor, pos);

            if (currentStage != MaxStage && (targetStage = getClimateSuitedGrowthStage(world, pos, conds)) != currentStage)
            {
                int nextStage = GameMath.Clamp(targetStage, currentStage - 1, currentStage + 1);

                return world.GetBlock(CodeWithParts(growthStages[nextStage]));
            }

            return null;
        }

        private ClimateCondition GetClimateAt(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (genBlockLayers == null)
            {
                return blockAccessor.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues);
            }
            else
            {
                // Some randomness stuff to hide straight lines in the climate transition system resulting from using lerp on a low resolution map
                int rndY = genBlockLayers.RandomlyAdjustPosition(pos, out double rndX, out double rndZ);
                int distx = (int)(Math.Round(rndX, 0));
                int distz = (int)(Math.Round(rndZ, 0));
                pos.Add(distx, rndY, distz);
                var conds = blockAccessor.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues);
                pos.Add(-distx, -rndY, -distz);
                return conds;
            }
        }

        protected Block tryGetBlockForDying(IWorldAccessor world)
        {
            int nextStage = Math.Max(currentStage - 1, 0);
            if (nextStage != currentStage)
            {
                return world.GetBlock(CodeWithParts(growthStages[nextStage]));
            }
            
            return null;
        }

        /// <summary>
        /// Gets the tallgrass block to be placed above soil. If tallgrass is already present
        /// then it will grow by either 1 or 2 stages. Returns null if none is to be placed
        /// or if it's already fully grown.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="abovePos"></param>
        /// <returns></returns>
        protected Block getTallGrassBlock(IWorldAccessor world, BlockPos abovePos, Random offthreadRandom)
        {
            if (offthreadRandom.NextDouble() > tallGrassGrowthChance) return null;
            Block block = world.BlockAccessor.GetBlock(abovePos);

            int curTallgrassStage = (block.FirstCodePart() == "tallgrass") ? Array.IndexOf(tallGrassGrowthStages, block.Variant["tallgrass"]) : 0;

            int nextTallgrassStage = Math.Min(curTallgrassStage + 1 + offthreadRandom.Next(3), tallGrassGrowthStages.Length - 1);

            return world.GetBlock(tallGrassCodes[nextTallgrassStage]);
        }


        /// <summary>
        /// Returns true if grass can grow on this block at this location. The requirements for growth are
        /// as follows:
        /// * Soil is not fully grown
        /// * Light Level is greater than or equal to the value of the growthLightLevel Attribute
        /// * The BlockLayer associated with the next growth stage has climate conditions that match the current climate
        /// * The block above this soil block is not solid
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns>true if grass can grow on this block at this location, false otherwise</returns>
        protected bool canGrassGrowHere(IWorldAccessor world, BlockPos pos)
        {
            bool isFullyGrown = currentStage == FullyGrownStage;

            if (!isFullyGrown &&
                world.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.MaxLight) >= growthLightLevel &&
                world.BlockAccessor.IsSideSolid(pos.X, pos.Y + 1, pos.Z, BlockFacing.DOWN) == false)
            {
                return getClimateSuitedGrowthStage(world, pos, GetClimateAt(world.BlockAccessor, pos)) != currentStage;
            }
            return false;
        }



        protected int getClimateSuitedGrowthStage(IWorldAccessor world, BlockPos pos, ClimateCondition climate)
        {
            if (climate == null) return currentStage;  // Can occasionally be null, e.g. during running /wgen regen command

            IMapChunk mapchunk = world.BlockAccessor.GetMapChunkAtBlockPos(pos);
            if (mapchunk == null) return 0;

            ICoreServerAPI api = (ICoreServerAPI)world.Api;
            int mapheight = api.WorldManager.MapSizeY;
            float transitionSize = blocklayerconfig.blockLayerTransitionSize;
            int topblockid = mapchunk.TopRockIdMap[(pos.Z % chunksize) * chunksize + (pos.X % chunksize)];

            double posRand = (double)GameMath.MurmurHash3(pos.X, 1, pos.Z) / int.MaxValue;
            posRand = (posRand + 1) * transitionSize;

            int posY = pos.Y + (int)(genBlockLayers.distort2dx.Noise(-pos.X, -pos.Z) / 4.0);

            for (int j = 0; j < blocklayerconfig.Blocklayers.Length; j++)
            {
                BlockLayer bl = blocklayerconfig.Blocklayers[j];
                float trfDist = bl.CalcTrfDistance(climate.Temperature, climate.WorldgenRainfall, climate.Fertility);
                float yDist = bl.CalcYDistance(posY, mapheight);

                if (trfDist + yDist <= posRand)
                {
                    int blockId = bl.GetBlockId(posRand, climate.Temperature, climate.WorldgenRainfall, climate.Fertility, topblockid, pos, mapheight, climate.Biome);

                    Block block = world.Blocks[blockId];
                    if (block is BlockSoil blockSoil)
                    {
                        return blockSoil.currentStage;
                    }
                }
            }

            return 0;
        }



        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            return base.GetColor(capi, pos);
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            if (facing == BlockFacing.UP && Variant["grasscoverage"] != "none")
            {
                return capi.World.ApplyColorMapOnRgba(ClimateColorMap, SeasonColorMap, capi.BlockTextureAtlas.GetRandomColor(Textures["specialSecondTexture"].Baked.TextureSubId, rndIndex), pos.X, pos.Y, pos.Z);
            }
            return base.GetRandomColor(capi, pos, facing, rndIndex);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (Variant.ContainsKey("fertility"))
            {
                var fertility = inSlot.Itemstack.Block.Variant["fertility"];
                var farmland = world.GetBlock(new AssetLocation("farmland-dry-" + fertility));
                if (farmland == null) return;
                var fert_value = farmland.Fertility;
                if (fert_value <= 0) return;
                dsc.Append(Lang.Get("Fertility when tilled:") + " " + fert_value + "%\n");
            }
        }

    }
}
