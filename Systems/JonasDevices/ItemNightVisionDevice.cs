using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class ModSystemNightVision : ModSystem, IRenderer
    {
        public double RenderOrder => 0;
        public int RenderRange => 1;

        IInventory gearInv;
        ICoreClientAPI capi;
        double hoursPassedLastFuelCheck;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Before, "nightvision");
            api.Event.LevelFinalize += Event_LevelFinalize;
        }

        private void Event_LevelFinalize()
        {
            gearInv = capi.World.Player.Entity.GearInventory;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (gearInv == null) return;

            var headSlot = gearInv[(int)EnumCharacterDressType.ArmorHead];
            var stack = headSlot?.Itemstack;
            var itemnvd = stack?.Collectible as ItemNightvisiondevice;
            var fuelLeft = itemnvd == null ? 0 : itemnvd.GetFuelLeft(stack);

            if (itemnvd != null)
            {
                capi.Render.ShaderUniforms.NightVisonStrength = (float)Math.Min(fuelLeft * 20, 0.8);

                double totalHours = capi.World.Calendar.TotalHours;

                if (hoursPassedLastFuelCheck - totalHours > 1)
                {
                    itemnvd.SetFuelLeft(stack, itemnvd.GetFuelLeft(stack) - 1 / 24f);
                    hoursPassedLastFuelCheck = totalHours;
                }
            }
            else
            {
                capi.Render.ShaderUniforms.NightVisonStrength = 0;
            }
        }

    }

    public class ItemNightvisiondevice : ItemWearable
    {
        protected float fuelHoursCapacity = 24;

        public float GetFuelLeft(ItemStack stack)
        {
            return stack.Attributes.GetFloat("fuel");
        }
        public void SetFuelLeft(ItemStack stack, float amount)
        {
            stack.Attributes.SetFloat("fuel", amount);
        }

        public float GetStackFuel(ItemStack stack)
        {
            return stack.ItemAttributes?["nightVisionFuelHours"].AsFloat(0) ?? 0;
        }

        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (priority == EnumMergePriority.DirectMerge)
            {
                float fuel = GetStackFuel(sourceStack);
                if (fuel == 0) return base.GetMergableQuantity(sinkStack, sourceStack, priority);
                return 1;
            }

            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            if (op.CurrentPriority == EnumMergePriority.DirectMerge)
            {
                float fuel = GetStackFuel(op.SourceSlot.Itemstack);

                if (fuel > 0 && GetFuelLeft(op.SinkSlot.Itemstack) < fuelHoursCapacity)
                {
                    SetFuelLeft(op.SinkSlot.Itemstack, Math.Min(fuelHoursCapacity, GetFuelLeft(op.SinkSlot.Itemstack) + fuel));
                    op.MovedQuantity = 1;
                    op.SourceSlot.TakeOut(1);
                    op.SinkSlot.MarkDirty();
                    return;
                }
            }

            base.TryMergeStacks(op);
        }



        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            float fuelLeft = GetFuelLeft(inSlot.Itemstack);
            dsc.AppendLine(Lang.Get("Has fuel for {0:0.#} hours", fuelLeft));
            if (fuelLeft < 0.1)
            {
                dsc.AppendLine(Lang.Get("Add temporal gear to fuel"));
            }
        }
    }
}
