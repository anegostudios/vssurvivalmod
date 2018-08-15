using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    // Definitions:
    // Farmland has 3 nutrient levels N, P and K
    // - Each nutrient level has a range of 0-100
    // - Every crop always only consumes one nutrient level (for now)
    // - Some crops require more nutrient than others
    // - Each crop has different total growth speed
    // - Each crop can have any amounts of growth stages
    // - Some crops can be harvested with right click, without destroying the crop
    public class BlockEntityFarmland : BlockEntity, IFarmlandBlockEntity
    {
        static Random rand = new Random();
        static AssetLocation[] tallGrassNames = new AssetLocation[] { new AssetLocation("tallgrass-veryshort"), new AssetLocation("tallgrass-short"), new AssetLocation("tallgrass-mediumshort"), new AssetLocation("tallgrass-medium") };
        static OrderedDictionary<string, float> Fertilities = new OrderedDictionary<string, float>{
            { "verylow", 5 },
            { "low", 25 },
            { "medium", 50 },
            { "high", 75 },
        };


        BlockPos upPos;
        long growListenerId;
        // Total game hours from where on it can enter the next growth stage
        double totalHoursForNextStage;
        // The last time fertility increase was checked
        double totalHoursFertilityCheck;

        // Stored values
        float[] nutrients = new float[3];
        bool isWatered = false;
        int originalFertility; // The fertility the soil will recover to (the soil from which the farmland was made of)
        TreeAttribute cropAttrs = new TreeAttribute();

        int delayGrowthBelowSunLight = 19;
        float lossPerLevel = 0.1f; 
            
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            upPos = base.pos.UpCopy();

            if (api is ICoreServerAPI)
            {
                if (HasUnripeCrop())
                {
                    growListenerId = RegisterGameTickListener(CheckGrow, 2000);
                }

                RegisterGameTickListener(SlowTick, 15000);
            }

            Block block = api.World.BlockAccessor.GetBlock(base.pos);
            if (block.Attributes != null)
            {
                delayGrowthBelowSunLight = block.Attributes["delayGrowthBelowSunLight"].AsInt(19);
                lossPerLevel = block.Attributes["lossPerLevel"].AsFloat(0.1f);
            }
        }


        internal void CreatedFromSoil(Block block)
        {
            string fertility = block.LastCodePart(1);
            originalFertility = (int)Fertilities[fertility];

            nutrients[0] = originalFertility;
            nutrients[1] = originalFertility;
            nutrients[2] = originalFertility;

            totalHoursFertilityCheck = api.World.Calendar.TotalHours;
        }


        private void SlowTick(float dt)
        {
            // 1. Watered check
            bool newWatered = false;

            for (int dx = -3; dx <= 3 && !newWatered; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (api.World.BlockAccessor.GetBlock(base.pos.X + dx, base.pos.Y, base.pos.Z + dz).IsWater())
                    {
                        newWatered = true;
                        break;
                    }
                }
            }

            if (newWatered != isWatered)
            {
                Block block = api.World.BlockAccessor.GetBlock(base.pos);
                if (block.BlockMaterial == EnumBlockMaterial.Air)
                {
                    // Block must have been removed already. This code should not get excecuted in that case but it does. I don't know why yet.
                    // OnBlockRemoved should have removed the game tick listener during removal.
                    api.World.BlockAccessor.RemoveBlockEntity(base.pos);
                    return;
                }

                AssetLocation newCode = block.CodeWithParts(newWatered ? "moist" : "dry", block.LastCodePart());
                Block nextBlock = api.World.GetBlock(newCode);
                if (nextBlock == null)
                {
                    api.World.BlockAccessor.RemoveBlockEntity(base.pos);
                    return;
                }

                api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, base.pos);

                isWatered = newWatered;

                api.World.BlockAccessor.MarkBlockEntityDirty(base.pos);
                api.World.BlockAccessor.MarkBlockDirty(base.pos);

                if (isWatered && HasUnripeCrop() && growListenerId == 0)
                {
                    growListenerId = RegisterGameTickListener(CheckGrow, 2000);
                }
            }

            // 2. Increase fertility when fallow
            if (!isWatered)
            {
                totalHoursFertilityCheck = api.World.Calendar.TotalHours;
                return;
            }

            

            Block upblock = api.World.BlockAccessor.GetBlock(upPos);
            if (upblock == null) return;

            double hoursPassed = api.World.Calendar.TotalHours - totalHoursFertilityCheck;

            // Let's increase fertility every 2 - 3 game hours
            double hoursRequired = (2 + rand.NextDouble());
            int fertilityGained = 0;
            bool growTallGrass = false;
            while (hoursPassed > hoursRequired)
            {
                fertilityGained++;
                hoursPassed -= hoursRequired;
                totalHoursFertilityCheck += hoursRequired;

                hoursRequired = (2 + rand.NextDouble());
                growTallGrass |= rand.NextDouble() < 0.006;
            }

            if (fertilityGained > 0 && upblock.BlockMaterial == EnumBlockMaterial.Air && growTallGrass)
            {
                Block weedsBlock = api.World.GetBlock(tallGrassNames[rand.Next(tallGrassNames.Length)]);
                if (weedsBlock != null)
                {
                    api.World.BlockAccessor.SetBlock(weedsBlock.BlockId, upPos);
                }
            }

            if (fertilityGained > 0 && (nutrients[0] < originalFertility || nutrients[1] < originalFertility || nutrients[2] < originalFertility))
            {
                EnumSoilNutrient? currentlyConsumedNutrient = null;
                if (upblock.CropProps != null)
                {
                    currentlyConsumedNutrient = upblock.CropProps.RequiredNutrient;
                    fertilityGained /= 3;
                    if (HasRipeCrop()) fertilityGained = 0;
                }

                if (currentlyConsumedNutrient != EnumSoilNutrient.N) nutrients[0] = Math.Min(originalFertility, nutrients[0] + fertilityGained);
                if (currentlyConsumedNutrient != EnumSoilNutrient.P) nutrients[1] = Math.Min(originalFertility, nutrients[1] + fertilityGained);
                if (currentlyConsumedNutrient != EnumSoilNutrient.K) nutrients[2] = Math.Min(originalFertility, nutrients[2] + fertilityGained);

                api.World.BlockAccessor.MarkBlockEntityDirty(base.pos);
            }


        }

        private void CheckGrow(float dt)
        {
            double hoursNextStage = GetHoursForNextStage();

            if (!isWatered)
            {
                // Delay growth by the passed time
                totalHoursForNextStage = api.World.Calendar.TotalHours + hoursNextStage;
                return;
            }

            // Slow down growth on bad light levels
            int sunlight = api.World.BlockAccessor.GetLightLevel(base.pos.UpCopy(), EnumLightLevelType.MaxLight);
            double lightGrowthSpeedFactor = GameMath.Clamp(1 - (delayGrowthBelowSunLight - sunlight) * lossPerLevel, 0, 1);

            if (lightGrowthSpeedFactor <= 0)
            {
                return;
            }

            double lightHoursPenalty = hoursNextStage / lightGrowthSpeedFactor - hoursNextStage;


            while (api.World.Calendar.TotalHours > totalHoursForNextStage + lightHoursPenalty)
            {
                TryGrowCrop(totalHoursForNextStage);
                totalHoursForNextStage += hoursNextStage;
            }
            
            if (HasRipeCrop())
            {
                api.Event.UnregisterGameTickListener(growListenerId);
                growListenerId = 0;
            }
        }

        public double GetHoursForNextStage()
        {
            Block block = GetCrop();
            if (block == null) return 99999999;

            float stageHours = 24 * block.CropProps.TotalGrowthDays / block.CropProps.GrowthStages;

            stageHours *= LowNutrientPenalty(block.CropProps.RequiredNutrient);

            // Add a bit random to it (+/- 10%)
            stageHours *= (float)(0.9 + 0.2 * rand.NextDouble());

            
            return stageHours;
        }

        public float LowNutrientPenalty(EnumSoilNutrient nutrient)
        {
            if (nutrients[(int)nutrient] > 75) return 1/1.1f;
            if (nutrients[(int)nutrient] > 50) return 1;
            if (nutrients[(int)nutrient] > 35) return 1/0.9f;
            if (nutrients[(int)nutrient] > 20) return 1/0.6f;
            if (nutrients[(int)nutrient] > 5) return 1/0.3f;
            return 1/0.1f;
        }

        public float DeathChance(int nutrientIndex)
        {
            if (nutrients[nutrientIndex] <= 5) return 0.5f;
            return 0f;
        }

        internal bool TryPlant(Block block)
        {
            if (CanPlant() && block.CropProps != null)
            {
                api.World.BlockAccessor.SetBlock(block.BlockId, upPos);
                totalHoursForNextStage = api.World.Calendar.TotalHours + GetHoursForNextStage();

                if (HasUnripeCrop() && api is ICoreServerAPI && growListenerId == 0)
                {
                    growListenerId = RegisterGameTickListener(CheckGrow, 2000);
                }

                foreach (CropBehavior behavior in block.CropProps.Behaviors)
                {
                    behavior.OnPlanted(api);
                }

                return true;
            }

            return false;
        }

        internal bool CanPlant()
        {
            Block block = api.World.BlockAccessor.GetBlock(upPos);
            return block == null || block.BlockMaterial == EnumBlockMaterial.Air;
        }

        internal bool HasUnripeCrop()
        {
            Block block = GetCrop();
            return block != null && CropStage(block) < block.CropProps.GrowthStages;
        }

        internal bool HasRipeCrop()
        {
            Block block = GetCrop();
            return block != null && CropStage(block) >= block.CropProps.GrowthStages;
        }

        internal bool TryGrowCrop(double currentTotalHours)
        {
            Block block = GetCrop();
            if (block == null) return false;

            int currentGrowthStage = CropStage(block);
            if (currentGrowthStage < block.CropProps.GrowthStages)
            {
                int newGrowthStage = currentGrowthStage + 1;

                Block nextBlock = api.World.GetBlock(block.CodeWithParts("" + newGrowthStage));
                if (nextBlock == null) return false;

                if (block.CropProps.Behaviors != null)
                {
                    EnumHandling handled = EnumHandling.NotHandled;
                    bool result = false;
                    foreach (CropBehavior behavior in block.CropProps.Behaviors)
                    {
                        result = behavior.TryGrowCrop(api, this, currentTotalHours, newGrowthStage, ref handled);
                        if (handled == EnumHandling.Last) return result;
                    }
                    if (handled == EnumHandling.PreventDefault) return result;
                }

                api.World.BlockAccessor.SetBlock(nextBlock.BlockId, upPos);
                ConsumeNutrients(block);
                return true;
            }
            return false;
        }

        private void ConsumeNutrients(Block block)
        {
            int prevLevel = FertilityLevel((nutrients[0] + nutrients[1] + nutrients[2]) / 3);

            float nutrientLoss = block.CropProps.NutrientConsumption / block.CropProps.GrowthStages;
            nutrients[(int)block.CropProps.RequiredNutrient] = Math.Max(0, nutrients[(int)block.CropProps.RequiredNutrient] - nutrientLoss);

            int nowLevel = FertilityLevel((nutrients[0] + nutrients[1] + nutrients[2]) / 3);

            if (prevLevel != nowLevel)
            {
                Block farmlandBlock = api.World.BlockAccessor.GetBlock(base.pos);
                Block nextFarmlandBlock = api.World.GetBlock(farmlandBlock.CodeWithParts(Fertilities.GetKeyAtIndex(nowLevel)));
                api.World.BlockAccessor.ExchangeBlock(nextFarmlandBlock.BlockId, base.pos);
            }

            api.World.BlockAccessor.MarkBlockEntityDirty(base.pos);
            api.World.BlockAccessor.MarkBlockDirty(base.pos);
        }


        int FertilityLevel(float fertiltyValue)
        {
            int i = 0;
            foreach (var val in Fertilities) {
                if (val.Value > fertiltyValue) return i;
                i++;
            }
            return 3;
        }

        internal Block GetCrop()
        {
            Block block = api.World.BlockAccessor.GetBlock(upPos);
            if (block == null || block.CropProps == null) return null;
            return block;
        }


        internal int CropStage(Block block)
        {
            int stage = 0;
            int.TryParse(block.LastCodePart(), out stage);
            return stage;
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            nutrients[0] = tree.GetFloat("n");
            nutrients[1] = tree.GetFloat("p");
            nutrients[2] = tree.GetFloat("k");
            isWatered = tree.GetInt("isWatered") > 0;
            originalFertility = tree.GetInt("originalFertility");

            if (tree.HasAttribute("totalHoursForNextStage"))
            {
                totalHoursForNextStage = tree.GetDouble("totalHoursForNextStage");
                totalHoursFertilityCheck = tree.GetDouble("totalHoursFertilityCheck");
            } else
            {
                // Pre v1.5.1
                totalHoursForNextStage = tree.GetDouble("totalDaysForNextStage") * 24;
                totalHoursFertilityCheck = tree.GetDouble("totalDaysFertilityCheck") * 24;
            }

            TreeAttribute cropAttrs = tree["cropAttrs"] as TreeAttribute;
            if (cropAttrs == null) cropAttrs = new TreeAttribute();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("n", nutrients[0]);
            tree.SetFloat("p", nutrients[1]);
            tree.SetFloat("k", nutrients[2]);
            tree.SetInt("isWatered", isWatered ? 1 : 0);
            tree.SetInt("originalFertility", originalFertility);
            tree.SetDouble("totalHoursForNextStage", totalHoursForNextStage);
            tree.SetDouble("totalHoursFertilityCheck", totalHoursFertilityCheck);
            tree["cropAttrs"] = cropAttrs;
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            return
                "Nutrient Levels: N " + (int)nutrients[0] + "%, P " + (int)nutrients[1] + "%, K " + (int)nutrients[2] + "%\n" +
                "Growth Speeds: N " + Math.Round(100*1/LowNutrientPenalty(EnumSoilNutrient.N),0) + "%, P " + Math.Round(100*1/LowNutrientPenalty(EnumSoilNutrient.P),0) + "%, K " + Math.Round(100*1/LowNutrientPenalty(EnumSoilNutrient.K),0) + "%\n"
            ;
        }

        public double TotalHoursForNextStage
        {
            get
            {
                return totalHoursForNextStage;
            }
        }

        public double TotalHoursFertilityCheck
        {
            get
            {
                return totalHoursFertilityCheck;
            }
        }

        public float[] Nutrients
        {
            get
            {
                return nutrients;
            }
        }

        public bool IsWatered
        {
            get
            {
                return isWatered;
            }
        }

        public int OriginalFertility
        {
            get
            {
                return originalFertility;
            }
        }

        public BlockPos Pos
        {
            get
            {
                return base.pos;
            }
        }

        public BlockPos UpPos
        {
            get
            {
                return upPos;
            }
        }

        public ITreeAttribute CropAttributes
        {
            get
            {
                return cropAttrs;
            }
        }
    }
}
