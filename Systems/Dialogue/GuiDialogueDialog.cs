using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public delegate int DialogueTriggerDelegate(EntityAgent triggeringEntity, string value, JsonObject data);

    public class GuiDialogueDialog : GuiDialog
    {
        protected GuiDialog chatDialog;
        ElementBounds clipBounds;
        GuiElementRichtext textElem;
        EntityAgent npcEntity;
        public override string ToggleKeyCombinationCode => null;

        public GuiDialogueDialog(ICoreClientAPI capi, EntityAgent npcEntity) : base(capi)
        {
            this.npcEntity = npcEntity;
        }

        public void InitAndOpen()
        {
            Compose();
            TryOpen();
        }


        public void EmitDialogue(RichTextComponent[] cmps)
        {
            foreach (var elem in textElem.Components)
            {
                if (elem is LinkTextComponent linkComp) linkComp.Clickable = false;
            }

            textElem.AppendText(cmps);
            updateScrollbarBounds();
        }


        public override void OnKeyDown(KeyEvent args)
        {
            if (args.KeyCode >= (int)GlKeys.Number1 && args.KeyCode < (int)GlKeys.Number9)
            {
                int index = args.KeyCode - (int)GlKeys.Number1;
                int i = 0;
                foreach (var elem in textElem.Components)
                {
                    if (elem is LinkTextComponent linkComp && linkComp.Clickable)
                    {
                        if (i == index)
                        {
                            linkComp.Trigger();
                            args.Handled = true;
                            break;
                        }
                        i++;
                    }
                }
            }

            base.OnKeyDown(args);
        }

        public void Compose()
        {
            ClearComposers();

            CairoFont font = CairoFont.WhiteMediumText().WithFont(GuiStyle.DecorativeFontName).WithColor(GuiStyle.DiscoveryTextColor).WithStroke(GuiStyle.DialogBorderColor, 2).WithOrientation(EnumTextOrientation.Center);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);


            ElementBounds textBounds = ElementBounds.Fixed(0, 30, 600, 250);

            clipBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(3).WithFixedOffset(0, 0);

            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3 + textBounds.fixedWidth + 7).WithFixedWidth(20);

            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);

            string traderName = npcEntity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            string dlgTitle = Lang.Get("tradingwindow-" + npcEntity.Code.Path, traderName);

            SingleComposer =
                capi.Gui
                .CreateCompo("dialogue-" + npcEntity.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(dlgTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)

                .BeginClip(clipBounds)
                    .AddInset(insetBounds, 3)
                    .AddRichtext("", CairoFont.WhiteSmallText(), textBounds, "dialogueText")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")

                .AddSmallButton(Lang.Get("Goodbye!"), OnByeClicked, leftButton.FixedUnder(clipBounds, 20))
                .Compose()
            ;

            textElem = SingleComposer.GetRichtext("dialogueText");
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private bool OnByeClicked()
        {
            TryClose();
            return true;
        }


        void updateScrollbarBounds()
        {
            if (textElem == null) return;
            var scrollbarElem = SingleComposer.GetScrollbar("scrollbar");

            scrollbarElem.Bounds.CalcWorldBounds();
            scrollbarElem.SetHeights(
                (float)(clipBounds.fixedHeight),
                (float)(textElem.Bounds.fixedHeight)
            );
            scrollbarElem.ScrollToBottom();
        }

        private void OnNewScrollbarValue(float value)
        {
            textElem.Bounds.fixedY = 0 - value;
            textElem.Bounds.CalcWorldBounds();
        }
    }
}
