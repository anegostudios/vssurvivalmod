using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using static System.Net.Mime.MediaTypeNames;

namespace Vintagestory.GameContent
{

    public class GuiDialogJournal : GuiDialogGeneric
    {
        List<JournalEntry> journalitems = new List<JournalEntry>();
        string[] pages;
        int currentLoreItemIndex;
        int page;

        ElementBounds containerBounds;


        public override string ToggleKeyCombinationCode
        {
            get { return null; }
        }

        public GuiDialogJournal(List<JournalEntry> journalitems, ICoreClientAPI capi) : base(Lang.Get("Journal"), capi)
        {
            this.journalitems = journalitems;
        }
        

        void ComposeDialog()
        {
            double elemToDlgPad = GuiStyle.ElementToDialogPadding;

            ElementBounds button = ElementBounds.Fixed(3, 3, 283, 25).WithFixedPadding(10, 2);

            ElementBounds lorelistBounds = ElementBounds.Fixed(0, 32, 285, 500);

            ElementBounds clippingBounds = lorelistBounds.ForkBoundingParent();
            ElementBounds insetBounds = lorelistBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);
            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(lorelistBounds.fixedWidth + 7).WithFixedWidth(20);


            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(6);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, clippingBounds, scrollbarBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle).WithFixedAlignmentOffset(5,0);

            ClearComposers();

            Composers["loreList"] =
                capi.Gui
                .CreateCompo("loreList", dialogBounds)
                .AddShadedDialogBG(ElementBounds.Fill)
                .AddDialogTitleBar(Lang.Get("Journal Inventory"), CloseIconPressed)
                .BeginChildElements(bgBounds)
                    .AddInset(insetBounds, 3)
                    .BeginClip(clippingBounds)
                    .AddContainer(containerBounds = clippingBounds.ForkContainingChild(0, 0, 0, -3), "journallist")
            ;

            var container = Composers["loreList"].GetContainer("journallist");

            CairoFont hoverFont = CairoFont.WhiteSmallText().Clone().WithColor(GuiStyle.ActiveButtonTextColor);

            for (int i = 0; i < journalitems.Count; i++)
            {
                int page = i;
                GuiElementTextButton elem = new GuiElementTextButton(capi, Lang.Get(journalitems[i].Title), CairoFont.WhiteSmallText(), hoverFont, () => { return onClickItem(page); }, button, EnumButtonStyle.Small);
                elem.SetOrientation(EnumTextOrientation.Left);
                container.Add(elem);
                button = button.BelowCopy();
            }

