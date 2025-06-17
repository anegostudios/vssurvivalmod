using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemCattailRoot : Item
    {
        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            Block block;

            if (this.Code.Path.Contains("papyrus"))
            {
                block = byEntity.World.GetBlock(new AssetLocation("tallplant-papyrus-land-harvested-free"));
            }
            else if (this.Code.Path.Equals("tuleroot"))
            {
                block = byEntity.World.GetBlock(new AssetLocation("tallplant-tule-land-harvested-free"));
            }
            else
            {
                block = byEntity.World.GetBlock(new AssetLocation("tallplant-coopersreed-land-harvested-free"));
            }

            if (block == null)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            blockSel = blockSel.Clone();
            blockSel.Position.Add(blockSel.Face);

            string useless = "";

            bool ok = block.TryPlaceBlock(byEntity.World, byPlayer, itemslot.Itemstack, blockSel, ref useless);

            if (ok)
            {
                byEntity.World.PlaySoundAt(block.Sounds.GetBreakSound(byPlayer), blockSel.Position, 0, byPlayer);
                itemslot.TakeOut(1);
                itemslot.MarkDirty();
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    HotKeyCode = "shift",
                    ActionLangCode = "heldhelp-plant",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }
}