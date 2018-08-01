using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class GuiDialogBlockEntityTextInput : GuiDialogGeneric
    {
        double textareaFixedY;
        BlockPos blockEntityPos;

        public GuiDialogBlockEntityTextInput(string DialogTitle, BlockPos blockEntityPos, string text, ICoreClientAPI capi) : base(DialogTitle, capi)
        {
            this.blockEntityPos = blockEntityPos;

            ElementBounds textAreaBounds = ElementBounds.Fixed(0, 0, 300, 150);
            textareaFixedY = textAreaBounds.fixedY;

            // Clipping bounds for textarea
            ElementBounds clippingBounds = textAreaBounds.ForkBoundingParent().WithFixedPosition(0, 30);

            ElementBounds scrollbarBounds = clippingBounds.CopyOffsetedSibling(textAreaBounds.fixedWidth + 3).WithFixedWidth(20);

            ElementBounds cancelButtonBounds = ElementBounds.FixedSize(0, 0).FixedUnder(clippingBounds, 2 * 5).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
            ElementBounds saveButtonBounds = ElementBounds.FixedSize(0, 0).FixedUnder(clippingBounds, 2 * 5).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);


            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(ElementGeometrics.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(clippingBounds, scrollbarBounds, cancelButtonBounds, saveButtonBounds); //textAreaBounds, 

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-ElementGeometrics.DialogToScreenPadding, 0);


            SingleComposer = capi.Gui
                .CreateCompo("blockentitytexteditordialog", dialogBounds, false)
                .AddDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .BeginClip(clippingBounds)
                    .AddTextArea(textAreaBounds, OnTextChanged, CairoFont.TextInput(), "text")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
                    .AddSmallButton("Cancel", OnButtonCancel, cancelButtonBounds)
                    .AddSmallButton("Save", OnButtonSave, saveButtonBounds)
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetTextArea("text").SetMaxLines(4);

            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)textAreaBounds.fixedHeight, (float)textAreaBounds.fixedHeight
            );

            if (text.Length > 0)
            {
                SingleComposer.GetTextArea("text").SetValue(text);
            }

            
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            SingleComposer.FocusElement(SingleComposer.GetTextArea("text").TabIndex);
        }

        private void OnTextChanged(string value)
        {
            GuiElementTextArea textArea = SingleComposer.GetTextArea("text");
            SingleComposer.GetScrollbar("scrollbar").SetNewTotalHeight((float)textArea.Bounds.fixedHeight);
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
            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                writer.Write(textArea.GetText());
                data = ms.ToArray();
            }

            capi.Network.SendBlockEntityPacket(blockEntityPos.X, blockEntityPos.Y, blockEntityPos.Z, 1000, data);

            TryClose();
            return true;
        }

        private bool OnButtonCancel()
        {
            TryClose();
            return true;
        }




    }
}
