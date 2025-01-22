using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        private int CountMatches(string text, Regex regex)
        {
            return regex.Matches(text).Count;
        }

        public void FilterItems()
        {
            shownHandbookPages.Clear();

            if (!loadingPagesAsync)
            {
                string searchText = currentSearchText ?? "";
                string[] searchWords = Regex.Split(searchText, "\\s+", RegexOptions.Multiline);
                var pattern = $"({String.Join("|", searchWords.Where(w => w != "").Select(w => $"{Regex.Escape(w.ToSearchFriendly().Trim())}"))})";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Multiline);

                var weightedPages = new List<WeightedHandbookPage>();
                allHandbookPages.ForEach(page =>
                {
                    if (currentCatgoryCode != null && page.CategoryCode != currentCatgoryCode || page.IsDuplicate) return;

                    var pageText = page.GetPageText();
                    var titleMathces = CountMatches(pageText.Title ?? "", regex);
                    var textMatches = CountMatches(pageText.Text ?? "", regex);
                    if (titleMathces > 0 || textMatches > 0)
                        weightedPages.Add(new WeightedHandbookPage
                        {
                            Page = page,
                            TitleMatches = titleMathces,
                            TitleLength = pageText.Title?.Length ?? 0,
                            TextMatches = textMatches,
                        });
                });
                if (searchText.Length > 0)
                {
                    weightedPages.Sort((a, b) =>
                    {
                        var titleSort = b.TitleMatches - a.TitleMatches;
                        if (titleSort != 0) return titleSort;
                        var textSort = b.TextMatches - a.TextMatches;
                        if (textSort != 0) return textSort;
                        // Prefer shorter matches, efffectively prioritizing more "exact" matches
                        // e. g. "Iron Plate" over "Plate Armor (Iron)" when searching "Iron Plate"
                        return a.TitleLength - b.TitleLength;
                    });
                }
                weightedPages.ForEach(page => shownHandbookPages.Add(page.Page));
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
