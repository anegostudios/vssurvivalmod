using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{

    public class ModSystemNightVision : ModSystem, IRenderer
    {
        public double RenderOrder => 0;
        public int RenderRange => 1;

        //IInventory gearInv;
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        EntityBehaviorPlayerInventory bh;

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Before, "nightvision");
            api.Event.LevelFinalize += Event_LevelFinalize;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            api.Event.RegisterGameTickListener(onTickServer1s, 1000, 200);
        }

        double lastCheckTotalHours;
        private void onTickServer1s(float dt)
        {
            double totalHours = sapi.World.Calendar.TotalHours;
            double hoursPassed = totalHours - lastCheckTotalHours;

            if (hoursPassed > 0.05)
            {
                foreach (var plr in sapi.World.AllOnlinePlayers)
                {
                    var inv = plr.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                    if (inv == null) continue;

                    var headArmorSlot = inv[(int)EnumCharacterDressType.ArmorHead];
                    if (headArmorSlot.Itemstack?.Collectible is ItemNightvisiondevice invd)
                    {
                        invd.AddFuelHours(headArmorSlot.Itemstack, -hoursPassed);
                        headArmorSlot.MarkDirty();
                    }
                }

                lastCheckTotalHours = totalHours;
            }
        }

        private void Event_LevelFinalize()
        {
            //gearInv = capi.World.Player.Entity.GearInventory;
            bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            //if (gearInv == null) return;
            if (bh?.Inventory == null) return;

            var headSlot = bh.Inventory[(int)EnumCharacterDressType.ArmorHead];
            var stack = headSlot?.Itemstack;
            var itemnvd = stack?.Collectible as ItemNightvisiondevice;
            var fuelLeft = itemnvd == null ? 0 : itemnvd.GetFuelHours(stack);

            if (itemnvd != null)
            {
                capi.Render.ShaderUniforms.NightVisionStrength = (float)GameMath.Clamp(fuelLeft * 20, 0, 0.8);
            }
            else
            {
                capi.Render.ShaderUniforms.NightVisionStrength = 0;
            }
        }

    }

    public class ItemNightvisiondevice : ItemWearable
    {
        protected float fuelHoursCapacity = 24;

        public double GetFuelHours(ItemStack stack)
        {
            return Math.Max(0, stack.Attributes.GetDecimal("fuelHours"));
        }
        public void SetFuelHours(ItemStack stack, double fuelHours)
        {
            stack.Attributes.SetDouble("fuelHours", fuelHours);
        }
        public void AddFuelHours(ItemStack stack, double fuelHours)
        {
            stack.Attributes.SetDouble("fuelHours", Math.Max(0, fuelHours + GetFuelHours(stack)));
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
                double fuelHoursLeft = GetFuelHours(op.SinkSlot.Itemstack);
                if (fuel > 0 && fuelHoursLeft + fuel/2 < fuelHoursCapacity)
                {
                    SetFuelHours(op.SinkSlot.Itemstack, fuel + fuelHoursLeft);
                    op.MovedQuantity = 1;
                    op.SourceSlot.TakeOut(1);
                    op.SinkSlot.MarkDirty();
                    return;
                }

                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "maskfull", Lang.Get("ingameerror-mask-full"));
                }
            }
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            double fuelLeft = GetFuelHours(inSlot.Itemstack);
            dsc.AppendLine(Lang.Get("Has fuel for {0:0.#} hours", fuelLeft));
            if (fuelLeft <= 0)
            {
                dsc.AppendLine(Lang.Get("Add temporal gear to refuel"));
            }
        }

    }
}
