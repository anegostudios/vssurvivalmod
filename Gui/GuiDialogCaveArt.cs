using Cairo;
using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class GuiDialogCaveArt : GuiDialogGeneric
    {
        public GuiDialogCaveArt(string DialogTitle, ICoreClientAPI capi) : base(DialogTitle, capi)
        {
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();

            double size = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGrid.unscaledSlotPadding;
            double innerWidth = size;
            int rows = 1;
            ElementBounds textBounds = ElementBounds.Fixed(0, rows * (size + 2) + 5, innerWidth, 25);

            SingleComposer =
                capi.Gui
                .CreateCompo("caveartselect", ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding / 2), false)
                .BeginChildElements()
            ;

            SingleComposer
                .AddDynamicText("", CairoFont.WhiteSmallishText(), EnumTextOrientation.Left, textBounds, "name")
                .EndChildElements()
                .Compose()
            ;
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
        }
    }
}
