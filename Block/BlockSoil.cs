using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Handles grass growth on top of soil via random server ticks. Grass will grow from none->verysparse->sparse->normal.
    /// It only grows on soil that has a sun light level of 7 or higher when there is an adjacent grass block near it. The
    /// adjacent grass block can be at a y level between 1 below it to 1 above it. 
    /// </summary>
    public class BlockSoil : Block
    {
        /// <summary>
        /// Data structure for easy access to BlockLayers. 
        /// </summary>
        private class BlockLayers
        {
            private Dictionary<AssetLocation, BlockLayer> layers = new Dictionary<AssetLocation, BlockLayer>();
            private BlockLayer blockLayer;
            private BlockLayerConfig blockLayerConfig;

            public BlockLayers(IWorldAccessor world, string blockLayerId)
            {
                blockLayerConfig = BlockLayerConfig.GetInstance((ICoreServerAPI)world.Api);
                blockLayer = blockLayerConfig.GetBlockLayerById(world, blockLayerId);
                InitBlockLayers(world, blockLayer);
            }

            public BlockLayer GetBlockLayerForNextGrowthStage(IWorldAccessor world, AssetLocation growthStage)
            {
                BlockLayer result;
                layers.TryGetValue(growthStage, out result);
                return result ?? blockLayer;
            }

            private void InitBlockLayers(IWorldAccessor world, BlockLayer parentBlockLayer)
            {
                foreach (BlockLayerCodeByMin blockLayerCodeByMin in parentBlockLayer.BlockCodeByMin)
                {
                    BlockLayer layer = new BlockLayer
                    {
                        MinFertility = blockLayerCodeByMin.MinFertility,
                        MaxFertility = blockLayerCodeByMin.MaxFertility,
                        MinRain = blockLayerCodeByMin.MinRain,
                        MaxRain = parentBlockLayer.MaxRain,
                        MinTemp = (int)blockLayerCodeByMin.MinTemp,
                        MaxTemp = parentBlockLayer.MaxTemp,
                        MaxY = blockLayerCodeByMin.MaxY,
                        BlockCode = blockLayerCodeByMin.BlockCode
                    };

                    layers.Add(blockLayerCodeByMin.BlockCode, layer);
                }
            }

        }

        internal class GrassTick
        {
            public Block Grass;
            public Block TallGrass;
        }

        private static readonly string blockLayersCacheKey = "BlockLayers";
        private static readonly Dictionary<string, string> growthStages = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> growthStagesReversed = new Dictionary<string, string>();
        private static readonly List<AssetLocation> tallGrass = new List<AssetLocation>();

        int growthLightLevel;
        string growthBlockLayer;
        float tallGrassGrowthProbability;

        static BlockSoil()
        {
            growthStages.Add("none", "verysparse");
            growthStages.Add("verysparse", "sparse");
            growthStages.Add("sparse", "normal");
            growthStages.Add("normal", "normal");//Just in case

            growthStagesReversed.Add("none", "none");//Just in case
            growthStagesReversed.Add("verysparse", "none");
            growthStagesReversed.Add("sparse", "verysparse");
            growthStagesReversed.Add("normal", "sparse");
            
            tallGrass.Add(new AssetLocation("tallgrass-veryshort"));
            tallGrass.Add(new AssetLocation("tallgrass-short"));
            tallGrass.Add(new AssetLocation("tallgrass-mediumshort"));
            tallGrass.Add(new AssetLocation("tallgrass-medium"));
            tallGrass.Add(new AssetLocation("tallgrass-tall"));
            tallGrass.Add(new AssetLocation("tallgrass-verytall"));
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            growthLightLevel = Attributes?["growthLightLevel"] != null ? Attributes["growthLightLevel"].AsInt(7) : 7;
            growthBlockLayer = Attributes?["growthBlockLayer"]?.AsString("l1soilwithgrass");
            tallGrassGrowthProbability = Attributes?["tallGrassGrowthProbability"] != null ? Attributes["tallGrassGrowthProbability"].AsFloat(0.3f) : 0.3f;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            base.OnServerGameTick(world, pos, extra);

            GrassTick tick = extra as GrassTick;
            world.BlockAccessor.SetBlock(tick.Grass.BlockId, pos);
            if (tick.TallGrass != null && world.BlockAccessor.GetBlock(pos.UpCopy()).BlockId == 0)
            {
                world.BlockAccessor.SetBlock(tick.TallGrass.BlockId, pos.UpCopy());
            }
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, out object extra)
        {
            extra = null;

            bool isGrowing = false;

            Block grass = null;
            BlockPos upPos = pos.UpCopy();
            string grasscoverage = LastCodePart();
            bool lowLightLevel = world.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.MaxLight) < growthLightLevel;
            if (lowLightLevel || isSmotheringBlock(world, upPos))
            {
                grass = tryGetBlockForDying(world);
            }
            else
            {
                isGrowing = true;
                grass = tryGetBlockForGrowing(world, pos);
            }

            if (grass != null)
            {
                extra = new GrassTick()
                {
                    Grass = grass,
                    TallGrass = isGrowing ? getTallGrassBlock(world, upPos) : null
                };
            }
            return extra != null;
        }

        private bool isSmotheringBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(pos);
            return block.SideSolid[BlockFacing.DOWN.Index] &&
                    block.SideOpaque[BlockFacing.DOWN.Index];
        }

        private Block tryGetBlockForGrowing(IWorldAccessor world, BlockPos pos)
        {
            string grasscoverage = LastCodePart();
            bool isFullyGrown = "normal".Equals(grasscoverage);
            if (isFullyGrown == false &&
                isAppropriateClimateToGrow(world, pos) &&
                isGrassNearby(world, pos))
            {
                return world.GetBlock(getNextGrowthStageCode());
            }
            return null;
        }

        private Block tryGetBlockForDying(IWorldAccessor world)
        {
            string grasscoverage = LastCodePart();
            bool isBarren = "none".Equals(grasscoverage);
            if (isBarren == false)
            {
                return world.GetBlock(getPreviousGrowthStageCode());
            }
            return null;
        }

        /// <summary>
        /// Gets the tallgrass block to be placed above soil. If tallgrass is already present
        /// then it will grow by either 1 or 2 stages. Returns null if none is to be placed
        /// or if it's already fully grown.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        private Block getTallGrassBlock(IWorldAccessor world, BlockPos pos)
        {
            if (world.Rand.NextDouble() > tallGrassGrowthProbability) return null;

            string nextGrassGrowthStage = getNextGrowthStage();
            if ("verysparse".Equals(nextGrassGrowthStage) || "sparse".Equals(nextGrassGrowthStage))
            {
                Block block = world.BlockAccessor.GetBlock(pos);
                if ("tallgrass".Equals(block.FirstCodePart()))//Tall grass already there, try growing it
                {
                    return tryGetGrownTallGrass(world, block.Code);
                }
                else
                {
                    return world.GetBlock(tallGrass[world.Rand.Next(0, 3)]);
                }
            }
            return null;
        }

        private Block tryGetGrownTallGrass(IWorldAccessor world, AssetLocation tallGrassCode)
        {
            int index = tallGrass.IndexOf(tallGrassCode) + world.Rand.Next(1, 3);
            if (index < tallGrass.Count)
            {
                return world.GetBlock(tallGrass[index]);
            }
            else//Growing by 2 is too much. Try growing by 1. 
            {
                index--;
                if (index < tallGrass.Count)
                {
                    return world.GetBlock(tallGrass[index]);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns true if grass can grow on this block at this location. The requirements for growth are
        /// as follows:
        /// * Soil is not fully grown
        /// * Light Level is greater than or equal to the value of the growthLightLevel Attribute
        /// * The BlockLayer associated with the next growth stage has climate conditions that match the current climate
        /// * The block above this soil block is solid
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns>true if grass can grow on this block at this location, false otherwise</returns>
        private bool canGrassGrowHere(IWorldAccessor world, BlockPos pos)
        {
            string grasscoverage = LastCodePart();
            bool isFullyGrown = "normal".Equals(grasscoverage);

            if (!isFullyGrown &&
                world.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.MaxLight) >= growthLightLevel &&
                world.BlockAccessor.GetBlock(pos.UpCopy()).SideSolid[BlockFacing.DOWN.Index] == false)
            {
                return isAppropriateClimateToGrow(world, pos);
            }
            return false;
        }

        /// <summary>
        /// Compares the ClimateCondition at the given BlockPos with the requirements of the 
        /// BlockLayer associated with the next growth stage block.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns>True if the climate is appropriate for growth, false otherwise</returns>
        private bool isAppropriateClimateToGrow(IWorldAccessor world, BlockPos pos)
        {
            ICoreServerAPI api = (ICoreServerAPI)world.Api;
            int mapheight = api.WorldManager.MapSizeY;
            ClimateCondition climate = world.BlockAccessor.GetClimateAt(pos);

            AssetLocation newGrowthStage = getNextGrowthStageCode();
            BlockLayers layers = getBlockLayers(world);
            BlockLayer bl = layers.GetBlockLayerForNextGrowthStage(world, newGrowthStage);
            //Check climate conditions to see whether the soil can grow to the next stage
            return (
                    climate.Temperature >= bl.MinTemp && climate.Temperature <= bl.MaxTemp &&
                    climate.Rainfall >= bl.MinRain && climate.Rainfall <= bl.MaxRain &&
                    climate.Fertility >= bl.MinFertility && climate.Fertility <= bl.MaxFertility &&
                    (float)pos.Y / mapheight <= bl.MaxY
            );
        }

        private bool isGrassNearby(IWorldAccessor world, BlockPos pos)
        {
            BlockPos neighborPos = new BlockPos();
            for (int y = -1; y < 2; y++)
            {
                foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                {
                    neighborPos.Set(pos.X + facing.Normali.X, pos.Y + y, pos.Z + facing.Normali.Z);
                    Block neighbor = world.BlockAccessor.GetBlock(neighborPos);
                    if (grassCanSpreadFrom(neighbor))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the neighbor block can spread grass. It must be a soil block with some grass on it.
        /// </summary>
        /// <param name="neighbor"></param>
        /// <returns>true if the neighbor block can spread grass, false otherwise</returns>
        private bool grassCanSpreadFrom(Block neighbor)
        {
            string[] parts = neighbor.Code.Path.Split('-');
            if ("soil".Equals(parts[0]))
            {
                string grasscoverage = parts[parts.Length - 1];
                if (!"none".Equals(grasscoverage))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the AssetLocation of the next growth stage for this soil block
        /// </summary>
        /// <returns></returns>
        private AssetLocation getNextGrowthStageCode()
        {
            return CodeWithParts(getNextGrowthStage());
        }

        private string getNextGrowthStage()
        {
            string currentGrassGrowth = LastCodePart();
            return growthStages[currentGrassGrowth];
        }

        private AssetLocation getPreviousGrowthStageCode()
        {
            string currentGrassGrowth = LastCodePart();
            return CodeWithParts(growthStagesReversed[currentGrassGrowth]);
        }

        private BlockLayers getBlockLayers(IWorldAccessor world)
        {
            if (world.Api.ObjectCache.ContainsKey(blockLayersCacheKey))
            {
                return world.Api.ObjectCache[blockLayersCacheKey] as BlockLayers;
            }
            else
            {
                BlockLayers blockLayers = new BlockLayers(world, growthBlockLayer);
                world.Api.ObjectCache[blockLayersCacheKey] = blockLayers;
                return blockLayers;
            }
        }
    }
}
