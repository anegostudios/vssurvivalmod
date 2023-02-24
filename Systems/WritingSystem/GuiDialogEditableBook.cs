using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    

    public class GuiDialogEditableBook : GuiDialogReadonlyBook
    {
        public bool DidSave;
        public bool DidSign;
        int maxPageCount;

        public GuiDialogEditableBook(ItemStack bookStack, ICoreClientAPI capi, int maxPageCount) : base(bookStack, capi)
        {
            this.maxPageCount = maxPageCount;
            KeyboardNavigation=false;
        }

        protected override void Compose()
        {
            ElementBounds titleBounds = ElementBounds.Fixed(0, 30, maxWidth, 24);
            ElementBounds textAreaBounds = ElementBounds.Fixed(0, 0, 300, maxLines * 21.2f).FixedUnder(titleBounds, 5);

            ElementBounds prevButtonBounds = ElementBounds.FixedSize(60, 30).FixedUnder(textAreaBounds, 5).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
            ElementBounds pageLabelBounds = ElementBounds.FixedSize(80, 30).FixedUnder(textAreaBounds, 2 * 5 + 7).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPadding(10, 2);
            ElementBounds nextButtonBounds = ElementBounds.FixedSize(60, 30).FixedUnder(textAreaBounds, 5).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);

            ElementBounds cancelButtonBounds = ElementBounds.FixedSize(0, 0).FixedUnder(prevButtonBounds, 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
            ElementBounds signButtonBounds = ElementBounds.FixedSize(0, 0).FixedUnder(prevButtonBounds, 25).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPadding(10, 2);
            ElementBounds saveButtonBounds = ElementBounds.FixedSize(0, 0).FixedUnder(nextButtonBounds, 25).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(cancelButtonBounds, saveButtonBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0)
            ;

            SingleComposer = capi.Gui
                .CreateCompo("blockentitytexteditordialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Edit book", OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddTextInput(titleBounds, null, CairoFont.TextInput().WithFontSize(18), "title")
                    .AddTextArea(textAreaBounds, onTextChanged, font, "text")

                    .AddSmallButton(Lang.Get("<"), OnPreviousPage, prevButtonBounds)
                    .AddDynamicText("1/1", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), pageLabelBounds, "pageNum")
                    .AddSmallButton(Lang.Get(">"), OnNextPage, nextButtonBounds)

                    .AddSmallButton(Lang.Get("Cancel"), OnButtonCancel, cancelButtonBounds)
                    .AddSmallButton(Lang.Get("Sign"), OnButtonSign, signButtonBounds)
                    .AddSmallButton(Lang.Get("Save"), OnButtonSave, saveButtonBounds)
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetTextInput("title").SetPlaceHolderText(Lang.Get("Book title"));
            SingleComposer.GetTextInput("title").SetValue(Title);

            SingleComposer.GetTextArea("text").OnCaretPositionChanged = onCaretPositionChanged;
            SingleComposer.GetTextArea("text").Autoheight = false;

            updatePage();   
        }

        private bool OnButtonSign()
        {
            new GuiDialogConfirm(capi, Lang.Get("Save and sign book now? It can not be edited afterwards."), onConfirmSign).TryOpen();
            return true;
        }

        private void onConfirmSign(bool ok)
        {
            if (ok)
            {
                StoreCurrentPage();
                Title = SingleComposer.GetTextInput("title").GetText();
                DidSign = true;
                DidSave = true;
                TryClose();
            }
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            SingleComposer.FocusElement(SingleComposer.GetTextArea("text").TabIndex);
        }

        private void OnTitleBarClose()
        {
            OnButtonCancel();
        }

        bool ignoreTextChange = false;
        private void onTextChanged(string text)
        {
            if (ignoreTextChange) return;

            ignoreTextChange = true;
            var elem = SingleComposer.GetTextArea("text");

            int posLine = elem.CaretPosLine;
            int posInLine = elem.CaretPosInLine;
            StoreCurrentPage();
            updatePage(false);
            elem.SetCaretPos(posInLine, posLine);

            ignoreTextChange = false;
        }

        private void onCaretPositionChanged(int posLine, int posInLine)
        {
            if (ignoreTextChange) return;
            ignoreTextChange = true;

            if (posLine >= maxLines)
            {
                var elem = SingleComposer.GetTextArea("text");
                StoreCurrentPage();
                curPage = Math.Min(curPage + 1, Pages.Count);
                updatePage();
                elem.SetCaretPos(posInLine, posLine - maxLines);
            }

            ignoreTextChange = false;
        }


        private bool OnButtonSave()
        {
            StoreCurrentPage();
            Title = SingleComposer.GetTextInput("title").GetText();
            DidSave = true;
            TryClose();
            return true;
        }

        private bool OnButtonCancel()
        {
            DidSave = false;
            TryClose();
            return true;
        }

        protected bool OnNextPage()
        {
            if (curPage >= maxPageCount) return true;

            if (curPage + 1 >= Pages.Count) return false;

            if (Pages.Count <= curPage + 1)
            {
                var lastPage = Pages[0];
                Pages.Add(new PagePosition() { Start = lastPage.Length + 1, Length = 1 });
                AllPagesText += "___NEWPAGE___";
            }

            ignoreTextChange = true;
            StoreCurrentPage();
            curPage = Math.Min(curPage + 1, Pages.Count);
            updatePage();
            ignoreTextChange = false;
            return true;
        }

        private void StoreCurrentPage()
        {
            var curPagePos = Pages[curPage];
            string pageText = SingleComposer.GetTextArea("text").GetText();
            AllPagesText = AllPagesText.Substring(0, curPagePos.Start) + pageText + AllPagesText.Substring(Math.Min(AllPagesText.Length, curPagePos.Start + curPagePos.Length));
            Pages = Pageize(AllPagesText, font, textAreaWidth, maxLines);
        }

        protected bool OnPreviousPage()
        {
            ignoreTextChange = true;
            StoreCurrentPage();
            curPage = Math.Max(0, curPage - 1);
            updatePage();
            ignoreTextChange = false;
            return true;
        }
    }
}
