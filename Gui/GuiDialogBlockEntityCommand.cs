using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [ProtoContract()]
    public class BlockEntityCommandPacket
    {
        [ProtoMember(1)]
        public string Commands;
        [ProtoMember(2)]
        public bool Silent;
    }

    public class GuiDialogBlockEntityCommand : GuiDialogBlockEntity
    {
        public GuiDialogBlockEntityCommand(BlockPos BlockEntityPosition, string command, bool silent, ICoreClientAPI capi, string title) : base("Command block", BlockEntityPosition, capi)
        {
            double pad = GuiElementItemSlotGrid.unscaledSlotPadding;

            int spacing = 5;

            double size = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGrid.unscaledSlotPadding;

            double innerWidth = 700;

            // 1.2. Name and Hotkey
            double halfWidth = innerWidth / 2 - 5;
            
            // 1.3. Commands text
            ElementBounds commmandsBounds = ElementBounds.Fixed(0, 30, innerWidth, 30);

            // 1.3. Command textarea
            ElementBounds textAreaBounds = ElementBounds.Fixed(0, 0, innerWidth - 20, 200);

            // 1.3.2. Clipping bounds for textarea
            ElementBounds clippingBounds = ElementBounds.Fixed(0, 0, innerWidth - 20 - 1, 200 - 1).FixedUnder(commmandsBounds, spacing - 10);

            // 1.3.3 Scrollbar right of textarea
            ElementBounds scrollbarBounds = clippingBounds.CopyOffsetedSibling(clippingBounds.fixedWidth + 6, -1).WithFixedWidth(20).FixedGrow(0, 2);

            // 1.4. Clear Macro button
            ElementBounds toClipboardBounds = ElementBounds.FixedSize(0, 0).FixedUnder(clippingBounds, 2 + 2 * spacing).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);
            ElementBounds cancelBounds = ElementBounds.FixedSize(0, 0).FixedUnder(toClipboardBounds, 20 + 24 + 2 * spacing).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
            ElementBounds saveBounds = ElementBounds.FixedSize(0, 0).FixedUnder(toClipboardBounds, 20 + 24 + 2 * spacing).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);

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
                    .AddStaticText("Commands", CairoFont.WhiteSmallText(), commmandsBounds)
                    .BeginClip(clippingBounds)
                        .AddTextArea(textAreaBounds, OnCommandCodeChanged, CairoFont.TextInput().WithFontSize(16), "commands")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewCmdScrollbarvalue, scrollbarBounds, "scrollbar")

                    .AddSwitch(null, textAreaBounds.BelowCopy(0, 10).WithFixedSize(30, 30), "silentSwitch", 25, 3)
                    .AddStaticText(Lang.Get("Execute silently"), CairoFont.WhiteSmallText(), textAreaBounds.BelowCopy(30, 13).WithFixedSize(150, 30))
                    .AddSmallButton(Lang.Get("Copy to clipboard"), OnCopy, toClipboardBounds)

                    .AddSmallButton(Lang.Get("Cancel"), OnCancel, cancelBounds)
                    .AddSmallButton(Lang.Get("Save"), OnSave, saveBounds)
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetTextArea("commands").SetValue(command);
            SingleComposer.GetTextArea("commands").OnCursorMoved = OnTextAreaCursorMoved;
            SingleComposer.GetSwitch("silentSwitch").On = silent;

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
            bool silent = SingleComposer.GetSwitch("silentSwitch").On;
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, 12, SerializerUtil.Serialize(new BlockEntityCommandPacket() { Commands = commands, Silent = silent }));
            TryClose();
            return true;
        }

        private void OnCommandCodeChanged(string t1)
        {
            
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
