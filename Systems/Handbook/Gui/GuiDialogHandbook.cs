using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class HandbookTab : GuiTab
    {
        public string CategoryCode;
    }


    public class BrowseHistoryElement
    {
        public GuiHandbookPage Page;
        public string SearchText;
        public float PosY;
    }

    public delegate List<GuiHandbookPage> OnCreatePagesDelegate();
    public delegate void OnComposePageDelegate(GuiHandbookPage page, GuiComposer detailViewGui, ElementBounds textBounds, ActionConsumable<string> openDetailPageFor);


    public class GuiDialogHandbook : GuiDialog
    {
        public override double DrawOrder => 0.2; // Needs to be same as chest container guis so it can be on top of those dialogs if necessary

        protected Dictionary<string, int> pageNumberByPageCode = new Dictionary<string, int>();
        internal List<GuiHandbookPage> allHandbookPages = new List<GuiHandbookPage>();
        protected List<IFlatListItem> shownHandbookPages = new List<IFlatListItem>();

        protected List<string> categoryCodes = new List<string>();

        protected Stack<BrowseHistoryElement> browseHistory = new Stack<BrowseHistoryElement>();
        protected string currentSearchText;
        protected GuiComposer overviewGui;
        protected GuiComposer detailViewGui;
        protected bool loadingPagesAsync;
        protected double listHeight = 500;
        protected GuiTab[] tabs;

        public string currentCatgoryCode;
        public override string ToggleKeyCombinationCode => "handbook";


        OnCreatePagesDelegate createPageHandlerAsync;
        OnComposePageDelegate composePageHandler;

        public virtual string DialogTitle => "";

        public GuiDialogHandbook(ICoreClientAPI capi, OnCreatePagesDelegate createPageHandlerAsync, OnComposePageDelegate composePageHandler) : base(capi)
        {
            this.createPageHandlerAsync = createPageHandlerAsync;
            this.composePageHandler = composePageHandler;

            capi.Settings.AddWatcher<float>("guiScale", (float val) =>
            {
                initOverviewGui();
                FilterItems();
                foreach (GuiHandbookPage elem in shownHandbookPages)
                {
                    elem.Dispose();
                }
            });

            loadEntries();
        }


        protected virtual void loadEntries()
        {
            capi.Logger.VerboseDebug("Starting initialising handbook");
            pageNumberByPageCode.Clear();
            shownHandbookPages.Clear();
            allHandbookPages.Clear();

            HashSet<string> codes = initCustomPages();
            codes.Add("stack");
            this.categoryCodes = codes.ToList();

            loadingPagesAsync = true;
            TyronThreadPool.QueueTask(LoadPages_Async);

            initOverviewGui();
            capi.Logger.VerboseDebug("Done creating handbook index GUI");
        }


        public void initOverviewGui()
        {
            ElementBounds searchFieldBounds = ElementBounds.Fixed(GuiStyle.ElementToDialogPadding - 2, 45, 300, 30);
            ElementBounds stackListBounds = ElementBounds.Fixed(0, 0, 500, listHeight).FixedUnder(searchFieldBounds, 5);

            ElementBounds clipBounds = stackListBounds.ForkBoundingParent();
            ElementBounds insetBounds = stackListBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3 + stackListBounds.fixedWidth + 7).WithFixedWidth(20);

            ElementBounds closeButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 8)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(2, 0)
            ;

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, stackListBounds, scrollbarBounds, closeButtonBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.None).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPosition(0, 70);
            ElementBounds tabBounds = ElementBounds.Fixed(-200, 35, 200, 545);

            int curTab;
            ElementBounds backButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 5)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(-6, 3)
            ;

            tabs = genTabs(out curTab);

            overviewGui = capi.Gui
                .CreateCompo("handbook-overview", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .AddIf(tabs.Length > 0)
                    .AddVerticalTabs(tabs, tabBounds, OnTabClicked, "verticalTabs")
                .EndIf()
                .AddTextInput(searchFieldBounds, FilterItemsBySearchText, CairoFont.WhiteSmallishText(), "searchField")
                .BeginChildElements(bgBounds)
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddFlatList(stackListBounds, onLeftClickListElement, shownHandbookPages, "stacklist")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalueOverviewPage, scrollbarBounds, "scrollbar")
                    .AddIf(capi.IsSinglePlayer && !capi.OpenedToLan)
                        .AddToggleButton(Lang.Get("Pause game"), CairoFont.WhiteDetailText(), onTogglePause, ElementBounds.Fixed(360, -15, 100, 22), "pausegame")
                    .EndIf()
                    .AddSmallButton(Lang.Get("general-back"), OnButtonBack, backButtonBounds, EnumButtonStyle.Normal, "backButton")
                    .AddSmallButton(Lang.Get("Close Handbook"), OnButtonClose, closeButtonBounds)
                .EndChildElements()
                .Compose()
            ;

            overviewGui.GetScrollbar("scrollbar").SetHeights(
                (float)listHeight,
                (float)overviewGui.GetFlatList("stacklist").insideBounds.fixedHeight
            );

            overviewGui.GetTextInput("searchField").SetPlaceHolderText(Lang.Get("Search..."));

            if (tabs.Length > 0)
            {
                overviewGui.GetVerticalTab("verticalTabs").SetValue(curTab, false);
                currentCatgoryCode = (tabs[curTab] as HandbookTab).CategoryCode;
            }

            var btn = overviewGui.GetToggleButton("pausegame");
            if (btn != null) btn.SetValue(!capi.Settings.Bool["noHandbookPause"]);

            overviewGui.FocusElement(overviewGui.GetTextInput("searchField").TabIndex);
        }

        protected virtual void onTogglePause(bool on)
        {
            capi.PauseGame(on);
            capi.Settings.Bool["noHandbookPause"] = !on;
        }

        protected virtual GuiTab[] genTabs(out int curTab)
        {
            curTab = 0;
            return new GuiTab[0];
        }


        protected virtual void OnTabClicked(int index, GuiTab tab)
        {
            selectTab((tab as HandbookTab).CategoryCode);
        }

        public void selectTab(string code)
        {
            currentCatgoryCode = code;
            FilterItems();
            capi.Settings.String["currentHandbookCategoryCode"] = currentCatgoryCode;
        }

        public void ReloadPage()
        {
            if (browseHistory.Count > 0)
            {
                initDetailGui();
            } else {
                initOverviewGui();
            }
        }

        protected virtual void initDetailGui()
        {
            ElementBounds textBounds = ElementBounds.Fixed(9, 45, 500, 30 + listHeight + 17);

            ElementBounds clipBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = clipBounds.CopyOffsetedSibling(textBounds.fixedWidth + 7, -6, 0, 6).WithFixedWidth(20);

            ElementBounds closeButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 5)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(-11, 1)
            ;
            ElementBounds backButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 5)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(4, 1)
            ;
            ElementBounds overviewButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 5)
                .WithAlignment(EnumDialogArea.CenterFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(0, 1)
            ;

            ElementBounds bgBounds = insetBounds.ForkBoundingParent(5, 40, 36, 52).WithFixedPadding(GuiStyle.ElementToDialogPadding / 2);
            bgBounds.WithChildren(insetBounds, textBounds, scrollbarBounds, backButtonBounds, closeButtonBounds);

            ElementBounds dialogBounds = bgBounds.ForkBoundingParent().WithAlignment(EnumDialogArea.None).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPosition(0, 70);
            ElementBounds tabBounds = ElementBounds.Fixed(-200, 35, 200, 545);

            BrowseHistoryElement curPage = browseHistory.Peek();
            float posY = curPage.PosY;
            int curTab;

            detailViewGui?.Dispose();
            detailViewGui = capi.Gui
                .CreateCompo("handbook-detail", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .AddVerticalTabs(genTabs(out curTab), tabBounds, OnDetailViewTabClicked, "verticalTabs")
                .BeginChildElements(bgBounds)
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
            ;

            composePageHandler(curPage.Page, detailViewGui, textBounds, OpenDetailPageFor);
            var lastAddedElement = detailViewGui.LastAddedElement;

            detailViewGui
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalueDetailPage, scrollbarBounds, "scrollbar")
                    .AddIf(capi.IsSinglePlayer && !capi.OpenedToLan)
                        .AddToggleButton(Lang.Get("Pause game"), CairoFont.WhiteDetailText(), onTogglePause, ElementBounds.Fixed(370, -5, 100, 22), "pausegame")
                    .EndIf()

                    .AddSmallButton(Lang.Get("general-back"), OnButtonBack, backButtonBounds)
                    .AddSmallButton(Lang.Get("handbook-overview"), OnButtonOverview, overviewButtonBounds)
                    .AddSmallButton(Lang.Get("general-close"), OnButtonClose, closeButtonBounds)
                .EndChildElements()
                .Compose()
            ;

            detailViewGui.GetScrollbar("scrollbar").SetHeights(
                (float)listHeight, (float)lastAddedElement.Bounds.fixedHeight
            );
            detailViewGui.GetScrollbar("scrollbar").CurrentYPosition = posY;
            OnNewScrollbarvalueDetailPage(posY);

            detailViewGui.GetVerticalTab("verticalTabs").SetValue(curTab, false);

            var btn = detailViewGui.GetToggleButton("pausegame");
            if (btn != null) btn.SetValue(!capi.Settings.Bool["noHandbookPause"]);
        }

        protected virtual void OnDetailViewTabClicked(int index, GuiTab tab)
        {
            browseHistory.Clear();
            OnTabClicked(index, tab);
        }

        protected bool OnButtonOverview()
        {
            browseHistory.Clear();
            return true;
        }

        public virtual bool OpenDetailPageFor(string pageCode)
        {
            capi.Gui.PlaySound("menubutton_press");

            int num;
            if (pageNumberByPageCode.TryGetValue(pageCode, out num))
            {
                GuiHandbookPage elem = allHandbookPages[num];
                if (browseHistory.Count > 0 && elem == browseHistory.Peek().Page) return true;

                browseHistory.Push(new BrowseHistoryElement()
                {
                    Page = elem,
                    PosY = 0
                });
                initDetailGui();
                return true;
            }

            return false;
        }


        protected bool OnButtonBack()
        {
            if (browseHistory.Count == 0) return true;

            browseHistory.Pop();
            if (browseHistory.Count > 0)
            {
                if (browseHistory.Peek().SearchText != null)
                {
                    Search(browseHistory.Peek().SearchText);
                }
                else
                {
                    initDetailGui();
                }
            }

            return true;
        }

        protected void onLeftClickListElement(int index)
        {
            browseHistory.Push(new BrowseHistoryElement()
            {
                Page = shownHandbookPages[index] as GuiHandbookPage
            });

            initDetailGui();
        }


        protected void OnNewScrollbarvalueOverviewPage(float value)
        {
            GuiElementFlatList stacklist = overviewGui.GetFlatList("stacklist");

            stacklist.insideBounds.fixedY = 3 - value;
            stacklist.insideBounds.CalcWorldBounds();
        }

        protected void OnNewScrollbarvalueDetailPage(float value)
        {
            GuiElementRichtext richtextElem = detailViewGui.GetRichtext("richtext");
            richtextElem.Bounds.fixedY = 3 - value;
            richtextElem.Bounds.CalcWorldBounds();

            browseHistory.Peek().PosY = detailViewGui.GetScrollbar("scrollbar").CurrentYPosition;
        }

        protected void OnTitleBarClose()
        {
            TryClose();
        }

        protected bool OnButtonClose()
        {
            TryClose();
            return true;
        }

        public override void OnGuiOpened()
        {
            initOverviewGui();
            FilterItems();
            base.OnGuiOpened();

            if (capi.IsSinglePlayer && !capi.OpenedToLan && !capi.Settings.Bool["noHandbookPause"]) capi.PauseGame(true);
        }

        public override void OnGuiClosed()
        {
            browseHistory.Clear();
            overviewGui.GetTextInput("searchField").SetValue("");

            if (capi.IsSinglePlayer && !capi.OpenedToLan && !capi.Settings.Bool["noHandbookPause"])
            {
                // Player might be still in the Create Character Dialog where he clicked one of the links there
                if (capi.OpenedGuis.FirstOrDefault(dlg => dlg is GuiDialogCreateCharacter) == null)
                {
                    capi.PauseGame(false);
                }
            }

            base.OnGuiClosed();
        }




        protected virtual HashSet<string> initCustomPages()
        {
            return new HashSet<string>();
        }


        protected void LoadPages_Async()
        {
            allHandbookPages.AddRange(createPageHandlerAsync());
            for (int i = 0; i < allHandbookPages.Count; i++)
            {
                var page = allHandbookPages[i];
                pageNumberByPageCode[page.PageCode] = page.PageNumber = i;
            }

            loadingPagesAsync = false;
        }

        public void Search(string text)
        {
            currentCatgoryCode = null;
            SingleComposer = overviewGui;
            overviewGui.GetTextInput("searchField").SetValue(text);

            if (browseHistory.Count > 0 && browseHistory.Peek().SearchText == text) return;

            capi.Gui.PlaySound("menubutton_press");

            browseHistory.Push(new BrowseHistoryElement()
            {
                Page = null,
                SearchText = text,
                PosY = 0
            });
        }


        protected void FilterItemsBySearchText(string text)
        {
            if (currentSearchText == text) return;
            currentSearchText = text;
            FilterItems();
        }



        public void FilterItems()
        {
            string text = currentSearchText?.ToLowerInvariant();
            string[] texts;
            bool logicalAnd = false;   // true if "and" is present; false if "or" or no logical operator is present
            if (text == null)
            {
                texts = new string[0];
            }
            else
            {
                if (text.Contains(" or ", StringComparison.Ordinal))
                {
                    texts = text.Split(new string[] { " or " }, StringSplitOptions.RemoveEmptyEntries).OrderBy(str => str.Length).ToArray();
                }
                else if (text.Contains(" and ", StringComparison.Ordinal))
                {
                    texts = text.Split(new string[] { " and " }, StringSplitOptions.RemoveEmptyEntries).OrderBy(str => str.Length).ToArray();
                    logicalAnd = texts.Length > 1;
                }
                else
                {
                    texts = new string[] { text };
                }
                int countEmpty = 0;
                for (int i = 0; i < texts.Length; i++)
                {
                    texts[i] = texts[i].ToSearchFriendly().Trim();      // Only remove diacritical marks etc after splitting on " or ", helps with languages such as Icelandic where "ör" is a word (a type of bow)
                    if (texts[i].Length == 0) countEmpty++;
                }
                if (countEmpty > 0)
                {
                    string[] newTexts = new string[texts.Length - countEmpty];
                    int j = 0;
                    for (int i = 0; i < texts.Length; i++)
                    {
                        if (texts[i].Length == 0) continue;
                        newTexts[j++] = texts[i];
                    }
                    texts = newTexts;
                    logicalAnd = logicalAnd && texts.Length > 1;
                }
            }

            List<WeightedHandbookPage> foundPages = new List<WeightedHandbookPage>();
            shownHandbookPages.Clear();

            if (!loadingPagesAsync)
            {
                for (int i = 0; i < allHandbookPages.Count; i++)
                {
                    GuiHandbookPage page = allHandbookPages[i];
                    if (currentCatgoryCode != null && page.CategoryCode != currentCatgoryCode) continue;
                    if (page.IsDuplicate) continue;

                    float weight = 1;
                    bool matched = logicalAnd;    // Normally (for no logical operator or logical operator OR) no match unless any found; if it's logical AND then we have no match if any in texts are not found (and texts length cannot be 0)

                    for (int j = 0; j < texts.Length; j++)
                    {
                        weight = page.GetTextMatchWeight(texts[j]);
                        if (weight > 0)
                        {
                            if (!logicalAnd) { matched = true; break; }
                        }
                        else
                        {
                            if (logicalAnd) { matched = false; break; };
                        }
                    }
                    if (!matched && texts.Length > 0) continue;

                    foundPages.Add(new WeightedHandbookPage() { Page = page, Weight = weight });
                }

                foreach (var val in foundPages.OrderByDescending(wpage => wpage.Weight))
                {
                    shownHandbookPages.Add(val.Page);
                }
            }

            GuiElementFlatList stacklist = overviewGui.GetFlatList("stacklist");
            stacklist.CalcTotalHeight();
            overviewGui.GetScrollbar("scrollbar").SetHeights(
                (float)listHeight, (float)stacklist.insideBounds.fixedHeight
            );
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (browseHistory.Count == 0 || browseHistory.Peek().SearchText != null)
            {
                SingleComposer = overviewGui;
            }
            else
            {
                SingleComposer = detailViewGui;
            }

            if (SingleComposer == overviewGui)
            {
                overviewGui.GetButton("backButton").Enabled = browseHistory.Count > 0;
            }

            base.OnRenderGUI(deltaTime);
        }


        public override bool PrefersUngrabbedMouse => true;

        public override bool CaptureAllInputs()
        {
            return false;
        }

        public override void Dispose()
        {
            if (allHandbookPages != null)
            {
                foreach (GuiHandbookPage elem in allHandbookPages)
                {
                    elem?.Dispose();
                }
            }

            overviewGui?.Dispose();
            detailViewGui?.Dispose();
        }


    }
}
