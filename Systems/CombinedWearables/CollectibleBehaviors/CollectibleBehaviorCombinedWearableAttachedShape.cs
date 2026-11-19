using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;


namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorCombinedWearableAttachedShape : CollectibleBehavior, ICustomAttachableGear
    {
        public CollectibleBehaviorCombinedWearableAttachedShape(CollectibleObject collObj) : base(collObj)
        {
        }


        protected ICoreAPI? api { get; set; }


        public virtual Shape AddGearToShape(Shape entityShape, ItemSlot gearSlot, string slotCode, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements, IDictionary<string, CompositeTexture>? collectedTextures)
        {
            ICombinedWearable? combined = gearSlot.Itemstack?.Collectible?.GetCollectibleInterface<ICombinedWearable>();

            if (gearSlot.Empty || api is not ICoreClientAPI clientApi || collectedTextures == null || combined == null) return entityShape;

            float damageEffect = 0;
            if (gearSlot.Itemstack.ItemAttributes?["visibleDamageEffect"].AsBool() == true)
            {
                damageEffect = Math.Max(0, 1 - (1 - (float)getLostDurabiltiy(gearSlot.Itemstack) / gearSlot.Itemstack.Collectible.GetMaxDurability(gearSlot.Itemstack)) * 1.1f);
            }

            IEnumerable<WearableBitEntry> bits = combined.RetrieveBits(gearSlot.Itemstack);
            foreach (WearableBitEntry entry in bits)
            {
                IWearableBitAttachedShape? bitAttachedShape = entry.Stack.Collectible.GetCollectibleInterface<IWearableBitAttachedShape>();
                bitAttachedShape?.AddGearToShape(clientApi.EntityTextureAtlas, entityShape, entry, shapePathForLogging, ref willDeleteElements, collectedTextures, carrierDamageEffect: damageEffect);
            }

            return entityShape;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
        }


        protected virtual int getLostDurabiltiy(ItemStack carrier)
        {
            int carierMaxDurability = carrier.Collectible.GetMaxDurability(carrier);
            int carrierCurrentDurability = carrier.Collectible.GetRemainingDurability(carrier);
            int durabilityLost = carierMaxDurability - carrierCurrentDurability;

            return durabilityLost;
        }
    }
}
