using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class GuiDialogTrader : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        InventoryTrader traderInventory;
        EntityAgent owningEntity;

        double prevPlrAbsFixedX, prevPlrAbsFixedY;
        double prevTdrAbsFixedX, prevTdrAbsFixedY;
        double notifyPlayerMoneyTextSeconds;
        double notifyTraderMoneyTextSeconds;

        int rows = 4;
        int cols = 4;
        int curTab = 0;

        
        ModSystemAuction auctionSys;
        InventoryGeneric auctionSlotInv;

        bool auctionHouseEnabled => capi.World.Config.GetBool("auctionHouse", true);



        GuiElementCellList<Auction> listElem;
        List<Auction> auctions;
        ElementBounds clipBounds;

        public GuiDialogTrader(InventoryTrader traderInventory, EntityAgent owningEntity, ICoreClientAPI capi, int rows = 4, int cols = 4) : base(capi)
        {
            auctionSys = capi.ModLoader.GetModSystem<ModSystemAuction>();
            auctionSys.OnCellUpdateClient = () =>
            {
                listElem?.ReloadCells(auctions);
                updateScrollbarBounds();
            };
            auctionSys.curTraderClient = owningEntity as EntityTrader;

            this.traderInventory = traderInventory;
            this.owningEntity = owningEntity;
            this.rows = rows;
            this.cols = cols;

            if (!auctionSys.createAuctionSlotByPlayer.TryGetValue(capi.World.Player.PlayerUID, out auctionSlotInv))
            {
                auctionSys.createAuctionSlotByPlayer[capi.World.Player.PlayerUID] = auctionSlotInv = new InventoryGeneric(1, "auctionslot-" + capi.World.Player.PlayerUID, capi);
                // a negative weight prevents the auction slot from being consider as a suitable slot when shift clicking an item in the hotbar, that is because the default weight is 0 and it checks for >= 0
                auctionSlotInv.OnGetSuitability = (s, t, isMerge) => -1f;
            }

            capi.Network.SendPacketClient(capi.World.Player.InventoryManager.OpenInventory(auctionSlotInv));

            Compose();
        }

        

        public void Compose()
        {
            var tabs = new GuiTab[] { new GuiTab() { Name = Lang.Get("Local goods"), DataInt=0 }, new GuiTab() { Name = Lang.Get("Auction house"),DataInt=1 }, new GuiTab() { Name = Lang.Get("Your Auctions"),DataInt=2 } };
            var tabBounds = ElementBounds.Fixed(0, -24, 500, 25);
            var tabFont = CairoFont.WhiteDetailText();

            if (!auctionHouseEnabled)
            {
                tabs = new GuiTab[] { new GuiTab() { Name = Lang.Get("Local goods"), DataInt = 0 } };
            }


            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);


            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);


            string traderName = owningEntity.GetBehavior<EntityBehaviorNameTag>().DisplayName;
            string dlgTitle = Lang.Get("tradingwindow-" + owningEntity.Code.Path, traderName);
            if (curTab > 0) dlgTitle = Lang.Get("tradertabtitle-" + curTab);

            SingleComposer =
                capi.Gui
                .CreateCompo("traderdialog-" + owningEntity.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(dlgTitle, OnTitleBarClose)
                .AddHorizontalTabs(tabs, tabBounds, OnTabClicked, tabFont, tabFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "tabs")
                .BeginChildElements(bgBounds)
            ;

            SingleComposer.GetHorizontalTabs("tabs").activeElement = curTab;

            if (curTab == 0)
            {
                double pad = GuiElementItemSlotGrid.unscaledSlotPadding;

                ElementBounds leftTopSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad, 70 + pad, cols, rows).FixedGrow(2 * pad, 2 * pad);
                ElementBounds rightTopSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad + leftTopSlotBounds.fixedWidth + 20, 70 + pad, cols, rows).FixedGrow(2 * pad, 2 * pad);

                ElementBounds rightBotSlotBounds = ElementStdBounds
                    .SlotGrid(EnumDialogArea.None, pad + leftTopSlotBounds.fixedWidth + 20, 15 + pad, cols, 1)
                    .FixedGrow(2 * pad, 2 * pad)
                    .FixedUnder(rightTopSlotBounds, 5)
                ;

                ElementBounds leftBotSlotBounds = ElementStdBounds
                    .SlotGrid(EnumDialogArea.None, pad, 15 + pad, cols, 1)
                    .FixedGrow(2 * pad, 2 * pad)
                    .FixedUnder(leftTopSlotBounds, 5)
                ;

                ElementBounds costTextBounds = ElementBounds.Fixed(pad, 85 + 2 * pad + leftTopSlotBounds.fixedHeight + leftBotSlotBounds.fixedHeight, 200, 25);
                ElementBounds offerTextBounds = ElementBounds.Fixed(leftTopSlotBounds.fixedWidth + pad + 20, 85 + 2 * pad + leftTopSlotBounds.fixedHeight + leftBotSlotBounds.fixedHeight, 200, 25);

                ElementBounds traderMoneyBounds = offerTextBounds.FlatCopy().WithFixedOffset(0, offerTextBounds.fixedHeight);
                ElementBounds playerMoneyBounds = costTextBounds.FlatCopy().WithFixedOffset(0, costTextBounds.fixedHeight);


                double daysLeft = (owningEntity as EntityTrader).NextRefreshTotalDays();
                string daysLeftString = daysLeft < 1 ? Lang.Get("Delievery of new goods in less than 1 day") : Lang.Get("Delievery of new goods in {0} days", (int)daysLeft);

                CairoFont deliveryTextFont = CairoFont.WhiteDetailText();
                deliveryTextFont.Color[3] *= 0.7;

                SingleComposer
                    .AddStaticText(daysLeftString, deliveryTextFont, ElementBounds.Fixed(pad, 20 + pad, 430, 25))

                    .AddStaticText(Lang.Get("You can Buy"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(pad, 50 + pad, 200, 25))
                    .AddStaticText(Lang.Get("You can Sell"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(leftTopSlotBounds.fixedWidth + pad + 20, 50 + pad, 200, 25))

                    .AddItemSlotGrid(traderInventory, DoSendPacket, cols, (new int[rows * cols]).Fill(i => i), leftTopSlotBounds, "traderSellingSlots")
                    .AddItemSlotGrid(traderInventory, DoSendPacket, cols, (new int[cols]).Fill(i => rows * cols + i), leftBotSlotBounds, "playerBuyingSlots")

                    .AddItemSlotGrid(traderInventory, DoSendPacket, cols, (new int[rows * cols]).Fill(i => rows * cols + cols + i), rightTopSlotBounds, "traderBuyingSlots")
                    .AddItemSlotGrid(traderInventory, DoSendPacket, cols, (new int[cols]).Fill(i => rows * cols + cols + rows * cols + i), rightBotSlotBounds, "playerSellingSlots")

                    .AddStaticText(Lang.Get("trader-yourselection"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(pad, 70 + 2 * pad + leftTopSlotBounds.fixedHeight, 150, 25))
                    .AddStaticText(Lang.Get("trader-youroffer"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(leftTopSlotBounds.fixedWidth + pad + 20, 70 + 2 * pad + leftTopSlotBounds.fixedHeight, 150, 25))

                    // Cost
                    .AddDynamicText("", CairoFont.WhiteDetailText(), costTextBounds, "costText")
                    // Player money
                    .AddDynamicText("", CairoFont.WhiteDetailText(), playerMoneyBounds, "playerMoneyText")
                    // Offer
                    .AddDynamicText("", CairoFont.WhiteDetailText(), offerTextBounds, "gainText")
                    // Trader money
                    .AddDynamicText("", CairoFont.WhiteDetailText(), traderMoneyBounds, "traderMoneyText")

                    .AddSmallButton(Lang.Get("Goodbye!"), OnByeClicked, leftButton.FixedUnder(playerMoneyBounds, 20))
                    .AddSmallButton(Lang.Get("Buy / Sell"), OnBuySellClicked, rightButton.FixedUnder(traderMoneyBounds, 20), EnumButtonStyle.Normal, "buysellButton")

                    .EndChildElements()
                    .Compose()
                ;

                SingleComposer.GetButton("buysellButton").Enabled = false;
                CalcAndUpdateAssetsDisplay();
                return;
            }

            double listHeight = 377;
            ElementBounds stackListBounds = ElementBounds.Fixed(0, 25, 700, listHeight); //.FixedUnder(searchFieldBounds, 5);
            clipBounds = stackListBounds.ForkBoundingParent();
            ElementBounds insetBounds = stackListBounds.FlatCopy().FixedGrow(3).WithFixedOffset(0, 0);

            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3 + stackListBounds.fixedWidth + 7).WithFixedWidth(20);

            if (curTab == 1)
            {
                auctions = auctionSys.activeAuctions;

                SingleComposer
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddCellList(stackListBounds, createCell, auctionSys.activeAuctions, "stacklist")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")

                    .AddSmallButton(Lang.Get("Goodbye!"), OnByeClicked, leftButton.FixedUnder(clipBounds, 20))
                    .AddSmallButton(Lang.Get("Buy"), OnBuyAuctionClicked, rightButton.FixedUnder(clipBounds, 20), EnumButtonStyle.Normal, "buyauction")
                ;
            }

            if (curTab == 2)
            {
                auctions = auctionSys.ownAuctions;

                ElementBounds button = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);
                string placeStr = Lang.Get("Place Auction");
                string cancelStr = Lang.Get("Cancel Auction");
                
                double placelen = CairoFont.ButtonText().GetTextExtents(placeStr).Width / RuntimeEnv.GUIScale;
                double cancellen = CairoFont.ButtonText().GetTextExtents(cancelStr).Width / RuntimeEnv.GUIScale;

                SingleComposer
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddCellList(stackListBounds, createCell, auctionSys.ownAuctions, "stacklist")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")

                    .AddSmallButton(Lang.Get("Goodbye!"), OnByeClicked, leftButton.FixedUnder(clipBounds, 20))
                    .AddSmallButton(Lang.Get("Place Auction"), OnCreateAuction, rightButton.FixedUnder(clipBounds, 20), EnumButtonStyle.Normal, "placeAuction")
                    .AddSmallButton(cancelStr, OnCancelAuction, button.FlatCopy().FixedUnder(clipBounds, 20).WithFixedAlignmentOffset(-placelen, 0), EnumButtonStyle.Normal, "cancelAuction")
                    .AddSmallButton(Lang.Get("Collect Funds"), OnCollectFunds, button.FlatCopy().FixedUnder(clipBounds, 20).WithFixedAlignmentOffset(-placelen, 0), EnumButtonStyle.Normal, "collectFunds")
                    .AddSmallButton(Lang.Get("Retrieve Items"), OnRetrieveItems, button.FixedUnder(clipBounds, 20).WithFixedAlignmentOffset(-placelen, 0), EnumButtonStyle.Normal, "retrieveItems")
                ;
            }

            if (curTab == 1 || curTab == 2)
            {
                selectedElem = null;
                listElem = SingleComposer.GetCellList<Auction>("stacklist");
                listElem.BeforeCalcBounds();
                listElem.UnscaledCellVerPadding = 0;
                listElem.unscaledCellSpacing = 5;
                SingleComposer.EndChildElements().Compose();

                updateScrollbarBounds();


                didClickAuctionElem(-1);
            }
        }


        void updateScrollbarBounds()
        {
            if (listElem == null) return;
            SingleComposer.GetScrollbar("scrollbar")?.Bounds.CalcWorldBounds();

            SingleComposer.GetScrollbar("scrollbar")?.SetHeights(
                (float)(clipBounds.fixedHeight),
                (float)(listElem.Bounds.fixedHeight)
            );
        }

        private void OnNewScrollbarValue(float value)
        {
            listElem = SingleComposer.GetCellList<Auction>("stacklist");
            listElem.Bounds.fixedY = 0 - value;
            listElem.Bounds.CalcWorldBounds();
        }

        private bool OnCancelAuction()
        {
            if (selectedElem?.auction == null) return false;
            auctionSys.RetrieveAuctionClient(owningEntity, selectedElem.auction.AuctionId);
            return true;
        }

        private bool OnBuyAuctionClicked()
        {
            if (selectedElem?.auction == null) return false;

            var odlg = capi.OpenedGuis.FirstOrDefault(d => d is GuiDialogConfirmPurchase);
            if (odlg != null)
            {
                (odlg as GuiDialog).Focus();
                return true;
            }

            var dlg = new GuiDialogConfirmPurchase(capi, capi.World.Player.Entity, owningEntity, selectedElem.auction);
            dlg.TryOpen();
            return true;
        }

        private bool OnCollectFunds()
        {
            if (selectedElem?.auction == null) return false;
            auctionSys.RetrieveAuctionClient(owningEntity, selectedElem.auction.AuctionId);
            return true;

        }

        private bool OnRetrieveItems()
        {
            if (selectedElem?.auction == null) return false;
            auctionSys.RetrieveAuctionClient(owningEntity, selectedElem.auction.AuctionId);
            return true;

        }

        private IGuiElementCell createCell(Auction auction, ElementBounds bounds)
        {
            bounds.fixedPaddingY = 0;
            var cellElem = new AuctionCellEntry(capi, auctionSlotInv, bounds, auction, didClickAuctionElem);
            return cellElem;
        }

        AuctionCellEntry selectedElem;

        private void didClickAuctionElem(int index)
        {
            if (selectedElem != null) selectedElem.Selected = false;
            if (index >= 0)
            {
                selectedElem = SingleComposer.GetCellList<Auction>("stacklist").elementCells[index] as AuctionCellEntry;
                selectedElem.Selected = true;
            }

            if (curTab == 2)
            {
                var auction = selectedElem?.auction;

                bool sold = auction?.State == EnumAuctionState.Sold || auction?.State == EnumAuctionState.SoldRetrieved;

                SingleComposer.GetButton("cancelAuction").Visible = auction?.State == EnumAuctionState.Active;
                SingleComposer.GetButton("retrieveItems").Visible = auction?.State == EnumAuctionState.Expired || (sold && auction.SellerUid != capi.World.Player.PlayerUID);
                SingleComposer.GetButton("collectFunds").Visible = sold && auction.SellerUid == capi.World.Player.PlayerUID;
                
            }

        }

        

        private bool OnCreateAuction()
        {
            var odlg = capi.OpenedGuis.FirstOrDefault(d => d is GuiDialogCreateAuction);
            if (odlg != null)
            {
                (odlg as GuiDialog).Focus();
                return true;
            }

            var dlg = new GuiDialogCreateAuction(capi, owningEntity, auctionSlotInv);
            dlg.TryOpen();
            return true;
        }


        private void OnTabClicked(int tab)
        {
            curTab = tab;
            Compose();
        }
        


        #region Local goods

        void CalcAndUpdateAssetsDisplay()
        {
            int playerAssets = InventoryTrader.GetPlayerAssets(capi.World.Player.Entity);
            SingleComposer.GetDynamicText("playerMoneyText")?.SetNewText(Lang.Get("You have {0} Gears", playerAssets));

            int traderAssets = traderInventory.GetTraderAssets();
            SingleComposer.GetDynamicText("traderMoneyText")?.SetNewText(Lang.Get("{0} has {1} Gears", owningEntity.GetBehavior<EntityBehaviorNameTag>().DisplayName, traderAssets));
        }

        private void TraderInventory_SlotModified(int slotid)
        {
            int totalCost = traderInventory.GetTotalCost();
            int totalGain = traderInventory.GetTotalGain();

            SingleComposer.GetDynamicText("costText")?.SetNewText(totalCost > 0 ? Lang.Get("Total Cost: {0} Gears", totalCost) : "");
            SingleComposer.GetDynamicText("gainText")?.SetNewText(totalGain > 0 ? Lang.Get("Total Gain: {0} Gears", totalGain) : "");

            if (SingleComposer.GetButton("buysellButton") != null)
            {
                SingleComposer.GetButton("buysellButton").Enabled = totalCost > 0 || totalGain > 0;

                CalcAndUpdateAssetsDisplay();
            }

            
        }

        private bool OnBuySellClicked()
        {
            EnumTransactionResult result = traderInventory.TryBuySell(capi.World.Player);
            if (result == EnumTransactionResult.Success)
            {
                capi.Gui.PlaySound(new AssetLocation("sounds/effect/cashregister"), false, 0.25f);
                (owningEntity as EntityTrader).talkUtil.Talk(EnumTalkType.Purchase);
            } 

            if (result == EnumTransactionResult.PlayerNotEnoughAssets)
            {
                (owningEntity as EntityTrader).talkUtil.Talk(EnumTalkType.Complain);
                if (notifyPlayerMoneyTextSeconds <= 0)
                {
                    prevPlrAbsFixedX = SingleComposer.GetDynamicText("playerMoneyText").Bounds.absFixedX;
                    prevPlrAbsFixedY = SingleComposer.GetDynamicText("playerMoneyText").Bounds.absFixedY;
                }
                notifyPlayerMoneyTextSeconds = 1.5f;
            }

            if (result == EnumTransactionResult.TraderNotEnoughAssets)
            {
                (owningEntity as EntityTrader).talkUtil.Talk(EnumTalkType.Complain);
                if (notifyTraderMoneyTextSeconds <= 0)
                {
                    prevTdrAbsFixedX = SingleComposer.GetDynamicText("traderMoneyText").Bounds.absFixedX;
                    prevTdrAbsFixedY = SingleComposer.GetDynamicText("traderMoneyText").Bounds.absFixedY;
                }
                notifyTraderMoneyTextSeconds = 1.5f;
            }

            if (result == EnumTransactionResult.TraderNotEnoughSupplyOrDemand)
            {
                (owningEntity as EntityTrader).talkUtil.Talk(EnumTalkType.Complain);
            }

            capi.Network.SendEntityPacket(owningEntity.EntityId, 1000, null);

            TraderInventory_SlotModified(0);
            CalcAndUpdateAssetsDisplay();

            return true;
        }

        private bool OnByeClicked()
        {
            TryClose();
            return true;
        }

        private void DoSendPacket(object p)
        {
            capi.Network.SendEntityPacket(owningEntity.EntityId, p);
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            traderInventory.SlotModified += TraderInventory_SlotModified;

            auctionSys.DidEnterAuctionHouse();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();

            traderInventory.SlotModified -= TraderInventory_SlotModified;

            (owningEntity as EntityTrader).talkUtil.Talk(EnumTalkType.Goodbye);

            capi.Network.SendPacketClient(capi.World.Player.InventoryManager.CloseInventory(traderInventory));

            SingleComposer.GetSlotGrid("traderSellingSlots")?.OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("playerBuyingSlots")?.OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("traderBuyingSlots")?.OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("playerSellingSlots")?.OnGuiClosed(capi);

            auctionSlotInv[0].Itemstack = null;
            capi.World.Player.InventoryManager.CloseInventory(auctionSlotInv);

            auctionSys.DidLeaveAuctionHouse();
        }

        public override void OnBeforeRenderFrame3D(float deltaTime)
        {
            base.OnBeforeRenderFrame3D(deltaTime);

            if (notifyPlayerMoneyTextSeconds > 0)
            {
                notifyPlayerMoneyTextSeconds -= deltaTime;
                var elem = SingleComposer.GetDynamicText("playerMoneyText");

                if (elem != null)
                {
                    if (notifyPlayerMoneyTextSeconds <= 0)
                    {
                        elem.Bounds.absFixedX = prevPlrAbsFixedX;
                        elem.Bounds.absFixedY = prevPlrAbsFixedY;
                    }
                    else
                    {
                        elem.Bounds.absFixedX = prevPlrAbsFixedX + notifyPlayerMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                        elem.Bounds.absFixedY = prevPlrAbsFixedY + notifyPlayerMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                    }
                }
            }


            if (notifyTraderMoneyTextSeconds > 0)
            {
                notifyTraderMoneyTextSeconds -= deltaTime;
                var elem = SingleComposer.GetDynamicText("traderMoneyText");

                if (elem != null)
                {
                    if (notifyTraderMoneyTextSeconds <= 0)
                    {
                        elem.Bounds.absFixedX = prevPlrAbsFixedX;
                        elem.Bounds.absFixedY = prevPlrAbsFixedY;
                    }
                    else
                    {
                        elem.Bounds.absFixedX = prevTdrAbsFixedX + notifyTraderMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                        elem.Bounds.absFixedY = prevTdrAbsFixedY + notifyTraderMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                    }
                }
            }
        }

        #endregion

        public override bool PrefersUngrabbedMouse => false;
        public override float ZSize => 300; // Due to crossed out texture on bought out items
    }
}
