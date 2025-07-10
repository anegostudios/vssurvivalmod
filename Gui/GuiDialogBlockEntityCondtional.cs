using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class GuiDialogBlockEntityConditional : GuiDialogBlockEntity
    {
        public GuiDialogBlockEntityConditional(BlockPos BlockEntityPosition, string command, bool latching, ICoreClientAPI capi, string title) : base("Conditional block", BlockEntityPosition, capi)
        {
            double pad = GuiElementItemSlotGrid.unscaledSlotPadding;

            int spacing = 5;

            double size = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGrid.unscaledSlotPadding;

            double innerWidth = 600;

            // 1.2. Name and Hotkey
            double halfWidth = innerWidth / 2 - 5;
            
            // 1.3. Commands text
            ElementBounds commmandsBounds = ElementBounds.Fixed(0, 30, innerWidth, 30);

            // 1.3. Command textarea
            ElementBounds textAreaBounds = ElementBounds.Fixed(0, 0, innerWidth - 20, 80);

            // 1.3.2. Clipping bounds for textarea
            ElementBounds clippingBounds = ElementBounds.Fixed(0, 0, innerWidth - 20 - 1, 80 - 1).FixedUnder(commmandsBounds, spacing - 10);

            // 1.3.3 Scrollbar right of textarea
            ElementBounds scrollbarBounds = clippingBounds.CopyOffsetedSibling(clippingBounds.fixedWidth + 6, -1).WithFixedWidth(20).FixedGrow(0, 2);

            // 1.4. Clear Macro button
            ElementBounds labelBounds = ElementBounds.Fixed(0, 0, innerWidth - 40, 20).FixedUnder(clippingBounds, 2 + 2 * spacing);
            ElementBounds resultBounds = ElementBounds.Fixed(0, 0, innerWidth - 20, 25).FixedUnder(labelBounds, 2);
            ElementBounds toClipboardBounds = ElementBounds.FixedSize(0, 0).FixedUnder(resultBounds, 4 + 2 * spacing).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);
            ElementBounds cancelBounds = ElementBounds.FixedSize(0, 0).FixedUnder(toClipboardBounds, 34 + 2 * spacing).WithFixedPadding(10, 2);
            ElementBounds saveBounds = ElementBounds.FixedSize(0, 0).FixedUnder(toClipboardBounds, 34 + 2 * spacing).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            if (SingleComposer != null) SingleComposer.Dispose();
            SingleComposer =
                capi.Gui
                .CreateCompo("commandeditordialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(title, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddStaticText(Lang.Get("Condition (e.g. e[type=gazelle,range=10])"), CairoFont.WhiteSmallText(), commmandsBounds)
                    .BeginClip(clippingBounds)
                        .AddTextArea(textAreaBounds, OnCommandCodeChanged, CairoFont.TextInput().WithFontSize(16), "commands")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewCmdScrollbarvalue, scrollbarBounds, "scrollbar")
                    .AddStaticText(Lang.Get("Condition syntax status"), CairoFont.WhiteSmallText(), labelBounds)
                    .AddInset(resultBounds)
                    .AddDynamicText("", CairoFont.WhiteSmallText(), resultBounds.ForkContainingChild(2,2,2,2), "result")
                    .AddSmallButton(Lang.Get("Cancel"), OnCancel, cancelBounds)
                    .AddSwitch(null, resultBounds.BelowCopy(0, 10).WithFixedSize(30, 30), "latchingSwitch", 25, 3)
                    .AddStaticText(Lang.Get("Latching"), CairoFont.WhiteSmallText(), resultBounds.BelowCopy(30, 13).WithFixedSize(150, 30))
                    .AddHoverText(Lang.Get("If latching is enabled, a repeatedly ticked Conditional Block only activates neibouring Command Block once, each time the condition changes"), CairoFont.WhiteSmallText(), 250, resultBounds.BelowCopy(25, 10).WithFixedSize(82, 25))
                    .AddSmallButton(Lang.Get("Copy to clipboard"), OnCopy, toClipboardBounds)
                    .AddSmallButton(Lang.Get("Save"), OnSave, saveBounds)
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetTextArea("commands").SetValue(command);
            SingleComposer.GetTextArea("commands").OnCursorMoved = OnTextAreaCursorMoved;
            SingleComposer.GetSwitch("latchingSwitch").On = latching;

            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)textAreaBounds.fixedHeight - 1, (float)textAreaBounds.fixedHeight
            );
            

            SingleComposer.UnfocusOwnElements();
        }

        private bool OnCopy()
        {
            capi.Input.ClipboardText = SingleComposer.GetTextArea("commands").GetText();
            return true;
        }

        private void OnNewCmdScrollbarvalue(float value)
        {
            GuiElementTextArea textArea = SingleComposer.GetTextArea("commands");

            textArea.Bounds.fixedY = 1 - value;
            textArea.Bounds.CalcWorldBounds();
        }

        private bool OnCancel()
        {
            TryClose();
            return true;
        }

        private bool OnSave()
        {
            string commands = SingleComposer.GetTextArea("commands").GetText();
            bool latching = SingleComposer.GetSwitch("latchingSwitch").On;
            capi.Network.SendBlockEntityPacket(BlockEntityPosition, 12, SerializerUtil.Serialize(new BlockEntityCommandPacket() { Commands = commands, Silent = latching }));
            TryClose();
            return true;
        }

        private void OnCommandCodeChanged(string t1)
        {
            string s = t1.Trim();
            if (s.Length == 0)
            {
                SingleComposer.GetDynamicText("result").SetNewText("");
                return;
            }

            string display = "Ok";
            ICommandArgumentParser test = s.StartsWith("isBlock") ? new IsBlockArgParser("cond", capi, true) : new EntitiesArgParser("test", capi, true);
            TextCommandCallingArgs packedArgs = new TextCommandCallingArgs()
            {
                Caller = new Caller()
                {
                    Type = EnumCallerType.Console,
                    CallerRole = "admin",
                    CallerPrivileges = new string[] { "*" },
                    FromChatGroupId = GlobalConstants.ConsoleGroup,
                    Pos = new Vec3d(0.5, 0.5, 0.5)
                },
                RawArgs = new CmdArgs(s)
            };
            EnumParseResult result = test.TryProcess(packedArgs);
            if (result != EnumParseResult.Good) display = test.LastErrorMessage;

            SingleComposer.GetDynamicText("result").SetNewText(display);
        }


        private void OnTextAreaCursorMoved(double posX, double posY)
        {
            double lineHeight = SingleComposer.GetTextArea("commands").Font.GetFontExtents().Height;

            SingleComposer.GetScrollbar("scrollbar").EnsureVisible(posX, posY);
            SingleComposer.GetScrollbar("scrollbar").EnsureVisible(posX, posY + lineHeight + 5);
        }


        private void OnTitleBarClose()
        {
            TryClose();
        }

        
        public override string ToggleKeyCombinationCode
        {
            get { return null; }
        }

    }
}
