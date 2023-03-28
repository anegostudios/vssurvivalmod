using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public delegate void TranscribePressedDelegate(string pageText, string pageTitle, int pageNumber);

    public class GuiDialogReadonlyBook : GuiDialogGeneric
    {
        public string AllPagesText;
        public string Title;
        protected int curPage = 0;
        protected int maxLines = 20;
        protected int maxWidth = 400;

        public List<PagePosition> Pages = new List<PagePosition>();
        protected CairoFont font = CairoFont.TextInput().WithFontSize(18);
        public double textAreaWidth => GuiElement.scaled(maxWidth);
        TranscribePressedDelegate onTranscribedPressed;

        public GuiDialogReadonlyBook(ItemStack bookStack, ICoreClientAPI capi, TranscribePressedDelegate onTranscribedPressed = null) : base("", capi)
        {
            this.onTranscribedPressed = onTranscribedPressed;

            if (bookStack.Attributes.HasAttribute("textCodes"))
            {
                AllPagesText = string.Join("\n", (bookStack.Attributes["textCodes"] as StringArrayAttribute).value.Select(code => Lang.Get(code)));
                Title = Lang.Get(bookStack.Attributes.GetString("titleCode", ""));
            } else
            {
                AllPagesText = bookStack.Attributes.GetString("text", "");
                Title = bookStack.Attributes.GetString("title", "");
            }

            Pages = Pageize(AllPagesText, font, textAreaWidth, maxLines);

            Compose();
        }

        protected List<PagePosition> Pageize(string fullText, CairoFont font, double pageWidth, int maxLinesPerPage)
        {
            fullText = fullText.Replace("\r", "");

            TextDrawUtil textUtil = new TextDrawUtil();
            Stack<string> lines = new Stack<string>();
            IEnumerable<TextLine> textlines = textUtil.Lineize(font, fullText, pageWidth, EnumLinebreakBehavior.Default, true).Reverse();
            foreach (var val in textlines) lines.Push(val.Text);

            List<PagePosition> pages = new List<PagePosition>();
            int start = 0;
            int curLen = 0;

            while (lines.Count > 0)
            {
                int currentPageLines = 0;

                while (currentPageLines < maxLinesPerPage && lines.Count > 0)
                {
                    string line = lines.Pop();
                    string[] parts = line.Split(new string[] { "___NEWPAGE___" }, 2, StringSplitOptions.None);

                    if (parts.Length > 1)
                    {
                        curLen += parts[0].Length;
                        if (parts[1].Length > 0)
                        {
                            lines.Push(parts[1]);
                        }
                        break;
                    }

                    currentPageLines++;
                    curLen += line.Length;
                }

                if (currentPageLines > 0)
                {
                    pages.Add(new PagePosition() { Start = start, Length = curLen, LineCount = currentPageLines });
                    start += curLen;
                }

                curLen = 0;
            }

            if (pages.Count == 0) pages.Add(new PagePosition() { Start = 0, Length = 0 });


            return pages;
        }


        protected virtual void Compose()
        {
            double lineHeight = font.GetFontExtents().Height * font.LineHeightMultiplier / RuntimeEnv.GUIScale;
            ElementBounds textAreaBounds = ElementBounds.Fixed(0, 30, maxWidth, maxLines * lineHeight + 1);

            ElementBounds prevButtonBounds = ElementBounds.FixedSize(60, 30).FixedUnder(textAreaBounds, 18 + 5).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
            ElementBounds pageLabelBounds = ElementBounds.FixedSize(80, 30).FixedUnder(textAreaBounds, 18 + 2 * 5 + 5).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPadding(10, 2);
            ElementBounds nextButtonBounds = ElementBounds.FixedSize(60, 30).FixedUnder(textAreaBounds, 18+ 5).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);

            ElementBounds closeButton = ElementBounds.FixedSize(0, 0).FixedUnder(prevButtonBounds, 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10, 2);
            ElementBounds saveButtonBounds = ElementBounds.FixedSize(0, 0).FixedUnder(nextButtonBounds, 25).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(10, 2);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(closeButton);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0)
            ;

            SingleComposer = capi.Gui
                .CreateCompo("blockentitytexteditordialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Title, () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddRichtext("", font, textAreaBounds, "text")

                    .AddSmallButton(Lang.Get("<"), prevPage, prevButtonBounds)
                    .AddDynamicText("1/1", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center), pageLabelBounds, "pageNum")
                    .AddSmallButton(Lang.Get(">"), nextPage, nextButtonBounds)

                    .AddSmallButton(Lang.Get("Close"), () => TryClose(), closeButton)
                    .AddIf(onTranscribedPressed != null)
                        .AddSmallButton(Lang.Get("Transcribe"), onButtonTranscribe, saveButtonBounds)
                    .EndIf()

                .EndChildElements()
                .Compose()
            ;

            updatePage();
        }


        private bool onButtonTranscribe()
        {
            onTranscribedPressed(CurPageText, Title, curPage);
            return true;
        }

        private bool nextPage()
        {
            curPage = Math.Min(curPage + 1, Pages.Count - 1);
            updatePage();
            return true;
        }

        private bool prevPage()
        {
            curPage = Math.Max(curPage - 1, 0);
            updatePage();
            return true;
        }

        protected void updatePage(bool setCaretPosToEnd = true)
        {
            string text = CurPageText;

            SingleComposer.GetDynamicText("pageNum").SetNewText((curPage + 1) + "/" + Pages.Count);

            var elem = SingleComposer.GetElement("text");

            if (elem is GuiElementTextArea textArea) textArea.SetValue(text, setCaretPosToEnd);
            else (elem as GuiElementRichtext).SetNewText(text, font);
        }

        protected bool KeyboardNavigation = true;


        public string CurPageText
        {
            get
            {
                if (curPage >= Pages.Count) return "";
                return Pages[curPage].Start >= AllPagesText.Length ? "" : AllPagesText.Substring(Pages[curPage].Start, Math.Min(AllPagesText.Length - Pages[curPage].Start, Pages[curPage].Length)).TrimStart(' ');
            }
        }

        public override void OnKeyDown(KeyEvent args)
        {
            base.OnKeyDown(args);

            if (KeyboardNavigation)
            {

                if (args.KeyCode == (int)GlKeys.Left || args.KeyCode == (int)GlKeys.PageUp)
                {
                    prevPage();
                }

                if (args.KeyCode == (int)GlKeys.Right || args.KeyCode == (int)GlKeys.PageDown)
                {
                    nextPage();
                }
            }
        }
    }
}
