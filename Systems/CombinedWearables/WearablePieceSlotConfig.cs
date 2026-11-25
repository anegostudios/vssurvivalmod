using System;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class WearableBitSlotConfig : WearableSlotConfig, ICloneable
    {
        /// <summary>
        /// Layer code of corresponding slot.<br/>
        /// <br/>
        /// In <see cref="EntityBehaviorWearablesCraftingStand"/> is filled on initialisation automaically, instead of being taken from json.
        /// </summary>
        public string Layer = "";

        /// <summary>
        /// Used to calculate hit chance to this slot as fraction of total coverage of layer
        /// </summary>
        public int Coverage = 0;

        public virtual object Clone()
        {
            return new WearableBitSlotConfig()
            {
                SeatConfig = SeatConfig,
                Code = Code,
                ForCategoryCodes = (string[]?)ForCategoryCodes?.Clone(),
                BehindSlots = (string[]?)BehindSlots?.Clone(),
                AttachmentPointCode = AttachmentPointCode,
                StepParentTo = StepParentTo?.ShallowClone(),
                ProvidesSeatId = ProvidesSeatId,
                EmptyInteractPassThrough = EmptyInteractPassThrough,
                Layer = Layer,
                Coverage = Coverage
            };
        }
    }
}