            if (journalitems.Count == 0)
            {
                string vtmlCode = "<i>" + Lang.Get("No lore found. Collect lore in the world to fill this list!.") + "</i>";
                container.Add(new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, vtmlCode, CairoFont.WhiteSmallText()), button));
            }


            Composers["loreList"]
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
                .EndChildElements()
                .Compose()
            ;

            containerBounds.CalcWorldBounds();
            clippingBounds.CalcWorldBounds();

            Composers["loreList"].GetScrollbar("scrollbar").SetHeights(
                (float)(clippingBounds.fixedHeight),
                (float)(containerBounds.fixedHeight)
            );
        }


        private bool onClickItem(int page)
        {
            currentLoreItemIndex = page;
            this.page = 0;

            CairoFont font = CairoFont.WhiteDetailText().WithFontSize(17).WithLineHeightMultiplier(1.15f);
            TextDrawUtil prober = new TextDrawUtil();
            StringBuilder fulltext = new StringBuilder();

            JournalEntry entry = journalitems[currentLoreItemIndex];

            for (int p = 0; p < entry.Chapters.Count; p++)
            {
                if (p > 0) fulltext.AppendLine();
                fulltext.Append(Lang.Get(entry.Chapters[p].Text));
            }

            pages = Paginate(fulltext.ToString(), font, GuiElement.scaled(629), GuiElement.scaled(450));
            

            double elemToDlgPad = GuiStyle.ElementToDialogPadding;

            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 630, 450);
            ElementBounds dialogBounds = textBounds.ForkBoundingParent(elemToDlgPad, elemToDlgPad + 20, elemToDlgPad, elemToDlgPad + 30).WithAlignment(EnumDialogArea.LeftMiddle);
            dialogBounds.fixedX = 350;
            

            Composers["loreItem"] =
                capi.Gui
                .CreateCompo("loreItem", dialogBounds)
                .AddShadedDialogBG(ElementBounds.Fill, true)
                .AddDialogTitleBar(Lang.Get(journalitems[page].Title), CloseIconPressedLoreItem)
                .AddRichtext(pages[0], font, textBounds, "page")
                .AddDynamicText("1 / " + pages.Length, CairoFont.WhiteSmallishText().WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(250, 500, 100, 30), "currentpage") 
                .AddButton(Lang.Get("Previous Page"), OnPrevPage, ElementBounds.Fixed(17, 500, 100, 23).WithFixedPadding(10, 4), CairoFont.WhiteSmallishText())
                .AddButton(Lang.Get("Next Page"), OnNextPage, ElementBounds.Fixed(520, 500, 100, 23).WithFixedPadding(10, 4), CairoFont.WhiteSmallishText())
                .Compose()
            ;

            return true;
        }


        private string[] Paginate(string fullText, CairoFont font, double pageWidth, double pageHeight)
        {
            TextDrawUtil textUtil = new TextDrawUtil();
            Stack<string> lines = new Stack<string>();
            IEnumerable<TextLine> textlines = textUtil.Lineize(font, fullText, pageWidth).Reverse();
            foreach (var val in textlines) lines.Push(val.Text);


            double lineheight = textUtil.GetLineHeight(font);
            int maxlinesPerPage = (int)(pageHeight / lineheight);

            List<string> pagesTemp = new List<string>();
            StringBuilder pageBuilder = new StringBuilder();

            while (lines.Count > 0)
            {
                int currentPageLines = 0;

                while (currentPageLines < maxlinesPerPage && lines.Count > 0)
                {
                    string line = lines.Pop();
                    string[] parts = line.Split(new string[] { "___NEWPAGE___" }, 2, StringSplitOptions.None);

                    if (parts.Length > 1)
                    {
                        pageBuilder.AppendLine(parts[0]);
                        if (parts[1].Length > 0)
                        {
                            lines.Push(parts[1]);
                        }
                        break;
                    }

                    currentPageLines++;
                    pageBuilder.AppendLine(line);
                }

                string pageText = pageBuilder.ToString().TrimEnd();

                if (pageText.Length > 0)
                {
                    pagesTemp.Add(pageText);
                }

                pageBuilder.Clear();
            }

            return pagesTemp.ToArray();
        }

        private bool OnNextPage()
        {
            CairoFont font = CairoFont.WhiteDetailText().WithFontSize(17).WithLineHeightMultiplier(1.15f);
            page = Math.Min(pages.Length - 1, page + 1);
            Composers["loreItem"].GetRichtext("page").SetNewText(pages[page], font);
            Composers["loreItem"].GetDynamicText("currentpage").SetNewText((page + 1) + " / " + pages.Length);
            return true;
        }

        private bool OnPrevPage()
        {
            CairoFont font = CairoFont.WhiteDetailText().WithFontSize(17).WithLineHeightMultiplier(1.15f);
            page = Math.Max(0, page - 1);
            Composers["loreItem"].GetRichtext("page").SetNewText(pages[page], font);
            Composers["loreItem"].GetDynamicText("currentpage").SetNewText((page + 1) + " / " + pages.Length);
            return true;
        }

        public override void OnGuiOpened()
        {
            ComposeDialog();
        }

        private void CloseIconPressed()
        {
            TryClose();
        }

        private void CloseIconPressedLoreItem()
        {
            Composers.Remove("loreItem");
        }

        private void OnNewScrollbarvalue(float value)
        {
            ElementBounds bounds = Composers["loreList"].GetContainer("journallist").Bounds;
            bounds.fixedY = 0 - value;
            bounds.CalcWorldBounds();
        }


        public override bool PrefersUngrabbedMouse => false;

        public void ReloadValues()
        {
            
        }
    }
}
