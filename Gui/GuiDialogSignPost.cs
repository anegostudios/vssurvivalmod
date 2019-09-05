using Cairo;
using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class GuiDialogSignPost : GuiDialogGeneric
    {
        BlockPos blockEntityPos;
        public API.Common.Action<string[]> OnTextChanged;
        public API.Common.Action OnCloseCancel;

        bool didSave;

        CairoFont signPostFont;

        public GuiDialogSignPost(string DialogTitle, BlockPos blockEntityPos, string[] textByCardinalDirection, ICoreClientAPI capi, CairoFont signPostFont) : base(DialogTitle, capi)
        {
            this.signPostFont = signPostFont;
            this.blockEntityPos = blockEntityPos;

            ElementBounds line = ElementBounds.Fixed(0, 0, 150, 20);
            ElementBounds input = ElementBounds.Fixed(0, 15, 150, 25);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftTop)
                .WithFixedAlignmentOffset(60 + GuiStyle.DialogToScreenPadding, GuiStyle.DialogToScreenPadding);


            float inputLineY = 27;
            float textLineY = 32;
            float width = 250;

            SingleComposer = capi.Gui
                .CreateCompo("blockentitytexteditordialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)

                    .AddStaticText("North", CairoFont.WhiteDetailText(), line = line.BelowCopy(0, 0).WithFixedWidth(width))
                    .AddTextInput(input = input.BelowCopy(0, 0).WithFixedWidth(width), OnTextChangedDlg, CairoFont.WhiteSmallText(), "text0")

                    .AddStaticText("Northeast", CairoFont.WhiteDetailText(), line = line.BelowCopy(0, textLineY).WithFixedWidth(width))
                    .AddTextInput(input = input.BelowCopy(0, inputLineY).WithFixedWidth(width), OnTextChangedDlg, CairoFont.WhiteSmallText(), "text1")

                    .AddStaticText("East", CairoFont.WhiteDetailText(), line = line.BelowCopy(0, textLineY).WithFixedWidth(width))
                    .AddTextInput(input = input.BelowCopy(0, inputLineY).WithFixedWidth(width), OnTextChangedDlg, CairoFont.WhiteSmallText(), "text2")

                    .AddStaticText("Southeast", CairoFont.WhiteDetailText(), line = line.BelowCopy(0, textLineY).WithFixedWidth(width))
                    .AddTextInput(input = input.BelowCopy(0, inputLineY).WithFixedWidth(width), OnTextChangedDlg, CairoFont.WhiteSmallText(), "text3")

                    .AddStaticText("South", CairoFont.WhiteDetailText(), line = line.BelowCopy(0, textLineY).WithFixedWidth(width))
                    .AddTextInput(input = input.BelowCopy(0, inputLineY).WithFixedWidth(width), OnTextChangedDlg, CairoFont.WhiteSmallText(), "text4")

                    .AddStaticText("Southwest", CairoFont.WhiteDetailText(), line = line.BelowCopy(0, textLineY).WithFixedWidth(width))
                    .AddTextInput(input = input.BelowCopy(0, inputLineY).WithFixedWidth(width), OnTextChangedDlg, CairoFont.WhiteSmallText(), "text5")

                    .AddStaticText("West", CairoFont.WhiteDetailText(), line = line.BelowCopy(0, textLineY).WithFixedWidth(width))
                    .AddTextInput(input = input.BelowCopy(0, inputLineY).WithFixedWidth(width), OnTextChangedDlg, CairoFont.WhiteSmallText(), "text6")

                    .AddStaticText("Northwest", CairoFont.WhiteDetailText(), line = line.BelowCopy(0, textLineY).WithFixedWidth(width))
                    .AddTextInput(input = input.BelowCopy(0, inputLineY).WithFixedWidth(width), OnTextChangedDlg, CairoFont.WhiteSmallText(), "text7")

                    .AddSmallButton(Lang.Get("Cancel"), OnButtonCancel, input = input.BelowCopy(0, 20).WithFixedSize(100, 20).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                    .AddSmallButton(Lang.Get("Save"), OnButtonSave, input = input.FlatCopy().WithFixedSize(100, 20).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2), EnumButtonStyle.Normal, EnumTextOrientation.Center)
                .EndChildElements()
                .Compose()
            ;

            for (int i = 0; i < 8; i++)
            {
                GuiElementTextInput texinput = SingleComposer.GetTextInput("text" + i);
                texinput.SetValue(textByCardinalDirection[i]);
            }
        }


        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
        }

        bool ignorechange;
        private void OnTextChangedDlg(string text)
        {
            if (ignorechange) return;
            ignorechange = true;

            ImageSurface surface = new ImageSurface(Format.Argb32, 1, 1);
            Context ctx = new Context(surface);
            signPostFont.SetupContext(ctx);

            string[] textByCardinal = new string[8];
            for (int i = 0; i < 8; i++)
            {
                GuiElementTextInput texinput = SingleComposer.GetTextInput("text" + i);
                textByCardinal[i] = texinput.GetText();
                if (textByCardinal[i] == null) textByCardinal[i] = "";

                int j = 0;
                while (ctx.TextExtents(textByCardinal[i]).Width > 200 && j++ < 100)
                {
                    textByCardinal[i] = textByCardinal[i].Substring(0, textByCardinal[i].Length-1);
                }

                texinput.SetValue(textByCardinal[i]);
            }

            OnTextChanged?.Invoke(textByCardinal);

            ignorechange = false;
        }
        

        private void OnTitleBarClose()
        {
            OnButtonCancel();
        }

        private bool OnButtonSave()
        {
            string[] textByCardinal = new string[8];
            for (int i = 0; i < 8; i++)
            {
                GuiElementTextInput texinput = SingleComposer.GetTextInput("text" + i);
                textByCardinal[i] = texinput.GetText();
            }

            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                for (int i = 0; i < 8; i++)
                {
                    writer.Write(textByCardinal[i]);
                }
                data = ms.ToArray();
            }

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

        public override bool PrefersUngrabbedMouse => false;

    }
}
