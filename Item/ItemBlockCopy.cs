using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemBlockCopy : Item
    {
        public static void GenStack()
        {
            /*AssetLocation blockCode = byEntity.World.BlockAccessor.GetBlock(blockSel.Position).Code;
            TreeAttribute tree = new TreeAttribute();
            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be != null)
            {
                be.ToTreeAttributes(tree);
            }*/
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling)
        {
            if (blockSel == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, ref handHandling);
                return;
            }

            ITreeAttribute tree = slot.Itemstack.Attributes;

            string domain = tree.GetString("domain");
            string path = tree.GetString("path");
            ITreeAttribute beotree = tree.GetTreeAttribute("attributes");

            handHandling = EnumHandHandling.PreventDefault;
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);
        }
    }
}
