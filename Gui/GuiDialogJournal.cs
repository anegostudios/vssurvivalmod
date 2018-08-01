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
    public class JournalItem
    {
        public string Title;
        public string[] Pieces;
    }

    public class GuiDialogJournal : GuiDialogGeneric
    {
        List<JournalItem> journalitems = new List<JournalItem>();
        string[] pages;
        int currentLoreItemIndex;
        int page;


        public override string ToggleKeyCombinationCode
        {
            get { return null; }
        }

        public GuiDialogJournal(TreeAttribute tree, ICoreClientAPI capi) : base("Journal", capi)
        {
            foreach (var val in tree)
            {
                string title = val.Key;
                string[] pieces = (val.Value as StringArrayAttribute).value;
                journalitems.Add(new JournalItem() { Title = title, Pieces = pieces });
            }
        }
        

        void ComposeDialog()
        {
            double elemToDlgPad = ElementGeometrics.ElementToDialogPadding;

            ElementBounds button = ElementBounds.Fixed(15, 40, 290, 30).WithFixedPadding(10, 2);

            ElementBounds dialogBounds =
                ElementBounds.Fixed(EnumDialogArea.LeftMiddle, 30, 0, 250, 500)
                .ForkBoundingParent(elemToDlgPad, elemToDlgPad, elemToDlgPad, elemToDlgPad)
            ;

            DialogComposers["loreList"] =
                capi.Gui
                .CreateCompo("loreList", dialogBounds, false)
                .AddDialogBG(ElementBounds.Fill)
                .AddDialogTitleBar("Journal Inventory", CloseIconPressed)
            ;

            for (int i = 0; i < journalitems.Count; i++)
            {
                int page = i;
                DialogComposers["loreList"].AddButton(journalitems[i].Title, () => { return onClickItem(page); }, button, CairoFont.WhiteSmallText(), EnumButtonStyle.None, EnumTextOrientation.Left, "button-"+i);

                DialogComposers["loreList"].GetButton("button-" + i).PlaySound = false;

                button = button.BelowCopy();
            }


            DialogComposers["loreList"]
                .Compose()
            ;
        }


        private bool onClickItem(int i)
        {
            currentLoreItemIndex = i;
            page = 0;

            CairoFont font = CairoFont.WhiteDetailText().WithFontSize(17);
            TextSizeProber prober = new TextSizeProber();
            StringBuilder fulltext = new StringBuilder();
            for (int p = 0; p < journalitems[currentLoreItemIndex].Pieces.Length; p++)
            {
                if (p > 0) fulltext.AppendLine();
                fulltext.Append(journalitems[currentLoreItemIndex].Pieces[p]);
            }

            string[] lines = prober.InsertAutoLineBreaks(font, fulltext, GuiElement.scaled(629));
            double lineheight = prober.GetLineHeight(font, 1.15f);

            int maxlinesPerPage = (int)(GuiElement.scaled(450) / lineheight);
            

            List<string> pagesTemp = new List<string>();
            StringBuilder pageBuilder = new StringBuilder();

            int j = 0;
            
            while (j < lines.Length)
            {
                for (int l = 0; l < maxlinesPerPage; l++)
                {
                    if (j >= lines.Length) break;
                    if (lines[j].Trim().Equals("___NEWPAGE___"))
                    {
                        j++;
                        break;
                    }

                    pageBuilder.AppendLine(lines[j++]);
                }

                pagesTemp.Add(pageBuilder.ToString());
                pageBuilder.Clear();
            }
            pages = pagesTemp.ToArray();


            double elemToDlgPad = ElementGeometrics.ElementToDialogPadding;

            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 630, 450);
            ElementBounds dialogBounds = textBounds.ForkBoundingParent(elemToDlgPad, elemToDlgPad + 20, elemToDlgPad, elemToDlgPad + 30).WithAlignment(EnumDialogArea.LeftMiddle);
            dialogBounds.fixedX = 350;
            

            DialogComposers["loreItem"] =
                capi.Gui
                .CreateCompo("loreItem", dialogBounds, false)
                .AddDialogBG(ElementBounds.Fill, true)
                .AddDialogTitleBar(journalitems[i].Title, CloseIconPressedLoreItem)
                .AddDynamicText(pages[0], font, EnumTextOrientation.Left, textBounds, 1.15f, "page")
                .AddDynamicText("1 / " + pages.Length, CairoFont.WhiteSmallishText(), EnumTextOrientation.Center, ElementBounds.Fixed(250, 500, 100, 30), 1, "currentpage") 
                .AddButton("Previous Page", OnPrevPage, ElementBounds.Fixed(17, 500, 100, 23).WithFixedPadding(10, 4), CairoFont.WhiteSmallishText())
                .AddButton("Next Page", OnNextPage, ElementBounds.Fixed(520, 500, 100, 23).WithFixedPadding(10, 4), CairoFont.WhiteSmallishText())
                .Compose()
            ;

            return true;
        }

        private bool OnNextPage()
        {
            page = Math.Min(pages.Length - 1, page + 1);
            DialogComposers["loreItem"].GetDynamicText("page").SetNewText(pages[page]);
            DialogComposers["loreItem"].GetDynamicText("currentpage").SetNewText((page + 1) + " / " + pages.Length);
            return true;
        }

        private bool OnPrevPage()
        {
            page = Math.Max(0, page - 1);
            DialogComposers["loreItem"].GetDynamicText("page").SetNewText(pages[page]);
            DialogComposers["loreItem"].GetDynamicText("currentpage").SetNewText((page + 1) + " / " + pages.Length);
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
            DialogComposers.Remove("loreItem");
        }

        private void OnNewScrollbarvalue(float value)
        {
            //ElementBounds bounds = journalInvDialog.GetSlotGrid("slotgrid").bounds;
            //bounds.fixedY = 10 - GuiElementItemSlotGrid.unscaledSlotPadding - value;
            //bounds.calcWorldBounds();
        }

        
        public override ITreeAttribute Attributes
        {
            get
            {
                return null;
            }
        }

        public override bool DisableWorldInteract()
        {
            return false;
        }

        public void ReloadValues()
        {
            
        }
    }
}
