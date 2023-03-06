using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class GuiDialogCommandHandbook : GuiDialogHandbook
    {
        public GuiDialogCommandHandbook(ICoreClientAPI capi, OnCreatePagesDelegate createPageHandlerAsync, OnComposePageDelegate composePageHandler) : base(capi, createPageHandlerAsync, composePageHandler)
        {
        }
    }
}
