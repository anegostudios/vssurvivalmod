using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    // Hoed soil for simple crop block that do not track their own state
    // Tracks crop growth, damage from enviromental factors, its drops and animal POI handling.
    public class BlockEntityFarmland : BlockEntitySoilNutrition, IFarmlandBlockEntity, IAnimalFoodSource
    {
        protected bool unripeCropColdDamaged;
        protected bool unripeHeatDamaged;
        protected bool ripeCropColdDamaged;
        protected Block deadCropBlock;
        protected bool allowcropDeath;

        // Total game hours from where on it can enter the next growth stage
        protected double totalHoursForNextStage;

        protected TreeAttribute cropAttrs = new TreeAttribute();

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreServerAPI)
            {
                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
            }

            deadCropBlock = Api.World.GetBlock(new AssetLocation("deadcrop"));
            allowcropDeath = Api.World.Config.GetBool("allowCropDeath", true);
        }

        public override void OnCropBlockBroken()
        {
            ripeCropColdDamaged = false;
            unripeCropColdDamaged = false;
            unripeHeatDamaged = false;
            base.OnCropBlockBroken();
        }

        protected override void beginIntervalledUpdate(out FarmlandFastForwardUpdate onInterval, out FarmlandUpdateEnd onEnd)
        {
            Block cropBlock = GetCrop();
            bool hasCrop = cropBlock != null;
            bool hasRipeCrop = HasRipeCrop();
            double hoursNextStage = GetHoursForNextStage();

            base.beginIntervalledUpdate(out onInterval, out onEnd);

            var prevOnInterval = onInterval;

            onInterval = (hourIntervall, conds, lightGrowthSpeedFactor, growthPaused) =>
            {
                prevOnInterval.Invoke(hourIntervall, conds, lightGrowthSpeedFactor, growthPaused);

                // Adjust for light level, ie 10% growth speed needs 90% of hourIntervall added back on to total growth time
                totalHoursForNextStage += hourIntervall * (1 - lightGrowthSpeedFactor);

                hasCrop = updateCropDamage(hourIntervall, cropBlock, hasCrop, hasRipeCrop, conds);

                if (growthPaused)
                {
                    totalHoursForNextStage += previousHourInterval; // Postpone crop growth for the same amount of time that has been suspended
                    return;
                }

                if (moistureLevel < 0.1)
                {
                    // Too dry to grow. Todo: Make it dependent on crop
                    return;
                }

                if (totalHoursLastUpdate >= totalHoursForNextStage)
                {
                    TryGrowCrop(totalHoursForNextStage);
                    hasRipeCrop = HasRipeCrop();
                    totalHoursForNextStage += hoursNextStage;

                    hoursNextStage = GetHoursForNextStage();
                }
            };
        }



        protected override void onRollback(double hoursrolledback)
        {
            base.onRollback(hoursrolledback);
            totalHoursForNextStage -= hoursrolledback;
        }

        protected override int RainHeightOffset => GetCrop() == null ? 0 : 1;
        protected override bool RecoverFertility => GetCrop() == null || !HasRipeCrop();


        private bool updateCropDamage(double hourIntervall, Block cropBlock, bool hasCrop, bool hasRipeCrop, ClimateCondition conds)
        {
            if (!hasCrop)
            {
                ripeCropColdDamaged = false;
                unripeCropColdDamaged = false;
                unripeHeatDamaged = false;
                for (int i = 0; i < damageAccum.Length; i++) damageAccum[i] = 0;
            }
            else
            {
                if (cropBlock?.CropProps != null && conds.Temperature < cropBlock.CropProps.ColdDamageBelow)
                {
                    if (hasRipeCrop)
                    {
                        ripeCropColdDamaged = true;
                    }
                    else
                    {
                        unripeCropColdDamaged = true;
                        damageAccum[(int)EnumCropStressType.TooCold] += (float)hourIntervall;
                    }
                }
                else
                {
                    damageAccum[(int)EnumCropStressType.TooCold] = Math.Max(0, damageAccum[(int)EnumCropStressType.TooCold] - (float)hourIntervall / 10);
                }

                if (cropBlock?.CropProps != null && conds.Temperature > cropBlock.CropProps.HeatDamageAbove && hasCrop)
                {
                    unripeHeatDamaged = true;
                    damageAccum[(int)EnumCropStressType.TooHot] += (float)hourIntervall;
                }
                else
                {
                    damageAccum[(int)EnumCropStressType.TooHot] = Math.Max(0, damageAccum[(int)EnumCropStressType.TooHot] - (float)hourIntervall / 10);
                }

                for (int i = 0; i < damageAccum.Length; i++)
                {
                    float dmg = damageAccum[i];
                    if (!allowcropDeath) dmg = damageAccum[i] = 0;

                    if (dmg > 48)
                    {
                        Api.World.BlockAccessor.SetBlock(deadCropBlock.Id, upPos);
                        var be = Api.World.BlockAccessor.GetBlockEntity(upPos) as BlockEntityDeadCrop;
                        be.Inventory[0].Itemstack = new ItemStack(cropBlock);
                        be.deathReason = (EnumCropStressType)i;
                        hasCrop = false;
                        break;
                    }
                }
            }

            return hasCrop;
        }


        public double GetHoursForNextStage()
        {
            Block block = GetCrop();
            if (block == null) return 99999999;

            var totalDays = block.CropProps.TotalGrowthDays;
            // Backwards compatibility, if days are provided we convert it to months using the default configuration timescale
            // After, we convert it to the currently configured timescale
            // For example, if something is set to grow in 6 days and the amount of days per month has been changed to 30, the new growth time will be 15 days.
            if (totalDays > 0)
            {
                var defaultTimeInMonths = totalDays / 12;
                totalDays = defaultTimeInMonths * Api.World.Calendar.DaysPerMonth;
            }
            else
            {
                totalDays = block.CropProps.TotalGrowthMonths * Api.World.Calendar.DaysPerMonth;
            }

            // There's one fewer update than growth stages, since we start on the first one already
            float stageHours = Api.World.Calendar.HoursPerDay * totalDays / Math.Max(1, block.CropProps.GrowthStages - 1);

            stageHours *= 1 / GetGrowthRate(block.CropProps.RequiredNutrient);

            // Add a bit random to it (+/- 10%)
            stageHours *= (float)(0.9 + 0.2 * rand.NextDouble());

            return stageHours / growthRateMul;
        }

        public bool TryPlant(Block block, ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel)
        {
            if (CanPlant() && block.CropProps != null)
            {
                Api.World.BlockAccessor.SetBlock(block.BlockId, upPos);
                totalHoursForNextStage = Api.World.Calendar.TotalHours + GetHoursForNextStage();

                foreach (CropBehavior behavior in block.CropProps.Behaviors)
                {
                    behavior.OnPlanted(Api, itemslot, byEntity, blockSel);
                }

                return true;
            }

            return false;
        }

        public bool CanPlant()
        {
            Block block = Api.World.BlockAccessor.GetBlock(upPos);
            return block == null || block.BlockMaterial == EnumBlockMaterial.Air;
        }

        public bool HasRipeCrop()
        {
            Block block = GetCrop();
            return block != null && GetCropStage(block) >= block.CropProps.GrowthStages;
        }

        public bool TryGrowCrop(double currentTotalHours)
        {
            Block block = GetCrop();
            if (block == null) return false;

            int currentGrowthStage = GetCropStage(block);
            if (currentGrowthStage < block.CropProps.GrowthStages)
            {
                int newGrowthStage = currentGrowthStage + 1;

                Block nextBlock = Api.World.GetBlock(block.CodeWithParts("" + newGrowthStage));
                if (nextBlock == null) return false;

                if (block.CropProps.Behaviors != null)
                {
                    EnumHandling handled = EnumHandling.PassThrough;
                    bool result = false;
                    foreach (CropBehavior behavior in block.CropProps.Behaviors)
                    {
                        result = behavior.TryGrowCrop(Api, this, currentTotalHours, newGrowthStage, ref handled);
                        if (handled == EnumHandling.PreventSubsequent) return result;
                    }
                    if (handled == EnumHandling.PreventDefault) return result;
                }

                if (Api.World.BlockAccessor.GetBlockEntity(upPos) == null)
                {
                    Api.World.BlockAccessor.SetBlock(nextBlock.BlockId, upPos);    //create any blockEntity if necessary (e.g. Bell Pepper and other fruiting crops)
                }
                else
                {
                    Api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, upPos);    //do not destroy existing blockEntity (e.g. Bell Pepper and other fruiting crops)
                }
                ConsumeNutrients(block);
                return true;
            }

            return false;
        }

        protected void ConsumeNutrients(Block cropBlock)
        {
            // There's one fewer update than growth stages, since we start on the first one already
            float nutrientLoss = cropBlock.CropProps.NutrientConsumption / Math.Max(1, cropBlock.CropProps.GrowthStages - 1);
            ConsumeNutrients(cropBlock.CropProps.RequiredNutrient, nutrientLoss);
        }



        public Block GetCrop()
        {
            Block block = Api.World.BlockAccessor.GetBlock(upPos);
            if (block == null || block.CropProps == null) return null;
            return block;
        }


        public int GetCropStage(Block block)
        {
            int.TryParse(block.LastCodePart(), out int stage);
            return stage;
        }


        public ItemStack[] GetDrops(ItemStack[] drops)
        {
            BlockEntityDeadCrop beDeadCrop = Api.World.BlockAccessor.GetBlockEntity(upPos) as BlockEntityDeadCrop;
            bool isDead = beDeadCrop != null;

            if (!ripeCropColdDamaged && !unripeCropColdDamaged && !unripeHeatDamaged && !isDead) return drops;
            if (!Api.World.Config.GetString("harshWinters").ToBool(true)) return drops;

            List<ItemStack> stacks = new List<ItemStack>();

            var cropProps = GetCrop()?.CropProps;
            if (cropProps == null) return drops;

            float mul = 1f;
            if (ripeCropColdDamaged) mul = cropProps.ColdDamageRipeMul;
            if (unripeHeatDamaged || unripeCropColdDamaged) mul = cropProps.DamageGrowthStuntMul;
            if (isDead) mul = beDeadCrop.deathReason == EnumCropStressType.Eaten ? 0 : Math.Max(cropProps.ColdDamageRipeMul, cropProps.DamageGrowthStuntMul);

            string[] debuffUnaffectedDrops = Block.Attributes?["debuffUnaffectedDrops"].AsArray<string>();

            for (int i = 0; i < drops.Length; i++)
            {
                ItemStack stack = drops[i];
                if (WildcardUtil.Match(debuffUnaffectedDrops, stack.Collectible.Code.ToShortString()))
                {
                    stacks.Add(stack);
                    continue;
                }

                float q = stack.StackSize * mul;
                float frac = q - (int)q;
                stack.StackSize = (int)q + (Api.World.Rand.NextDouble() > frac ? 1 : 0);

                if (stack.StackSize > 0)
                {
                    stacks.Add(stack);
                }
            }

            MarkDirty(true);

            return stacks.ToArray();
        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (tree.HasAttribute("totalHoursForNextStage"))
            {
                totalHoursForNextStage = tree.GetDouble("totalHoursForNextStage");
            }
            else
            {
                // Pre v1.5.1
                totalHoursForNextStage = tree.GetDouble("totalDaysForNextStage") * 24;
            }

            cropAttrs = tree["cropAttrs"] as TreeAttribute;
            if (cropAttrs == null) cropAttrs = new TreeAttribute();

            unripeCropColdDamaged = tree.GetBool("unripeCropExposedToFrost");
            ripeCropColdDamaged = tree.GetBool("ripeCropExposedToFrost");
            unripeHeatDamaged = tree.GetBool("unripeHeatDamaged");
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("totalHoursForNextStage", totalHoursForNextStage);
            tree.SetBool("ripeCropExposedToFrost", ripeCropColdDamaged);
            tree.SetBool("unripeCropExposedToFrost", unripeCropColdDamaged);
            tree.SetBool("unripeHeatDamaged", unripeHeatDamaged);
            tree["cropAttrs"] = cropAttrs;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            var cropProps = GetCrop()?.CropProps;

            if (cropProps != null)
            {
                dsc.AppendLine(Lang.Get("Required Nutrient: {0}", cropProps.RequiredNutrient));
                dsc.AppendLine(Lang.Get("Growth Stage: {0} / {1}", GetCropStage(GetCrop()), cropProps.GrowthStages));
                dsc.AppendLine();
            }

            if (cropProps == null)
            {
                float speedn = (float)Math.Round(100 * GetGrowthRate(EnumSoilNutrient.N), 0);
                float speedp = (float)Math.Round(100 * GetGrowthRate(EnumSoilNutrient.P), 0);
                float speedk = (float)Math.Round(100 * GetGrowthRate(EnumSoilNutrient.K), 0);

                string colorn = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, speedn)]);
                string colorp = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, speedp)]);
                string colork = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, speedk)]);

                dsc.AppendLine(Lang.Get("farmland-growthspeeds", colorn, speedn, colorp, speedp, colork, speedk));
            }
            else
            {
                float speed = (float)Math.Round(100 * GetGrowthRate(cropProps.RequiredNutrient), 0);
                string color = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, speed)]);

                dsc.AppendLine(Lang.Get("farmland-growthspeed", color, speed, cropProps.RequiredNutrient));
            }

            if ((ripeCropColdDamaged || unripeCropColdDamaged || unripeHeatDamaged) && cropProps != null)
            {
                if (ripeCropColdDamaged)
                {
                    dsc.AppendLine(Lang.Get("farmland-ripecolddamaged", (int)(cropProps.ColdDamageRipeMul * 100)));
                }
                else if (unripeCropColdDamaged)
                {
                    dsc.AppendLine(Lang.Get("farmland-unripecolddamaged", (int)(cropProps.DamageGrowthStuntMul * 100)));
                }
                else if (unripeHeatDamaged)
                {
                    dsc.AppendLine(Lang.Get("farmland-unripeheatdamaged", (int)(cropProps.DamageGrowthStuntMul * 100)));
                }
            }

            base.GetBlockInfo(forPlayer, dsc);
        }


        public ITreeAttribute CropAttributes => cropAttrs;
        public double TotalHoursForNextStage => totalHoursForNextStage;

        #region IAnimalFoodSource impl
        public bool IsSuitableFor(Entity entity, CreatureDiet diet)
        {
            if (diet == null) return false;

            Block cropBlock = GetCrop();
            if (cropBlock == null) return false;
            var creatureFoodTags = cropBlock.Attributes?["foodTags"].AsArray<string>([]) ?? [];

            // Use NoNutrition category because its a crop, not a loose item
            return diet.Matches(EnumFoodCategory.NoNutrition, creatureFoodTags);
        }

        public float ConsumeOnePortion(Entity entity)
        {
            Block cropBlock = GetCrop();
            if (cropBlock == null) return 0;

            Block deadCropBlock = Api.World.GetBlock(new AssetLocation("deadcrop"));
            Api.World.BlockAccessor.SetBlock(deadCropBlock.Id, upPos);
            var be = Api.World.BlockAccessor.GetBlockEntity(upPos) as BlockEntityDeadCrop;
            be.Inventory[0].Itemstack = new ItemStack(cropBlock);
            be.deathReason = EnumCropStressType.Eaten;
            return 1f;
        }

        public Vec3d Position => base.Pos.ToVec3d().Add(0.5, 1, 0.5);
        public string Type => "food";

        #endregion

        BlockPos IFarmlandBlockEntity.Pos => this.Pos;

        public double TotalHoursFertilityCheck => throw new NotImplementedException();

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }


    }
}
