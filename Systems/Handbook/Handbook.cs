using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{

    public delegate void InitCustomPagesDelegate(List<GuiHandbookPage> pages);

    public class ModSystemHandbook : ModSystem
    {
        ICoreClientAPI capi;
        GuiDialogHandbook dialog;

        public event InitCustomPagesDelegate OnInitCustomPages;

        internal void TriggerOnInitCustomPages(List<GuiHandbookPage> pages)
        {
            OnInitCustomPages?.Invoke(pages);
        }


        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            api.Input.RegisterHotKeyFirst("handbook", "Show Handbook", GlKeys.H, HotkeyType.HelpAndOverlays);
            api.Input.SetHotKeyHandler("handbook", OnHelpHotkey);

            api.Event.LevelFinalize += Event_LevelFinalize;
            api.RegisterLinkProtocol("handbook", onHandBookLinkClicked);
            api.RegisterLinkProtocol("handbooksearch", onHandBookSearchLinkClicked);
        }

        private void onHandBookSearchLinkClicked(LinkTextComponent comp)
        {
            string text = comp.Href.Substring("handbooksearch://".Length);
            if (!dialog.IsOpened()) dialog.TryOpen();

            dialog.Search(text);            
        }

        private void onHandBookLinkClicked(LinkTextComponent comp)
        {
            string target = comp.Href.Substring("handbook://".Length);

            // Seems to fix links like thos not working: block-labeledchest-east-{{ \"type\": \\\"normal-labeled\\\" }}
            target = target.Replace("\\", "");

            if (!dialog.IsOpened()) dialog.TryOpen();

            dialog.OpenDetailPageFor(target);
        }

        private void Event_LevelFinalize()
        {
            dialog = new GuiDialogHandbook(capi);
            capi.Logger.VerboseDebug("Done initialising handbook");
        }

        private bool OnHelpHotkey(KeyCombination key)
        {
            if (dialog.IsOpened())
            {
                dialog.TryClose();
            } else
            {
                dialog.TryOpen();
                // dunno why
                dialog.ignoreNextKeyPress = true;

                if (capi.World.Player.InventoryManager.CurrentHoveredSlot?.Itemstack != null)
                {
                    ItemStack stack = capi.World.Player.InventoryManager.CurrentHoveredSlot.Itemstack;
                    string pageCode = GuiHandbookItemStackPage.PageCodeForStack(stack); 

                    if (!dialog.OpenDetailPageFor(pageCode))
                    {
                        dialog.OpenDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(new ItemStack(stack.Collectible)));
                    }
                }

                if (capi.World.Player.Entity.Controls.ShiftKey && capi.World.Player.CurrentBlockSelection != null)
                {
                    BlockPos pos = capi.World.Player.CurrentBlockSelection.Position;
                    ItemStack stack = capi.World.BlockAccessor.GetBlock(pos).OnPickBlock(capi.World, pos);

                    string pageCode = GuiHandbookItemStackPage.PageCodeForStack(stack);

                    if (!dialog.OpenDetailPageFor(pageCode))
                    {
                        dialog.OpenDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(new ItemStack(stack.Collectible)));
                    }
                }
            }

            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            dialog?.Dispose();
            capi?.Input.HotKeys.Remove("handbook");
        }

    }
}
