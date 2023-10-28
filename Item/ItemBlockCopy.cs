using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

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

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            ITreeAttribute tree = slot.Itemstack.Attributes;

            string domain = tree.GetString("domain");
            string path = tree.GetString("path");
            ITreeAttribute beotree = tree.GetTreeAttribute("attributes");

            handHandling = EnumHandHandling.PreventDefault;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }
}
