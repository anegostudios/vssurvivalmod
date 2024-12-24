using Cairo;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{

    public class GuiHandbookTutorialPage : GuiHandbookTextPage, IFlatListItemInteractable
    {
        private ICoreClientAPI capi;

        GuiElementTextButton startStopButton;
        GuiElementTextButton restartButton;
        ElementBounds baseBounds;
        ModSystemTutorial modsys;

        bool tutorialActive;
        float prevProgress;

        public GuiHandbookTutorialPage(ICoreClientAPI capi, string pagecode)
        {
            this.pageCode = pagecode;
            this.capi = capi;
            this.Title = Lang.Get("title-" + pagecode);
            this.Text = "";

            modsys = capi.ModLoader.GetModSystem<ModSystemTutorial>();
            recomposeButton();
        }

        private void recomposeButton()
        {
            tutorialActive = modsys.CurrentTutorial == pageCode.Substring("tutorial-".Length);

            startStopButton?.Dispose();
            restartButton?.Dispose();

            baseBounds = ElementBounds.Fixed(0, 0, 400, 100).WithParent(capi.Gui.WindowBounds);
            startStopButton = new GuiElementTextButton(capi, Lang.Get("Start"), CairoFont.WhiteSmallText(), CairoFont.WhiteSmallText(), onStartStopTutorial, ElementBounds.Fixed(0, 0).WithFixedPadding(6, 3).WithParent(baseBounds), EnumButtonStyle.Normal);
            restartButton = null;

            prevProgress = modsys.GetTutorialProgress(pageCode.Substring("tutorial-".Length));

            if (tutorialActive)
            {
                startStopButton.Text = Lang.Get("Stop Tutorial");
            }
            else
            {
                if (prevProgress >= 1)
                {
                    startStopButton = null;
                }
                else
                {
                    startStopButton.Text = Lang.Get(prevProgress > 0 ? "button-tutorial-resume" : "Start Tutorial");
                }
            }

            if (startStopButton != null) compose(startStopButton);

            if ((!tutorialActive && prevProgress > 0) || prevProgress >= 1)
            {
                var rbounds = ElementBounds.Fixed(0, 0).WithFixedPadding(6, 3).WithParent(baseBounds);
                restartButton = new GuiElementTextButton(capi, Lang.Get("Restart"), CairoFont.WhiteSmallText(), CairoFont.WhiteSmallText(), onRestartTutorial, rbounds, EnumButtonStyle.Normal);
                compose(restartButton);

                rbounds.fixedX -= restartButton.Bounds.OuterWidth/RuntimeEnv.GUIScale + 5;
                rbounds.CalcWorldBounds();
            }
        }

        private bool onRestartTutorial()
        {
            modsys.StartTutorial(pageCode.Substring("tutorial-".Length), true);
            capi.Event.EnqueueMainThreadTask(() => capi.Gui.LoadedGuis.FirstOrDefault(dlg => dlg is GuiDialogSurvivalHandbook)?.TryClose(), "closehandbook");
            recomposeButton();
            return true;
        }

        private void compose(GuiElementTextButton button)
        {
            ImageSurface surface = new ImageSurface(Format.Argb32, 1, 1);
            Context ctx = new Context(surface);
            button.BeforeCalcBounds();
            button.ComposeElements(ctx, surface);
            ctx.Dispose();
            surface.Dispose();
        }

        private bool onStartStopTutorial()
        {
            if (!tutorialActive)
            {
                tutorialActive = true;
                modsys.StartTutorial(pageCode.Substring("tutorial-".Length));
                capi.Event.EnqueueMainThreadTask(() => capi.Gui.LoadedGuis.FirstOrDefault(dlg => dlg is GuiDialogSurvivalHandbook)?.TryClose(), "closehandbook");
            } else
            {
                tutorialActive = false;
                modsys.StopActiveTutorial();
                recomposeButton();
            }
            
            return true;
        }

        public override string PageCode => pageCode;

        public override string CategoryCode => "tutorial";

        public override bool IsDuplicate => false;

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWdith, double cellHeight)
        {
            base.RenderListEntryTo(capi, dt, x, y, cellWdith, cellHeight);

            if (startStopButton == null && restartButton == null)
            {
                recomposeButton();
            }

            float p = modsys.GetTutorialProgress(pageCode.Substring("tutorial-".Length));
            if (p != prevProgress) recomposeButton();

            baseBounds.absFixedX = x + cellWdith - (startStopButton?.Bounds.OuterWidth??0) - 10;
            baseBounds.absFixedY = y;

            startStopButton?.RenderInteractiveElements(dt);

            if (restartButton != null)
            {
                restartButton?.RenderInteractiveElements(dt);
            }

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

            restartButton?.Dispose();
            restartButton = null;
        }

        public void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            startStopButton?.OnMouseMove(api, args);
            if (!args.Handled) restartButton?.OnMouseMove(api, args);
        }

        public void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            startStopButton?.OnMouseDown(api, args);
            if (!args.Handled) restartButton?.OnMouseDown(api, args);
        }

        public void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            startStopButton?.OnMouseUp(api, args);
            if (!args.Handled) restartButton?.OnMouseUp(api, args);
        }
    }
}