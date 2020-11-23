using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{

    public class GuiDialogJournal : GuiDialogGeneric
    {
        List<JournalEntry> journalitems = new List<JournalEntry>();
        string[] pages;
        int currentLoreItemIndex;
        int page;


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

            ElementBounds button = ElementBounds.Fixed(15, 40, 290, 30).WithFixedPadding(10, 2);

            ElementBounds dialogBounds =
                ElementBounds.Fixed(EnumDialogArea.LeftMiddle, 30, 0, 250, 500)
                .ForkBoundingParent(elemToDlgPad, elemToDlgPad, elemToDlgPad, elemToDlgPad)
            ;

            ClearComposers();

            Composers["loreList"] =
                capi.Gui
                .CreateCompo("loreList", dialogBounds)
                .AddShadedDialogBG(ElementBounds.Fill)
                .AddDialogTitleBar(Lang.Get("Journal Inventory"), CloseIconPressed)
            ;

            for (int i = 0; i < journalitems.Count; i++)
            {
                int page = i;
                Composers["loreList"].AddButton(Lang.Get(journalitems[i].Title), () => { return onClickItem(page); }, button, CairoFont.WhiteSmallText(), EnumButtonStyle.None, EnumTextOrientation.Left, "button-"+i);

                Composers["loreList"].GetButton("button-" + i).PlaySound = false;

                button = button.BelowCopy();
            }


            Composers["loreList"].Compose();
        }


        private bool onClickItem(int i)
        {
            currentLoreItemIndex = i;
            page = 0;

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
                .AddDialogTitleBar(Lang.Get(journalitems[i].Title), CloseIconPressedLoreItem)
                .AddRichtext(pages[0], font, textBounds, "page")
                .AddDynamicText("1 / " + pages.Length, CairoFont.WhiteSmallishText(), EnumTextOrientation.Center, ElementBounds.Fixed(250, 500, 100, 30), "currentpage") 
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
            //ElementBounds bounds = journalInvDialog.GetSlotGrid("slotgrid").bounds;
            //bounds.fixedY = 10 - GuiElementItemSlotGrid.unscaledSlotPadding - value;
            //bounds.calcWorldBounds();
        }


        public override bool PrefersUngrabbedMouse => false;

        public void ReloadValues()
        {
            
        }
    }
}
