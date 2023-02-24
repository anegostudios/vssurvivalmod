using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemFirewood : Item
    {
        
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.ShiftKey) return;

            BlockPos onBlockPos = blockSel.Position;
            Block block = byEntity.World.BlockAccessor.GetBlock(onBlockPos);

            if (block is BlockFirepit || block is BlockPitkiln || block is BlockClayOven)
            {
                // Prevent placing firewoodpiles when trying to construct firepits
                return;
            } 

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }




    }
}
