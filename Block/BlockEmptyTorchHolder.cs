using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEmptyTorchHolder : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemStack heldStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            if (heldStack!= null && heldStack.Collectible.Code.Path.Equals("torch-up"))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

                Block filledBlock = world.GetBlock(CodeWithParts("filled", LastCodePart(0)));
                world.BlockAccessor.SetBlock(filledBlock.BlockId, blockSel.Position);

                if (Sounds?.Place != null)
                {
                    world.PlaySoundAt(Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                }

                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
        
    }
}
