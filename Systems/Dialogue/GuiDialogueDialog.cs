using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#nullable disable

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

        public void ClearDialogue()
        {
            foreach (var cmp in textElem.Components)
            {
                cmp.Dispose();
            }
            textElem.SetNewText(System.Array.Empty<RichTextComponent>());
        }

        public void EmitDialogue(RichTextComponentBase[] cmps)
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

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            int w = 600;
            int h = 470;
            ElementBounds textBounds = ElementBounds.Fixed(0, 30, w, h);

            clipBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-2, -2);

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
                    .AddRichtext("", CairoFont.WhiteSmallText(), textBounds.WithFixedPadding(5).WithFixedSize(w-10, h-10), "dialogueText")
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

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
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
        
        public override void OnFinalizeFrame(float dt)
        {
            base.OnFinalizeFrame(dt);

            var playerPos = capi.World.Player.Entity.Pos;

            if (IsOpened() && playerPos.SquareDistanceTo(npcEntity.Pos) > EntityBehaviorConversable.StopTalkRangeSq)
            {
                // Because we cant do it in here
                capi.Event.EnqueueMainThreadTask(() => TryClose(), "closedlg");
            }
        }
    }
}
