using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class PagePosition
    {
        public int Start, Length;
    }

    public class ItemBook : Item, IBookShelvable
    {
        ModSystemEditableBook bookModSys;
        int maxPageCount;
        bool editable;
        ICoreClientAPI capi;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;
            editable = Attributes["editable"].AsBool(false);
            maxPageCount = Attributes["maxPageCount"].AsInt(90);
            bookModSys = api.ModLoader.GetModSystem<ModSystemEditableBook>();
        }


        ItemSlot curSlot;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.Controls.Sneak)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (!isReadable(slot))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            var player = (byEntity as EntityPlayer).Player;

            if (editable && isWritingTool(byEntity.LeftHandItemSlot) && !isSigned(slot))
            {
                bookModSys.BeginEdit(player, slot);

                if (api.Side == EnumAppSide.Client)
                {
                    var dlg = new GuiDialogEditableBook(slot.Itemstack, api as ICoreClientAPI, maxPageCount);
                    dlg.OnClosed += () =>
                    {
                        if (dlg.DidSave) bookModSys.EndEdit(player, dlg.AllPagesText, dlg.Title, dlg.DidSign);
                        else bookModSys.CancelEdit(player);
                    };
                    dlg.TryOpen();
                }

                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (slot.Itemstack.Attributes.HasAttribute("text") || slot.Itemstack.Attributes.HasAttribute("textCodes"))
            {
                bookModSys.BeginEdit(player, slot);

                if (api.Side == EnumAppSide.Client)
                {
                    curSlot = slot;
                    var dlg = new GuiDialogReadonlyBook(slot.Itemstack, api as ICoreClientAPI, onTranscribePressed);
                    dlg.OnClosed += () =>
                    {
                        curSlot = null;
                        bookModSys.CancelEdit(player);
                    };
                    dlg.TryOpen();
                }

                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        private void onTranscribePressed(string pageText, string pageTitle, int pageNumber)
        {
            bookModSys.Transcribe(capi.World.Player, pageText, pageTitle, pageNumber, curSlot);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string title = itemStack.Attributes.GetString("title");
            if (title != null) return title;

            return base.GetHeldItemName(itemStack);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string signedby = inSlot.Itemstack.Attributes.GetString("signedby");
            string transcribedby = inSlot.Itemstack.Attributes.GetString("transcribedby");
            if (signedby != null)
            {
                dsc.AppendLine(Lang.Get("Written by {0}", signedby.Length == 0 ? Lang.Get("Unknown author") : signedby));
            }
            if (transcribedby != null && transcribedby.Length > 0)
            {
                dsc.AppendLine(Lang.Get("Transcribed by {0}", transcribedby.Length == 0 ? Lang.Get("Unknown author") : transcribedby));
            }
        }


        public static bool isReadable(ItemSlot slot)
        {
            return slot.Itemstack.Collectible.Attributes["readable"].AsBool() == true;
        }
        public static bool isSigned(ItemSlot slot)
        {
            return slot.Itemstack.Attributes.GetString("signedby") != null;
        }
        public static bool isWritingTool(ItemSlot slot)
        {
            return slot.Itemstack?.Collectible.Attributes?.IsTrue("writingTool") == true;
        }
    }
}
