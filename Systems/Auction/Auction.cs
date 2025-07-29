using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumAuctionState
    {
        Active = 0,
        Sold = 1,
        SoldRetrieved = 2,
        Expired = 3
    }

    public class ModSystemAuction : ModSystem
    {
        protected ICoreAPI api;
        protected ICoreServerAPI sapi;
        protected ICoreClientAPI capi;

        protected AuctionsData auctionsData = new AuctionsData();

        protected API.Datastructures.OrderedDictionary<long, Auction> auctions => auctionsData.auctions;

        protected IServerNetworkChannel serverCh => sapi.Network.GetChannel("auctionHouse");
        protected IClientNetworkChannel clientCh => capi.Network.GetChannel("auctionHouse");


        public Dictionary<string, InventoryGeneric> createAuctionSlotByPlayer = new Dictionary<string, InventoryGeneric>();

        public Action OnCellUpdateClient;
        public EntityTradingHumanoid curTraderClient;
        public float debtClient;

        /// <summary>
        /// For modders, change this value if you want to increase/reduce delivery costs
        /// </summary>
        public float DeliveryPriceMul = 1f;

        /// <summary>
        /// For modders, change this value if you want to increase auction times, does not affect cost
        /// </summary>
        public int DurationWeeksMul = 6;

        /// <summary>
        /// The % cut the trader takes from the profits (default is 0.1 which is 10%)
        /// </summary>
        public float SalesCutRate = 0.1f;



        public int DeliveryCostsByDistance(Vec3d src, Vec3d dst)
        {
            return DeliveryCostsByDistance(src.DistanceTo(dst));
        }

        public int DeliveryCostsByDistance(double distance)
        {
            // x/2000 was too expensive, so we use a nonlinar curve
            // https://pfortuny.net/fooplot.com/#W3sidHlwZSI6MCwiZXEiOiI1KmxuKCh4LTIwMCkvMTAwMDArMSkiLCJjb2xvciI6IiMwMDAwMDAifSx7InR5cGUiOjAsImVxIjoieC8yMDAwIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiMCIsIjgwMDAwIiwiMCIsIjE1Il0sInNpemUiOls2NDgsMzk4XX1d
            return (int)Math.Ceiling(3.5 * Math.Log((distance-200) / 10000 + 1) * DeliveryPriceMul);
        }



        public ItemStack SingleCurrencyStack;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            api.Network
                .RegisterChannel("auctionHouse")
                .RegisterMessageType<AuctionActionPacket>()
                .RegisterMessageType<AuctionlistPacket>()
                .RegisterMessageType<AuctionActionResponsePacket>()
                .RegisterMessageType<DebtPacket>();
            ;
        }

        public void loadPricingConfig()
        {
            DeliveryPriceMul = api.World.Config.GetFloat("auctionHouseDeliveryPriceMul", 1);
            DurationWeeksMul = api.World.Config.GetInt("auctionHouseDurationWeeksMul", 3);
            SalesCutRate = api.World.Config.GetFloat("auctionHouseSalesCutRate", 0.1f);
        }

        #region Client
        public List<Auction> activeAuctions = new List<Auction>();
        public List<Auction> ownAuctions = new List<Auction>();

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;
            clientCh.SetMessageHandler<AuctionlistPacket>(onAuctionList).SetMessageHandler<AuctionActionResponsePacket>(onAuctionActionResponse).SetMessageHandler<DebtPacket>(onDebtPkt);

            api.Event.BlockTexturesLoaded += Event_BlockTexturesLoaded;
        }

        private void onDebtPkt(DebtPacket pkt)
        {
            debtClient = pkt.TraderDebt;
        }

        private void Event_BlockTexturesLoaded()
        {
            var item = capi.World.GetItem(new AssetLocation("gear-rusty"));
            if (item != null)
            {
                SingleCurrencyStack = new ItemStack(item);
            }
            loadPricingConfig();
        }

        private void onAuctionActionResponse(AuctionActionResponsePacket pkt)
        {
            if (pkt.ErrorCode != null)
            {
                capi.TriggerIngameError(this, pkt.ErrorCode, Lang.Get("auctionerror-" + pkt.ErrorCode));
                curTraderClient?.TalkUtil.Talk(EnumTalkType.Complain);
            } else
            {
                if (pkt.Action == EnumAuctionAction.PurchaseAuction || (pkt.Action == EnumAuctionAction.RetrieveAuction && pkt.MoneyReceived))
                {
                    capi.Gui.PlaySound(new AssetLocation("sounds/effect/cashregister"), false, 0.25f);
                }

                curTraderClient?.TalkUtil.Talk(EnumTalkType.Purchase);
            }
        }

        private void onAuctionList(AuctionlistPacket pkt)
        {
            debtClient = pkt.TraderDebt;

            if (pkt.IsFullUpdate)
            {
                activeAuctions.Clear();
                ownAuctions.Clear();
                auctions.Clear();
            }

            if (pkt.NewAuctions != null)
            {
                foreach (var auction in pkt.NewAuctions)
                {
                    auctions[auction.AuctionId] = auction;
                    auction.ItemStack.ResolveBlockOrItem(capi.World);

                    if (auction.State == EnumAuctionState.Active || (auction.State == EnumAuctionState.Sold && (auction.RetrievableTotalHours - capi.World.Calendar.TotalHours) > 0))
                    {
                        insertOrUpdate(activeAuctions, auction);
                    } else
                    {
                        remove(activeAuctions, auction);
                    }
                    if (
                        (auction.SellerUid == capi.World.Player.PlayerUID) ||
                        (auction.State == EnumAuctionState.Sold && auction.BuyerUid == capi.World.Player.PlayerUID)
                    )
                    {
                        insertOrUpdate(ownAuctions, auction);
                    } else
                    {
                        remove(ownAuctions, auction);
                    }

                }
            }

            if (pkt.RemovedAuctions != null)
            {
                foreach (var auctionId in pkt.RemovedAuctions)
                {
                    auctions.Remove(auctionId);

                    RemoveFromList(auctionId, activeAuctions);
                    RemoveFromList(auctionId, ownAuctions);
                }
            }

            activeAuctions.Sort();
            ownAuctions.Sort();

            OnCellUpdateClient?.Invoke();
        }

        void remove(List<Auction> auctions, Auction auction)
        {
            for (int i = 0; i < auctions.Count; i++)
            {
                if (auctions[i].AuctionId == auction.AuctionId)
                {
                    auctions.RemoveAt(i);
                    return;
                }
            }
        }

        void insertOrUpdate(List<Auction> auctions, Auction auction)
        {
            bool found = false;
            for (int i = 0; !found && i < auctions.Count; i++)
            {
                if (auctions[i].AuctionId == auction.AuctionId)
                {
                    auctions[i] = auction;
                    return;
                }
            }

            auctions.Add(auction);
        }


        private void RemoveFromList(long auctionId, List<Auction> auctions)
        {
            for (int i = 0; i < auctions.Count; i++)
            {
                var litem = auctions[i];
                if (litem.AuctionId == auctionId)
                {
                    auctions.RemoveAt(i);
                    i--;
                }
            }
        }

        public void DidEnterAuctionHouse()
        {
            clientCh.SendPacket(new AuctionActionPacket() { Action = EnumAuctionAction.EnterAuctionHouse });
        }

        public void DidLeaveAuctionHouse()
        {
            clientCh.SendPacket(new AuctionActionPacket() { Action = EnumAuctionAction.LeaveAuctionHouse });
        }

        public void PlaceAuctionClient(Entity traderEntity, int price, int durationWeeks = 1)
        {
            clientCh.SendPacket(new AuctionActionPacket() { Action = EnumAuctionAction.PlaceAuction, AtAuctioneerEntityId = traderEntity.EntityId, Price = price, DurationWeeks = durationWeeks });
        }

        public void BuyAuctionClient(Entity traderEntity, long auctionId, bool withDelivery)
        {
            clientCh.SendPacket(new AuctionActionPacket() { Action = EnumAuctionAction.PurchaseAuction, AtAuctioneerEntityId = traderEntity.EntityId, AuctionId = auctionId, WithDelivery = withDelivery });
        }

        public void RetrieveAuctionClient(Entity traderEntity, long auctionId)
        {
            clientCh.SendPacket(new AuctionActionPacket() { Action = EnumAuctionAction.RetrieveAuction, AtAuctioneerEntityId = traderEntity.EntityId, AuctionId = auctionId });
        }

        #endregion


        #region Server

        bool auctionHouseEnabled => sapi.World.Config.GetBool("auctionHouse", true);
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;
            api.Network.GetChannel("auctionHouse").SetMessageHandler<AuctionActionPacket>(onAuctionAction);
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.PlayerDisconnect += Event_PlayerDisconnect;
            api.Event.PlayerJoin += Event_PlayerJoin;
            api.Event.RegisterGameTickListener(TickAuctions, 5000);
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            int expiredAuctions = 0;
            int activeAuctions = 0;
            int readyPurchasedAuctions = 0;
            int enroutePurchasedAuctions = 0;
            int soldAuctions = 0;

            foreach (var auction in auctionsData.auctions.Values)
            {
                if (auction.BuyerUid == byPlayer.PlayerUID)
                {
                    if (auction.RetrievableTotalHours <= sapi.World.Calendar.TotalHours) readyPurchasedAuctions++;
                    else enroutePurchasedAuctions++;
                }
                if (auction.SellerUid == byPlayer.PlayerUID)
                {
                    if (auction.State == EnumAuctionState.Sold || auction.State == EnumAuctionState.SoldRetrieved) soldAuctions++;
                    if (auction.State == EnumAuctionState.Expired) expiredAuctions++;
                    if (auction.State == EnumAuctionState.Active) activeAuctions++;
                }
            }

            if (expiredAuctions + activeAuctions + readyPurchasedAuctions + enroutePurchasedAuctions + soldAuctions > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(Lang.Get("Auction House: You have") + " ");
                if (activeAuctions > 0) { sb.AppendLine(Lang.Get("{0} active auctions", activeAuctions)); }
                if (soldAuctions > 0) { sb.AppendLine(Lang.Get("{0} sold auctions", soldAuctions)); }
                if (expiredAuctions > 0) { sb.AppendLine(Lang.Get("{0} expired auctions", expiredAuctions)); }
                if (enroutePurchasedAuctions > 0) { sb.AppendLine(Lang.Get("{0} purchased auctions en-route", readyPurchasedAuctions)); }
                if (readyPurchasedAuctions > 0) { sb.AppendLine(Lang.Get("{0} purchased auctions ready for pick-up", readyPurchasedAuctions)); }

                byPlayer.SendMessage(GlobalConstants.GeneralChatGroup, sb.ToString(), EnumChatType.Notification);
            }
        }

        private void Event_PlayerDisconnect(IServerPlayer byPlayer)
        {
            if (createAuctionSlotByPlayer.TryGetValue(byPlayer.PlayerUID, out var inv))
            {
                byPlayer.InventoryManager.CloseInventoryAndSync(createAuctionSlotByPlayer[byPlayer.PlayerUID]);
                createAuctionSlotByPlayer.Remove(byPlayer.PlayerUID);
            }
        }


        private void onAuctionAction(IServerPlayer fromPlayer, AuctionActionPacket pkt)
        {
            if (!auctionHouseEnabled) return;

            switch (pkt.Action)
            {
                case EnumAuctionAction.EnterAuctionHouse:

                    if (!createAuctionSlotByPlayer.ContainsKey(fromPlayer.PlayerUID))
                    {
                        var ainv = createAuctionSlotByPlayer[fromPlayer.PlayerUID] = new InventoryGeneric(1, "auctionslot-" + fromPlayer.PlayerUID, sapi);
                        // a negative weight prevents the auction slot from being consider as a suitable slot when shift clicking an item in the hotbar, that is because the default weight is 0 and it checks for >= 0
                        // this one here is for good measure but the important one in on the client side in the GuiDialogTrader constructor
                        ainv.OnGetSuitability = (s, t, isMerge) => -1f;
                        ainv.OnInventoryClosed += (plr) => ainv.DropAll(plr.Entity.Pos.XYZ);
                    }

                    fromPlayer.InventoryManager.OpenInventory(createAuctionSlotByPlayer[fromPlayer.PlayerUID]);

                    sendAuctions(auctions.Values, null, true, fromPlayer);

                    break;
                case EnumAuctionAction.LeaveAuctionHouse:
                    Event_PlayerDisconnect(fromPlayer);
                    break;
                case EnumAuctionAction.PurchaseAuction:
                    {
                        var auctioneerEntity = sapi.World.GetEntityById(pkt.AtAuctioneerEntityId);
                        PurchaseAuction(pkt.AuctionId, fromPlayer.Entity, auctioneerEntity, pkt.WithDelivery, out string failureCode);
                        serverCh.SendPacket(new AuctionActionResponsePacket() { Action = pkt.Action, AuctionId = pkt.AuctionId, ErrorCode = failureCode }, fromPlayer);
                        break;
                    }
                case EnumAuctionAction.RetrieveAuction:
                    {
                        ItemStack stack = RetrieveAuction(pkt.AuctionId, pkt.AtAuctioneerEntityId, fromPlayer.Entity, out string failureCode);
                        if (stack != null)
                        {
                            if (!fromPlayer.InventoryManager.TryGiveItemstack(stack, true))
                            {
                                sapi.World.SpawnItemEntity(stack, fromPlayer.Entity.Pos.XYZ);
                            }

                            sapi.World.Logger.Audit("{0} Got 1x{1} from Auction at {2}.",
                                fromPlayer.PlayerName,
                                stack.Collectible.Code,
                                fromPlayer.Entity.Pos
                            );
                        }
                        serverCh.SendPacket(new AuctionActionResponsePacket() { Action = pkt.Action, AuctionId = pkt.AuctionId, ErrorCode = failureCode, MoneyReceived = stack?.Collectible.Attributes?["currency"].Exists == true }, fromPlayer);



                        break;
                    }
                case EnumAuctionAction.PlaceAuction:

                    if (createAuctionSlotByPlayer.TryGetValue(fromPlayer.PlayerUID, out var inv))
                    {
                        if (inv.Empty)
                        {
                            serverCh.SendPacket(new AuctionActionResponsePacket() { Action = pkt.Action, AuctionId = pkt.AuctionId, ErrorCode = "emptyauctionslot" }, fromPlayer);
                            break;
                        }

                        pkt.DurationWeeks = Math.Max(1, pkt.DurationWeeks);

                        var auctioneerEntity = sapi.World.GetEntityById(pkt.AtAuctioneerEntityId);
                        PlaceAuction(inv[0], inv[0].StackSize, pkt.Price, pkt.DurationWeeks * 7 * 24, pkt.DurationWeeks / DurationWeeksMul, fromPlayer.Entity, auctioneerEntity, out string failureCode);

                        if (failureCode != null)
                        {
                            inv.DropAll(fromPlayer.Entity.Pos.XYZ);
                        }

                        auctionsData.DebtToTraderByPlayer.TryGetValue(fromPlayer.PlayerUID, out float debt);

                        serverCh.SendPacket(new AuctionActionResponsePacket() { Action = pkt.Action, AuctionId = pkt.AuctionId, ErrorCode = failureCode }, fromPlayer);
                        serverCh.SendPacket(new DebtPacket() { TraderDebt = debt }, fromPlayer);
                    }

                    break;
            }
        }

        /// <summary>
        /// Returns all not yet expired auctions
        /// </summary>
        /// <returns></returns>
        public List<Auction> GetActiveAuctions()
        {
            return auctions.Values.Where(ac => ac.State == EnumAuctionState.Active || ac.State == EnumAuctionState.Sold).ToList();
        }

        /// <summary>
        /// Returns all own auctions, active or expired
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public List<Auction> GetAuctionsFrom(IPlayer player)
        {
            List<Auction> auctions = new List<Auction>();
            foreach (var auction in this.auctions.Values)
            {
                if (auction.SellerName == player.PlayerUID)
                {
                    auctions.Add(auction);
                }
            }

            return auctions;
        }



        private void TickAuctions(float dt)
        {
            var totalhours = sapi.World.Calendar.TotalHours;
            var auctions = this.auctions.Values.ToArray();

            List<Auction> toSendAuctions = new List<Auction>();

            foreach (var auction in auctions)
            {
                if (auction.State == EnumAuctionState.Active && auction.ExpireTotalHours < totalhours)
                {
                    auction.State = EnumAuctionState.Expired;
                    toSendAuctions.Add(auction);
                }
            }

            sendAuctions(toSendAuctions, null);
        }

        public virtual int GetDepositCost(ItemSlot forItem)
        {
            return 1;
        }


        public void PlaceAuction(ItemSlot slot, int quantity, int price, double durationHours, int depositCost, EntityAgent sellerEntity, Entity auctioneerEntity, out string failureCode)
        {
            if (slot.StackSize < quantity)
            {
                failureCode = "notenoughitems";
                return;
            }

            if (GetAuctionsFrom((sellerEntity as EntityPlayer).Player).Count > 30)
            {
                failureCode = "toomanyauctions";
                return;
            }

            int monehs = InventoryTrader.GetPlayerAssets(sellerEntity);
            if (monehs < GetDepositCost(slot) * depositCost)
            {
                failureCode = "notenoughgears";
                return;
            }

            if (price < 1)
            {
                failureCode = "atleast1gear";
                return;
            }


            failureCode = null;
            InventoryTrader.DeductFromEntity(sapi, sellerEntity, depositCost);
            (auctioneerEntity as EntityTradingHumanoid).Inventory?.GiveToTrader(depositCost);

            long id = ++auctionsData.nextAuctionId;

            string sellerName = sellerEntity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (sellerName == null) sellerName = sellerEntity.Properties.Code.ToShortString();


            string uid = (sellerEntity as EntityPlayer)?.PlayerUID ?? "";
            auctionsData.DebtToTraderByPlayer.TryGetValue(uid, out float debt);

            float traderCutGears = price * SalesCutRate + debt;

            auctionsData.DebtToTraderByPlayer[uid] = traderCutGears - (int)traderCutGears;


            var auction = new Auction()
            {
                AuctionId = id,
                ExpireTotalHours = sapi.World.Calendar.TotalHours + durationHours,
                ItemStack = slot.TakeOut(quantity),
                PostedTotalHours = sapi.World.Calendar.TotalHours,
                Price = price,
                TraderCut = (int)traderCutGears,
                SellerName = sellerName,
                SellerUid = (sellerEntity as EntityPlayer)?.PlayerUID,
                SellerEntityId = sellerEntity.EntityId,
                SrcAuctioneerEntityPos = auctioneerEntity.Pos.XYZ,
                SrcAuctioneerEntityId = auctioneerEntity.EntityId
            };

            auctions.Add(id, auction);
            slot.MarkDirty();

            sendAuctions(new Auction[] { auction }, null);
        }

        public void PurchaseAuction(long auctionId, EntityAgent buyerEntity, Entity auctioneerEntity, bool withDelivery, out string failureCode)
        {
            if (auctions.TryGetValue(auctionId, out var auction))
            {
                if ((buyerEntity as EntityPlayer)?.PlayerUID == auction.SellerUid)
                {
                    failureCode = "ownauction";
                    return;
                }

                // Already purchased
                if (auction.BuyerName != null)
                {
                    failureCode = "alreadypurchased";
                    return;
                }

                int monehs = InventoryTrader.GetPlayerAssets(buyerEntity);
                int deliveryCosts = withDelivery ? DeliveryCostsByDistance(auctioneerEntity.Pos.XYZ, auction.SrcAuctioneerEntityPos) : 0;

                int totalcost = auction.Price + deliveryCosts;

                if (monehs < totalcost)
                {
                    failureCode = "notenoughgears";
                    return;
                }

                InventoryTrader.DeductFromEntity(sapi, buyerEntity, totalcost);
                (auctioneerEntity as EntityTradingHumanoid).Inventory?.GiveToTrader((int)(auction.Price * SalesCutRate + deliveryCosts));

                string buyerName = buyerEntity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
                if (buyerName == null) buyerName = buyerEntity.Properties.Code.ToShortString();

                auction.BuyerName = buyerName;
                auction.WithDelivery = withDelivery;
                auction.BuyerUid = (buyerEntity as EntityPlayer)?.PlayerUID;
                auction.RetrievableTotalHours = sapi.World.Calendar.TotalHours + 1 + 3*deliveryCosts;
                auction.DstAuctioneerEntityId = withDelivery ? auctioneerEntity.EntityId : auction.SrcAuctioneerEntityId;
                auction.DstAuctioneerEntityPos = withDelivery ? auctioneerEntity.Pos.XYZ : auction.SrcAuctioneerEntityPos;
                auction.State = EnumAuctionState.Sold;

                sendAuctions(new Auction[] { auction }, null);

                failureCode = null;
                return;
            }

            failureCode = "nosuchauction";
            return;
        }

        public void DeleteActiveAuction(long auctionId)
        {
            auctions.Remove(auctionId);
            sendAuctions(null, new long[] { auctionId });
        }

        public ItemStack RetrieveAuction(long auctionId, long atAuctioneerEntityId, EntityPlayer reqEntity, out string failureCode)
        {
            if (!auctions.TryGetValue(auctionId, out var auction))
            {
                failureCode = "nosuchauction";
                return null;
            }

            // Buyer: Retrieve your purchase
            if (reqEntity.PlayerUID == auction.BuyerUid)
            {
                if (auction.RetrievableTotalHours > sapi.World.Calendar.TotalHours)
                {
                    failureCode = "notyetretrievable";
                    return null;
                }

                if (auction.State == EnumAuctionState.SoldRetrieved)
                {
                    failureCode = "alreadyretrieved";
                    return null;
                }

                if (auction.State == EnumAuctionState.Expired || auction.State == EnumAuctionState.Active)
                {
                    sapi.Logger.Notification("Auction was bought by {0}, but is in state {1}? O.o Setting it to sold state.");
                    auction.State = EnumAuctionState.Sold;
                    auction.RetrievableTotalHours = sapi.World.Calendar.TotalHours + 6;
                    failureCode = null;
                    sendAuctions(new Auction[] { auction }, null);
                    return null;
                }

                if (!auction.WithDelivery && (auction.SrcAuctioneerEntityId != atAuctioneerEntityId && auction.SrcAuctioneerEntityPos.DistanceTo(reqEntity.Pos.XYZ) > 100))
                {
                    failureCode = "wrongtrader";
                    return null;
                }

                auction.State = EnumAuctionState.SoldRetrieved;

                if (auction.MoneyCollected)
                {
                    auctions.Remove(auctionId);
                    sendAuctions(null, new long[] { auctionId });
                } else
                {
                    sendAuctions(new Auction[] { auction }, null);
                }

                failureCode = null;
                return auction.ItemStack.Clone();
            }


            // Seller: Cancel auction or retrieve expired auction or retreive money
            if (reqEntity.PlayerUID == auction.SellerUid)
            {
                if (auction.State == EnumAuctionState.Active)
                {
                    auction.State = EnumAuctionState.Expired;
                    auction.RetrievableTotalHours = sapi.World.Calendar.TotalHours + 6;
                    failureCode = null;
                    sendAuctions(new Auction[] { auction }, null);
                    return null;
                }

                if (auction.RetrievableTotalHours > sapi.World.Calendar.TotalHours)
                {
                    failureCode = "notyetretrievable";
                    return null;
                }

                if (auction.State == EnumAuctionState.Expired)
                {
                    auctions.Remove(auctionId);
                    sendAuctions(null, new long[] { auctionId });

                    failureCode = null;
                    return auction.ItemStack;
                }


                if (auction.State == EnumAuctionState.Sold || auction.State == EnumAuctionState.SoldRetrieved)
                {
                    if (auction.MoneyCollected)
                    {
                        failureCode = "moneyalreadycollected";
                        return null;
                    }

                    if (auction.State == EnumAuctionState.SoldRetrieved)
                    {
                        auctions.Remove(auctionId);
                        sendAuctions(null, new long[] { auctionId });
                    } else
                    {
                        sendAuctions(new Auction[] { auction }, null);
                    }

                    failureCode = null;
                    auction.MoneyCollected = true;

                    var stack = SingleCurrencyStack.Clone();

                    stack.StackSize = auction.Price - auction.TraderCut;

                    return stack;
                }

                failureCode = "codingerror";
                return null;
            }


            failureCode = "notyouritem";
            return null;
        }


        private void sendAuctions(IEnumerable<Auction> newauctions, long[] removedauctions, bool isFullUpdate = false, IServerPlayer toPlayer = null)
        {
            var newauctionsa = newauctions?.ToArray();
            if ((newauctionsa?.Length ?? 0) == 0 && (removedauctions?.Length ?? 0) == 0 && !isFullUpdate) return;

            float debt = 0;
            if (toPlayer != null)
            {
                auctionsData.DebtToTraderByPlayer.TryGetValue(toPlayer.PlayerUID, out debt);
            }

            var pkt = new AuctionlistPacket() { NewAuctions = newauctionsa, RemovedAuctions = removedauctions, IsFullUpdate = isFullUpdate, TraderDebt = debt };
            if (toPlayer != null)
            {
                sapi.Network.GetChannel("auctionHouse").SendPacket(pkt, toPlayer);
            }
            else
            {
                foreach (var playeruid in createAuctionSlotByPlayer.Keys)
                {
                    IServerPlayer plr = sapi.World.PlayerByUid(playeruid) as IServerPlayer;
                    if (plr != null)
                    {
                        sapi.Network.GetChannel("auctionHouse").SendPacket(pkt, plr);
                    }
                }
            }
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("auctionsData", SerializerUtil.Serialize(auctionsData));
        }

        private void Event_SaveGameLoaded()
        {
            var item = sapi.World.GetItem(new AssetLocation("gear-rusty"));
            if (item == null) return;
            SingleCurrencyStack = new ItemStack(item);

            byte[] data = sapi.WorldManager.SaveGame.GetData("auctionsData");
            if (data != null)
            {
                this.auctionsData = SerializerUtil.Deserialize<AuctionsData>(data);

                foreach (var auction in auctionsData.auctions.Values)
                {
                    auction.ItemStack?.ResolveBlockOrItem(sapi.World);
                }
            }

            loadPricingConfig();
        }
        #endregion
    }
}
