using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class BehaviorRightClickPickup : BlockBehavior
    {
        public BehaviorRightClickPickup(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            ItemStack stack = block.OnPickBlock(world, blockSel.Position);

            if (!byPlayer.Entity.Controls.Sneak && byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                {
                    world.BlockAccessor.SetBlock(0, blockSel.Position);
                    world.PlaySoundAt(block.Sounds.Place, byPlayer, byPlayer);
                    handling = EnumHandling.PreventDefault;
                    return true;
                }
            }

            return false;
        }
        
    }
}
