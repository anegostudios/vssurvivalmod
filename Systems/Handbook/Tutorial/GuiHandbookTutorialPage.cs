using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{

    public class GuiHandbookTutorialPage : GuiHandbookTextPage, IFlatListItemInteractable
    {
        private ICoreClientAPI capi;

        GuiElementTextButton startStopButton;
        ElementBounds baseBounds;
        ModSystemTutorial modsys;

        bool tutorialActive;

        public GuiHandbookTutorialPage(ICoreClientAPI capi, string pagecode)
        {
            this.pageCode = pagecode;
            this.capi = capi;

            modsys = capi.ModLoader.GetModSystem<ModSystemTutorial>();

            tutorialActive = modsys.CurrentTutorial == pageCode.Substring("tutorial-".Length);

            this.Title = Lang.Get("title-" + pagecode);
            this.Text = "";

            recomposeButton();
        }

        private void recomposeButton()
        {
            startStopButton?.Dispose();
            baseBounds = ElementBounds.Fixed(0, 0, 400, 100).WithParent(capi.Gui.WindowBounds);
            startStopButton = new GuiElementTextButton(capi, Lang.Get("Start"), CairoFont.WhiteSmallText(), CairoFont.WhiteSmallText(), onStartStopTutorial, ElementBounds.Fixed(0, 0).WithFixedPadding(6, 3).WithParent(baseBounds), EnumButtonStyle.Normal);

            startStopButton.Text = tutorialActive ? Lang.Get("Stop") : Lang.Get("Start");

            ImageSurface surface = new ImageSurface(Format.Argb32, 1, 1);
            Context ctx = new Context(surface);
            startStopButton.BeforeCalcBounds();
            startStopButton.ComposeElements(ctx, surface);
            ctx.Dispose();
            surface.Dispose();
        }

        private bool onStartStopTutorial()
        {
            if (!tutorialActive)
            {
                tutorialActive = true;
                modsys.StartTutorial(pageCode.Substring("tutorial-".Length));
            } else
            {
                tutorialActive = false;
                modsys.StopActiveTutorial();
            }
            recomposeButton();
            return true;
        }

        public override string PageCode => pageCode;

        public override string CategoryCode => "tutorial";

        public override bool IsDuplicate => false;

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWdith, double cellHeight)
        {
            base.RenderListEntryTo(capi, dt, x, y, cellWdith, cellHeight);

            if (startStopButton == null)
            {
                recomposeButton();
            }

            baseBounds.absFixedX = x + cellWdith - startStopButton.Bounds.OuterWidth - 10;
            baseBounds.absFixedY = y;

            startStopButton.RenderInteractiveElements(dt);

            if (tutorialActive && "tutorial-" + modsys.CurrentTutorial != pageCode)
            {
                tutorialActive = false;
                recomposeButton();
            }
        }

        public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
        {
            var comps = capi.ModLoader.GetModSystem<ModSystemTutorial>().GetPageText(pageCode, false);
            detailViewGui.AddRichtext(comps, textBounds, "richtext");
        }

        public override float GetTextMatchWeight(string text)
        {
            /*string title = TextCacheTitle;
            if (title.Equals(searchText, StringComparison.InvariantCultureIgnoreCase)) return 3;
            if (title.StartsWith(searchText, StringComparison.InvariantCultureIgnoreCase)) return 2.5f;
            if (title.CaseInsensitiveContains(searchText)) return 2;
            if (TextCacheAll.CaseInsensitiveContains(searchText)) return 1;*/
            return 0;
        }


        public override void Dispose()
        {
            startStopButton?.Dispose();
            startStopButton = null;
        }

        public void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            startStopButton?.OnMouseMove(api, args);
        }

        public void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            startStopButton?.OnMouseDown(api, args);
        }

        public void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            startStopButton?.OnMouseUp(api, args);
        }
    }
}