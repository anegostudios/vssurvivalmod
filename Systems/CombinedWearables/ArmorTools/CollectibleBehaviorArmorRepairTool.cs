using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Tool for repairing armor pieces on crafting stand
    /// </summary>
    public class CollectibleBehaviorArmorRepairTool : CollectibleBehaviorArmorTool, IArmorRepairTool
    {
        public CollectibleBehaviorArmorRepairTool(CollectibleObject collObj) : base(collObj)
        {
        }



        public float MinimumDurabilityFraction { get; set; } = 0;

        public float RepairEfficiency { get; set; } = 1;


        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            MinimumDurabilityFraction = properties["minimumDurabilityFraction"]?.AsFloat(MinimumDurabilityFraction) ?? MinimumDurabilityFraction;
            RepairEfficiency = properties["repairEfficiency"]?.AsFloat(RepairEfficiency) ?? RepairEfficiency;
        }

        public bool CanRepair(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand)
        {
            if (toolSlot.Itemstack == null) return false;

            int toolDurability = toolSlot.Itemstack.Collectible.GetRemainingDurability(toolSlot.Itemstack);
            int availableDurability = (int)Math.Floor(toolDurability * RepairEfficiency);

            foreach (ItemSlot bitSlot in bitsSlots)
            {
                if (availableDurability <= 0) break;

                if (bitSlot.Itemstack == null || !ArmorBitIsSupported(bitSlot)) continue;

                bool canRepair = CalculateRepairValues(bitSlot.Itemstack, toolDurability, out int repairedDamage, out int toolDamage);

                if (canRepair) return true;
            }

            return false;
        }

        public void Repair(ItemSlot toolSlot, IEnumerable<ItemSlot> bitsSlots, EntityAgent byEntity, ILayeredContainer craftingStand)
        {
            if (toolSlot.Itemstack == null) return;

            DummySlot currentToolSlot = new(toolSlot.TakeOut(1));

            if (currentToolSlot.Itemstack == null) return;

            int toolDurability = currentToolSlot.Itemstack.Collectible.GetRemainingDurability(currentToolSlot.Itemstack);
            int availableDurability = (int)Math.Floor(toolDurability * RepairEfficiency);

            foreach (ItemSlot bitSlot in bitsSlots)
            {
                if (availableDurability <= 0) break;

                if (bitSlot.Itemstack == null || !ArmorBitIsSupported(bitSlot)) continue;

                bool canRepair = CalculateRepairValues(bitSlot.Itemstack, toolDurability, out int repairedDamage, out int toolDamage);

                if (!canRepair) continue;

                bitSlot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, bitSlot, -repairedDamage);
                currentToolSlot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, currentToolSlot, toolDamage);

                int pieceMaxDurability = bitSlot.Itemstack.Collectible.GetMaxDurability(bitSlot.Itemstack);
                int pieceRemainingDurability = bitSlot.Itemstack.Collectible.GetRemainingDurability(bitSlot.Itemstack);

                if (pieceMaxDurability == pieceRemainingDurability)
                {
                    bitSlot.Itemstack.Attributes.RemoveAttribute("durability");
                }

                bitSlot.MarkDirty();

                if (currentToolSlot.Itemstack == null) break;

                availableDurability -= toolDamage;
            }

            if (currentToolSlot.TryPutInto(byEntity.World, toolSlot) <= 0 && currentToolSlot.Itemstack != null)
            {
                ItemStack unusedTool = currentToolSlot.TakeOutWhole();
                if (!byEntity.TryGiveItemStack(unusedTool))
                {
                    byEntity.World.SpawnItemEntity(unusedTool, byEntity.Pos.AsBlockPos);
                }
            }
            currentToolSlot.MarkDirty();

            OnUsed(toolSlot, bitsSlots, byEntity, craftingStand);
        }

        public bool CalculateRepairValues(ItemStack bit, int toolDurability, out int repairedDamage, out int toolDamage)
        {
            int maxDurability = bit.Collectible.GetMaxDurability(bit);
            int remainingDurability = bit.Collectible.GetRemainingDurability(bit);
            toolDamage = (int)Math.Ceiling((maxDurability - remainingDurability) / RepairEfficiency);
            toolDamage = Math.Min(toolDurability, toolDamage);
            repairedDamage = Math.Min((int)Math.Floor(toolDamage * RepairEfficiency), maxDurability - remainingDurability);
            float remainingFraction = (float)remainingDurability / maxDurability;

            return repairedDamage != 0 && remainingFraction >= MinimumDurabilityFraction;
        }
    }
}
