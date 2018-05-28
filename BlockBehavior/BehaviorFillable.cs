using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorLiquidContainer : BlockBehavior
    {
        public BlockBehaviorLiquidContainer(Block block) : base(block)
        {
        }

        public virtual bool OnInteractWithBucket(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel)
        {
            return false;
        }

    }
}
