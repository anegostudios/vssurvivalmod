using Vintagestory.API.Client;

#nullable disable

namespace Vintagestory.GameContent
{
    public class HudTutorial : HudElement
    {
        public HudTutorial(ICoreClientAPI capi) : base(capi)
        {
            
        }

        public void loadHud(string pagecode)
        {
            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 300, 200);
            ElementBounds bgBounds = new ElementBounds().WithSizing(ElementSizing.FitToChildren).WithFixedPadding(GuiStyle.ElementToDialogPadding / 2);
            bgBounds.WithChildren(textBounds);

            ElementBounds dialogBounds = bgBounds.ForkBoundingParent().WithAlignment(EnumDialogArea.None).WithAlignment(EnumDialogArea.RightMiddle).WithFixedPosition(0, -225);

            RichTextComponentBase[] cmps = capi.ModLoader.GetModSystem<ModSystemTutorial>().GetPageText(pagecode, true);

            SingleComposer?.Dispose();
            SingleComposer = capi.Gui
                .CreateCompo("tutorialhud", dialogBounds)
                .AddGameOverlay(bgBounds, GuiStyle.DialogLightBgColor)
                .AddRichtext(cmps, textBounds, "richtext")
                .Compose()
            ;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}