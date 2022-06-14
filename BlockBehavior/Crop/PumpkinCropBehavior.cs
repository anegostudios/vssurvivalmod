using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Controls pumpkin crop(motherplant) growth. 
    /// </summary>
    public class PumpkinCropBehavior : CropBehavior
    {
        //Minimum stage at which vines can grow
        private int vineGrowthStage = 3;

        //Probability of vine growth once the minimum vine growth stage is reached
        private float vineGrowthQuantity;

        private AssetLocation vineBlockLocation;
        NatFloat vineGrowthQuantityGen;

        public PumpkinCropBehavior(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            vineGrowthStage = properties["vineGrowthStage"].AsInt();
            vineGrowthQuantityGen = properties["vineGrowthQuantity"].AsObject<NatFloat>();
            vineBlockLocation = new AssetLocation("pumpkin-vine-1-normal");
        }

        public override void OnPlanted(ICoreAPI api)
        {
            vineGrowthQuantity = vineGrowthQuantityGen.nextFloat(1, api.World.Rand);
        }
        
        public override bool TryGrowCrop(ICoreAPI api, IFarmlandBlockEntity farmland, double currentTotalHours, int newGrowthStage, ref EnumHandling handling)
        {
            if (vineGrowthQuantity == 0)
            {
                vineGrowthQuantity = farmland.CropAttributes.GetFloat("vineGrowthQuantity", vineGrowthQuantityGen.nextFloat(1, api.World.Rand));
                farmland.CropAttributes.SetFloat("vineGrowthQuantity", vineGrowthQuantity);
            }

            handling = EnumHandling.PassThrough;

            if (newGrowthStage >= vineGrowthStage)
            {
                if (newGrowthStage == 8)
                {
                    bool allWithered = true;
                    foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                    {
                        Block block = api.World.BlockAccessor.GetBlock(farmland.Pos.AddCopy(facing).Up());
                        if (block.Code.Path.StartsWith("pumpkin-vine"))
                        {
                            allWithered &= block.LastCodePart() == "withered";    
                        }
                    }

                    if (!allWithered)
                    {
                        handling = EnumHandling.PreventDefault;
                    }
                    return false;
                }

                if (api.World.Rand.NextDouble() < vineGrowthQuantity)
                {
                    return TrySpawnVine(api, farmland, currentTotalHours);
                }
            }

            return false;
        }

        private bool TrySpawnVine(ICoreAPI api, IFarmlandBlockEntity farmland, double currentTotalHours)
        {
            BlockPos motherplantPos = farmland.UpPos;
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                BlockPos candidatePos = motherplantPos.AddCopy(facing);
                Block block = api.World.BlockAccessor.GetBlock(candidatePos);
                if (CanReplace(block))
                {
                    if(CanSupportPumpkin(api, candidatePos.DownCopy()))
                    {
                        DoSpawnVine(api, candidatePos, motherplantPos, facing, currentTotalHours);
                        return true;
                    }
                }
            }

            return false;
        }

        private void DoSpawnVine(ICoreAPI api, BlockPos vinePos, BlockPos motherplantPos, BlockFacing facing, double currentTotalHours)
        {
            Block vineBlock = api.World.GetBlock(vineBlockLocation);
            api.World.BlockAccessor.SetBlock(vineBlock.BlockId, vinePos);

            if (api.World is IServerWorldAccessor)
            {
                BlockEntity be = api.World.BlockAccessor.GetBlockEntity(vinePos);
                if (be is BlockEntityPumpkinVine)
                {
                    ((BlockEntityPumpkinVine)be).CreatedFromParent(motherplantPos, facing, currentTotalHours);
                }
            }
        }

        private bool CanReplace(Block block)
        {
            if(block == null)
            {
                return true;
            }
            return block.Replaceable >= 6000 && !block.Code.GetName().Contains("pumpkin");
        }

        public static bool CanSupportPumpkin(ICoreAPI api, BlockPos pos)
        {
            Block underblock = api.World.BlockAccessor.GetLiquidBlock(pos);
            if (underblock.IsLiquid()) return false;
            underblock = api.World.BlockAccessor.GetBlock(pos);
            return underblock.Replaceable <= 5000;
        }
    }
}
