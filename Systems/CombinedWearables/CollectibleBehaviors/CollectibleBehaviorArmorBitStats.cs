using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;


namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorArmorBitStats : CollectibleBehavior
    {
        public CollectibleBehaviorArmorBitStats(CollectibleObject collObj) : base(collObj)
        {
        }



        public ProtectionModifiers DefaultModifiers { get; set; } = new();
        public JsonObject? ModifiersBySlot { get; set; }
        public JsonObject? StatsBySlotCode { get; set; }
        public JsonObject? TotalStats { get; set; }



        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            DefaultModifiers = properties["protectionModifiers"]?.AsObject<ProtectionModifiers>() ?? new();
            ModifiersBySlot = properties["protectionModifiersBySlotCode"];
            StatsBySlotCode = properties["statsBySlotCode"];
            TotalStats = properties["statsByItemCode"];
        }

        public virtual ProtectionModifiers? GetArmorBitProtectionModifiers(ItemStack stack, string slotCode, string layerCode)
        {
            ProtectionModifiers defaultModifiers = DefaultModifiers;
            ProtectionModifiers? statsBySlotCode = ModifiersBySlot?.FirstByWildcardKey(slotCode)?["protectionModifiers"]?.AsObject<ProtectionModifiers>();

            if (statsBySlotCode != null) defaultModifiers = statsBySlotCode;

            return defaultModifiers;
        }

        public virtual void GetStatModifiers(ItemStack stack, string slotCode, string layerCode, string carrierCode, out StatModifiers byItem, out StatModifiers bySlot, out int maxPieces)
        {
            bySlot = StatsBySlotCode?.FirstByWildcardKey(slotCode)?.AsObject<StatModifiers>() ?? new();
            byItem = TotalStats?.FirstByWildcardKey(carrierCode)?["statModifiers"]?.AsObject<StatModifiers>() ?? new();
            maxPieces = TotalStats?.FirstByWildcardKey(carrierCode)?["maxBits"]?.AsInt(1) ?? 1;
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            GetProtectionInfo(inSlot, dsc, world, withDebugInfo);
            GetStatsInfo(inSlot, dsc, world, withDebugInfo);
        }

        public virtual void GetStatsInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            // too complex to display
        }

        public virtual void GetProtectionInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack == null) return;

            ProtectionModifiers defaultModifiers = DefaultModifiers;

            dsc.AppendLine();
            defaultModifiers.GetDescription(dsc, world.Api);

            // ignore ModifiersBySlot - too complex to display
        }
    }
}
