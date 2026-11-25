using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

#nullable disable

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

    public delegate List<IFlatListItem> OnCreatePagesDelegate();
    public delegate void OnComposePageDelegate(GuiHandbookPage page, GuiComposer detailViewGui, ElementBounds textBounds, ActionConsumable<string> openDetailPageFor);


    public class GuiDialogHandbook : GuiDialog
    {
        public override double DrawOrder => 0.2; // Needs to be same as chest container guis so it can be on top of those dialogs if necessary

        protected Dictionary<string, GuiHandbookPage> pageElementByPageCode = [];
        protected internal List<IFlatListItem> allHandbookListItems = [];
        protected List<IFlatListItem> shownHandbookListItems = [];

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
                if (opened)
                {
                    Dispose();
                    initOverviewGui();
                    FilterItems();
                }
            });

            loadEntries();
        }


        protected virtual void loadEntries()
        {
            capi.Logger.VerboseDebug("Starting initialising handbook");
            pageElementByPageCode.Clear();
            shownHandbookListItems.Clear();
            allHandbookListItems.Clear();

            HashSet<string> codes = initCustomPages();
            codes.Add("stack");
            this.categoryCodes = codes.ToList();

            loadingPagesAsync = true;
            TyronThreadPool.QueueTask(LoadPages_Async,"Handbook.LoadPages_Async");

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

            ElementBounds backButtonBounds = ElementBounds
                .FixedSize(0, 0)
                .FixedUnder(clipBounds, 2 * 5 + 5)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20, 4)
                .WithFixedAlignmentOffset(-6, 3)
            ;

            tabs = genTabs(out int curTab);

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
                        .AddNestedList(stackListBounds, onLeftClickListElement, shownHandbookListItems, "stacklist")
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
                (float)overviewGui.GetNestedList("stacklist").insideBounds.fixedHeight
            );

            var searchInput = overviewGui.GetTextInput("searchField");
            searchInput.SetPlaceHolderText(Lang.Get("Search..."));
            searchInput.SetValue(currentSearchText);

            if (tabs.Length > 0)
            {
                overviewGui.GetVerticalTab("verticalTabs").SetValue(curTab, false);
                currentCatgoryCode = (tabs[curTab] as HandbookTab).CategoryCode;
            }

            var btn = overviewGui.GetToggleButton("pausegame");
            if (btn != null) btn.SetValue(!capi.Settings.Bool["noHandbookPause"]);

            overviewGui.FocusElement(searchInput.TabIndex);
        }

        protected virtual void onTogglePause(bool on)
        {
            capi.PauseGame(on);
            capi.Settings.Bool["noHandbookPause"] = !on;
        }

        protected virtual GuiTab[] genTabs(out int curTab)
        {
            curTab = 0;
            return Array.Empty<GuiTab>();
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

            capi.World.FrameProfiler.Mark("handbook-opendetailpage-1");
            detailViewGui?.Dispose();
            detailViewGui = capi.Gui
                .CreateCompo("handbook-detail", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .AddVerticalTabs(genTabs(out int curTab), tabBounds, OnDetailViewTabClicked, "verticalTabs")
                .BeginChildElements(bgBounds)
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
            ;
            capi.World.FrameProfiler.Mark("handbook-opendetailpage-2");

            composePageHandler(curPage.Page, detailViewGui, textBounds, OpenDetailPageFor);
            var lastAddedElement = detailViewGui.LastAddedElement;
            capi.World.FrameProfiler.Mark("handbook-opendetailpage-3");

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
            ;
            detailViewGui.Compose();
            capi.World.FrameProfiler.Mark("handbook-opendetailpage-compose");

            float posY = curPage.PosY;
            if (posY == 0)
            {
                float fullHeight = (float)lastAddedElement.Bounds.fixedHeight;
                if (fullHeight > (float)listHeight)
                {
                    float searchRel = curPage.Page.GetSearchTextPosRel(currentSearchText);
                    posY = Math.Max(0, (searchRel * 0.98f) * fullHeight);
                }
            }
            detailViewGui.GetScrollbar("scrollbar").SetHeights(
                (float)listHeight, (float)lastAddedElement.Bounds.fixedHeight
            );
            detailViewGui.GetScrollbar("scrollbar").CurrentYPosition = posY;
            OnNewScrollbarvalueDetailPage(posY);

            detailViewGui.GetVerticalTab("verticalTabs").SetValue(curTab, false);

            var btn = detailViewGui.GetToggleButton("pausegame");
            if (btn != null) btn.SetValue(!capi.Settings.Bool["noHandbookPause"]);

            capi.World.FrameProfiler.Mark("handbook-opendetailpage-scrollbars");
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

            if (pageElementByPageCode.TryGetValue(pageCode, out var elem))
            {
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

        protected void onLeftClickListElement(List<IFlatListItem> elements, int index)
        {
            if (elements[index] is INestedListItemWithChildren nestedListItem)
            {
                nestedListItem.IsExpanded = !nestedListItem.IsExpanded;
                var stacklist = overviewGui.GetNestedList("stacklist");
                stacklist.CalcTotalHeight();
                overviewGui.GetScrollbar("scrollbar").SetHeights(
                    (float)listHeight, (float)stacklist.insideBounds.fixedHeight
                );

                return;
            }

            browseHistory.Push(new BrowseHistoryElement()
            {
                Page = elements[index] as GuiHandbookPage
            });

            initDetailGui();
        }


        protected void OnNewScrollbarvalueOverviewPage(float value)
        {
            GuiElementNestedList stacklist = overviewGui.GetNestedList("stacklist");

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
            allHandbookListItems.AddRange(createPageHandlerAsync());
            loadPageElementsByPageCodes(allHandbookListItems);

            loadingPagesAsync = false;
            if (!opened || overviewGui == null) return;
            capi.Event.EnqueueMainThreadTask(FilterItems, "Handbook.FilterItems");
        }

        private void loadPageElementsByPageCodes(List<IFlatListItem> list)
        {
            foreach (var listItem in list)
            {
                switch (listItem)
                {
                    case GuiHandbookNestedListItem nestedItem:
                        loadPageElementsByPageCodes(nestedItem.Children);
                        break;
                    case GuiHandbookPage page:
                        pageElementByPageCode[page.PageCode] = page;
                        break;
                }
            }
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
            shownHandbookListItems.Clear();

            if (!loadingPagesAsync)
            {
                string searchText = currentSearchText ?? string.Empty;
                var regex = RegexFromSearchText(searchText);
                var strictRegex = RegexFromSearchText(searchText, true);

                filterItemsRecursive(allHandbookListItems, shownHandbookListItems, searchText, regex, strictRegex, out _);
            }

            GuiElementNestedList stacklist = overviewGui.GetNestedList("stacklist");
            stacklist.CalcTotalHeight();
            overviewGui.GetScrollbar("scrollbar").SetHeights(
                (float)listHeight, (float)stacklist.insideBounds.fixedHeight
            );
        }

        private void filterItemsRecursive(List<IFlatListItem> elements, List<IFlatListItem> filteredElements, string searchText, Regex regex, Regex strictRegex, out List<WeightedHandbookListItem> weightedListItems)
        {
            weightedListItems = new List<WeightedHandbookListItem>();
            bool filtered = !string.IsNullOrWhiteSpace(currentSearchText);
            foreach (var element in elements)
            {
                if (element is INestedListItemWithChildren nestedListItem)
                {
                    nestedListItem.IsFiltered = filtered;
                    nestedListItem.FilteredChildren.Clear();
                    filterItemsRecursive(nestedListItem.Children, nestedListItem.FilteredChildren, searchText, regex, strictRegex, out var nestedWeightedListItems);
                    nestedListItem.AddWeightedHandbookListItem(regex, strictRegex, ref weightedListItems, nestedWeightedListItems);
                    continue;
                }

                if (element is not GuiHandbookPage page) continue;

                if (currentCatgoryCode != null && page.CategoryCode != currentCatgoryCode || page.IsDuplicate) continue;

                var pageText = page.GetPageText();
                var titleMatches = CountMatches(pageText.Title, regex);
                var strictTitleMatches = CountMatches(pageText.Title, strictRegex);
                if (strictTitleMatches > 0 && !string.IsNullOrWhiteSpace(pageText.Title) &&
                    pageText.Title.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    strictTitleMatches++;
                    titleMatches += searchText.Length * 50 / pageText.Title.Length;
                }
                var textMatches = CountMatches(pageText.Text, regex);
                if (titleMatches > 0 || textMatches > 0)
                    weightedListItems.Add(new WeightedHandbookListItem
                    {
                        ListItem = page,
                        TitleMatches = titleMatches,
                        StrictTitleMatches = strictTitleMatches,
                        TitleLength = pageText.Title?.Length ?? 0,
                        TextMatches = textMatches,
                        SearchWeight = 1 + page.SearchWeightOffset
                    });
            };
            if (searchText.Length > 0)
            {
                weightedListItems.Sort((a, b) =>
                {
                    // Start by comparing strict word matches in the title
                    var strictSort = b.StrictTitleMatches - a.StrictTitleMatches;
                    if (strictSort != 0) return strictSort;
                    // Then for fuzzier matches in the title
                    var titleSort = b.TitleMatches - a.TitleMatches;
                    if (titleSort != 0) return titleSort;
                    // Then we use the search weight to sort matches
                    var weightSort = b.SearchWeight.CompareTo(a.SearchWeight);
                    if (weightSort != 0) return weightSort;
                    // Then for length of the title
                    var lengthSort = a.TitleLength - b.TitleLength;
                    if (lengthSort != 0) return lengthSort;

                    // Then we will compare number of matches in the text and finally the search weight offset for just a bit extra
                    return b.TextMatches - a.TextMatches;
                });
            }

            foreach (var listItem in weightedListItems) filteredElements.Add(listItem.ListItem);
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
            if (allHandbookListItems != null)
            {
                foreach (IFlatListItem elem in allHandbookListItems)
                {
                    elem?.Dispose();
                }
            }

            overviewGui?.Dispose();
            detailViewGui?.Dispose();
        }
    }
}
