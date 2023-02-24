using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class GuiDialogBlockEntityTextInput : GuiDialogGeneric
    {
        double textareaFixedY;
        BlockPos blockEntityPos;
        public Action<string> OnTextChanged;
        public Action OnCloseCancel;
        public float FontSize;

        bool didSave;

        TextAreaConfig signConfig;

        public GuiDialogBlockEntityTextInput(string DialogTitle, BlockPos blockEntityPos, string text, ICoreClientAPI capi, TextAreaConfig signConfig) : base(DialogTitle, capi)
        {
            if (signConfig == null) signConfig = new TextAreaConfig();

            this.signConfig = signConfig;

            FontSize = signConfig.FontSize;
            this.blockEntityPos = blockEntityPos;

            ElementBounds textAreaBounds = ElementBounds.Fixed(0, 0, signConfig.MaxWidth, signConfig.MaxHeight);
            textareaFixedY = textAreaBounds.fixedY;

            // Clipping bounds for textarea
            ElementBounds clippingBounds = textAreaBounds.ForkBoundingParent().WithFixedPosition(0, 30);

            ElementBounds scrollbarBounds = clippingBounds.CopyOffsetedSibling(textAreaBounds.fixedWidth + 3).WithFixedWidth(20);

            ElementBounds cancelButtonBounds = ElementBounds.FixedSize(0, 0).FixedUnder(clippingBounds, 2 * 5).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(8, 2).WithFixedAlignmentOffset(-1, 0);
            ElementBounds fontSizeBounds = ElementBounds.FixedSize(45, 22).FixedUnder(clippingBounds, 2 * 5).WithAlignment(EnumDialogArea.CenterFixed).WithFixedAlignmentOffset(3, 0);
            ElementBounds saveButtonBounds = ElementBounds.FixedSize(0, 0).FixedUnder(clippingBounds, 2 * 5).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(8, 2);


            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(clippingBounds, scrollbarBounds, cancelButtonBounds, saveButtonBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            var font = CairoFont.TextInput().WithFontSize(signConfig.FontSize).WithFont(signConfig.FontName);
            if (signConfig.BoldFont) font.WithWeight(Cairo.FontWeight.Bold);
            font.LineHeightMultiplier = 0.9;

            string[] sizes = new string[] { "14", "18", "20", "24", "28", "32", "36", "40"};

            SingleComposer = capi.Gui
                .CreateCompo("blockentitytexteditordialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .BeginClip(clippingBounds)
                    .AddTextArea(textAreaBounds, OnTextAreaChanged, font, "text")
                    .EndClip()
                    .AddIf(signConfig.WithScrollbar)
                        .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
                    .EndIf()
                    .AddSmallButton(Lang.Get("Cancel"), OnButtonCancel, cancelButtonBounds)
                    .AddDropDown(sizes, sizes, sizes.IndexOf(""+signConfig.FontSize), onfontsizechanged, fontSizeBounds)
                    .AddSmallButton(Lang.Get("Save"), OnButtonSave, saveButtonBounds)
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetTextArea("text").SetMaxHeight(signConfig.MaxHeight);

            if (signConfig.WithScrollbar)
            {
                SingleComposer.GetScrollbar("scrollbar").SetHeights(
                    (float)textAreaBounds.fixedHeight, (float)textAreaBounds.fixedHeight
                );
            }

            if (text.Length > 0)
            {
                SingleComposer.GetTextArea("text").SetValue(text);
            }

            
        }

        private void onfontsizechanged(string code, bool selected)
        {
            var textArea = SingleComposer.GetTextArea("text");
            string text = textArea.GetText();
            textArea.SetFont(textArea.Font.Clone().WithFontSize(FontSize = code.ToInt()));
            textArea.Font.WithFontSize(FontSize = code.ToInt());
            textArea.SetMaxHeight(signConfig.MaxHeight);
            textArea.SetValue(text);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            SingleComposer.FocusElement(SingleComposer.GetTextArea("text").TabIndex);
        }

        private void OnTextAreaChanged(string value)
        {
            GuiElementTextArea textArea = SingleComposer.GetTextArea("text");
            if (signConfig.WithScrollbar) SingleComposer.GetScrollbar("scrollbar").SetNewTotalHeight((float)textArea.Bounds.fixedHeight);

            OnTextChanged?.Invoke(textArea.GetText());
        }

        private void OnNewScrollbarvalue(float value)
        {
            GuiElementTextArea textArea = SingleComposer.GetTextArea("text");

            textArea.Bounds.fixedY = 3 + textareaFixedY - value;
            textArea.Bounds.CalcWorldBounds();
        }


        private void OnTitleBarClose()
        {
            OnButtonCancel();
        }

        private bool OnButtonSave()
        {
            GuiElementTextArea textArea = SingleComposer.GetTextArea("text");
            string text = textArea.GetText();
            byte[] data = SerializerUtil.Serialize(new EditSignPacket()
            {
                Text = text,
                FontSize = this.FontSize
            });

            capi.Network.SendBlockEntityPacket(blockEntityPos.X, blockEntityPos.Y, blockEntityPos.Z, (int)EnumSignPacketId.SaveText, data);
            didSave = true;
            TryClose();
            return true;
        }

        private bool OnButtonCancel()
        {
            TryClose();
            return true;
        }

        public override void OnGuiClosed()
        {
            if (!didSave) OnCloseCancel?.Invoke();
            base.OnGuiClosed();
        }




    }
}
