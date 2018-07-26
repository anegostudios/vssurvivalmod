using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class ItemSellingSlot : ItemSlot
    {
        public ItemSellingSlot(InventoryBase inventory) : base(inventory)
        {
        }

        public override bool CanTake()
        {
            return base.CanTake();
        }

        public override bool CanTakeFrom(IItemSlot sourceSlot)
        {
            return base.CanTakeFrom(sourceSlot);
        }

        public override bool CanHold(IItemSlot sourceSlot)
        {
            return base.CanHold(sourceSlot);
        }

        
        public override bool TryFlipWith(ItemSlot itemSlot)
        {
            return base.TryFlipWith(itemSlot);
        }

        protected override void FlipWith(ItemSlot withSlot)
        {
            base.FlipWith(withSlot);
        }

        public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            base.ActivateSlot(sourceSlot, ref op);
        }

        public override void TryPutInto(IItemSlot sinkSlot, ref ItemStackMoveOperation op)
        {
            base.TryPutInto(sinkSlot, ref op);
        }

        public override void TryPutInto(IWorldAccessor world, IItemSlot sinkSlot)
        {
            base.TryPutInto(world, sinkSlot);
        }
    }
}
