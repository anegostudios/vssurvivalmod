using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class BehaviorSneakPlacing : BlockBehavior
    {
        public BehaviorSneakPlacing(Block block) : base(block)
        {
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            if (!byPlayer.Entity.Controls.Sneak)
            {
                handling = EnumHandling.PreventDefault;
                failureCode = "onlywhensneaking";
                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref handling, ref failureCode);
        }
        
    }
}
