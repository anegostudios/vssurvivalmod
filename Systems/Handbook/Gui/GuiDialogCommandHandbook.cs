using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{

    public class GuiDialogCommandHandbook : GuiDialogHandbook
    {
        public override string DialogTitle => Lang.Get("Command Handbook");

        public GuiDialogCommandHandbook(ICoreClientAPI capi, OnCreatePagesDelegate createPageHandlerAsync, OnComposePageDelegate composePageHandler) : base(capi, createPageHandlerAsync, composePageHandler)
        {
            categoryCodes = new List<string>() { null, "server", "client" };
        }

        protected override GuiTab[] genTabs(out int curTab)
        {
            curTab = 0;
            return new GuiTab[]
            {
                new HandbookTab() { Name=Lang.Get("All"), CategoryCode = null },
                new HandbookTab() { Name=Lang.Get("Server Commands"), CategoryCode = "server" },
                new HandbookTab() { Name=Lang.Get("Client Commands"), CategoryCode = "client" },
            };
        }
    }
}
