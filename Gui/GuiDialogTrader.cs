using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
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


        public GuiDialogTrader(InventoryTrader traderInventory, EntityAgent owningEntity, ICoreClientAPI capi) : base(capi)
        {
            this.traderInventory = traderInventory;
            this.owningEntity = owningEntity;

            


            double pad = GuiElementItemSlotGrid.unscaledSlotPadding;

            ElementBounds leftTopSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad, 40 + pad, 4, 4).FixedGrow(2 * pad, 2 * pad);
            ElementBounds rightTopSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad + leftTopSlotBounds.fixedWidth + 20, 40 + pad, 4, 4).FixedGrow(2 * pad, 2 * pad);

            ElementBounds rightBotSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad + leftTopSlotBounds.fixedWidth + 20, 15 + pad, 4, 1).FixedGrow(2 * pad, 2 * pad).FixedUnder(rightTopSlotBounds, 5);
            ElementBounds leftBotSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad, 15 + pad, 4, 1).FixedGrow(2 * pad, 2 * pad).FixedUnder(leftTopSlotBounds, 5);
            
            //ElementBounds chanceInputBounds = ElementBounds.Fixed(3, 0, 48, 30).FixedUnder(l, -4);

            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(ElementGeometrics.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;



            ElementBounds costTextBounds = ElementBounds.Fixed(pad, 55 + 2 * pad + leftTopSlotBounds.fixedHeight + leftBotSlotBounds.fixedHeight, 150, 25);
            ElementBounds offerTextBounds = ElementBounds.Fixed(leftTopSlotBounds.fixedWidth + pad + 20, 55 + 2 * pad + leftTopSlotBounds.fixedHeight + leftBotSlotBounds.fixedHeight, 150, 25);

            ElementBounds traderMoneyBounds = offerTextBounds.FlatCopy().WithAddedFixedPosition(0, offerTextBounds.fixedHeight);
            ElementBounds playerMoneyBounds = costTextBounds.FlatCopy().WithAddedFixedPosition(0, costTextBounds.fixedHeight);

            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-ElementGeometrics.DialogToScreenPadding, 0);


            SingleComposer = 
                capi.Gui
                .CreateCompo("itemlootrandomizer", dialogBounds, false)
                .AddDialogBG(bgBounds, true)
                .AddDialogTitleBar(owningEntity.GetBehavior<EntityBehaviorNameTag>().DisplayName + " Has Wares, If You Have Coin", OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddStaticText("You can Buy", CairoFont.WhiteDetailText(), ElementBounds.Fixed(pad, 20 + pad, 150, 25))
                    .AddStaticText("You can Sell", CairoFont.WhiteDetailText(), ElementBounds.Fixed(leftTopSlotBounds.fixedWidth + pad + 20, 20 + pad, 150, 25))

                    .AddItemSlotGrid(traderInventory, DoSendPacket, 4, (new int[16]).Fill(i => i), leftTopSlotBounds, "traderSellingSlots")
                    .AddItemSlotGrid(traderInventory, DoSendPacket, 4, (new int[4]).Fill(i => 16 + i), leftBotSlotBounds, "playerBuyingSlots")

                    .AddItemSlotGrid(traderInventory, DoSendPacket, 4, (new int[16]).Fill(i => 16 + 4 + i), rightTopSlotBounds, "traderBuyingSlots")
                    .AddItemSlotGrid(traderInventory, DoSendPacket, 4, (new int[4]).Fill(i => 16 + 4 + 16 + i), rightBotSlotBounds, "playerSellingSlots")

                    .AddStaticText("Your Selection", CairoFont.WhiteDetailText(), ElementBounds.Fixed(pad, 40 + 2*pad + leftTopSlotBounds.fixedHeight, 150, 25))
                    .AddStaticText("Your Offer", CairoFont.WhiteDetailText(), ElementBounds.Fixed(leftTopSlotBounds.fixedWidth + pad + 20, 40 + 2*pad + leftTopSlotBounds.fixedHeight, 150, 25))

                    // Cost
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, costTextBounds, 1, "costText")
                    // Player money
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, playerMoneyBounds , 1, "playerMoneyText")
                    // Offer
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, offerTextBounds, 1, "gainText")
                    // Trader money
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, traderMoneyBounds , 1, "traderMoneyText")

                    .AddSmallButton("Goodbye!", OnByeClicked, leftButton.FixedUnder(playerMoneyBounds, 30).WithFixedPadding(8, 5))
                    .AddSmallButton("Buy / Sell", OnBuySellClicked, rightButton.FixedUnder(traderMoneyBounds, 30).WithFixedPadding(8, 5), EnumButtonStyle.Normal, EnumTextOrientation.Left, "buysellButton")
                    

                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetButton("buysellButton").Enabled = false;

            CalcAndUpdateAssetsDisplay();
        }

        void CalcAndUpdateAssetsDisplay()
        {
            int playerAssets = traderInventory.GetPlayerAssets(capi.World.Player);
            SingleComposer.GetDynamicText("playerMoneyText").SetNewText(Lang.Get("You have {0} Gears", playerAssets));

            int traderAssets = traderInventory.GetTraderAssets();
            SingleComposer.GetDynamicText("traderMoneyText").SetNewText(Lang.Get("{0} has {1} Gears", owningEntity.GetBehavior<EntityBehaviorNameTag>().DisplayName, traderAssets));
        }

        private void TraderInventory_SlotModified(int slotid)
        {
            int totalCost = traderInventory.GetTotalCost();
            int totalGain = traderInventory.GetTotalGain();

            SingleComposer.GetDynamicText("costText").SetNewText(totalCost > 0 ? Lang.Get("Total Cost: {0} Gears", totalCost) : "");
            SingleComposer.GetDynamicText("gainText").SetNewText(totalGain > 0 ? Lang.Get("Total Gain: {0} Gears", totalGain) : "");

            SingleComposer.GetButton("buysellButton").Enabled = totalCost > 0 || totalGain > 0;

            CalcAndUpdateAssetsDisplay();
        }

        private bool OnBuySellClicked()
        {
            EnumTransactionResult result = traderInventory.TryBuySell(capi.World.Player);
            if (result == EnumTransactionResult.Success)
            {
                capi.Gui.PlaySound(new AssetLocation("sounds/effect/cashregister"), false);
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
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();

            traderInventory.SlotModified -= TraderInventory_SlotModified;

            (owningEntity as EntityTrader).talkUtil.Talk(EnumTalkType.Goodbye);

            capi.Network.SendPacketClient(capi.World.Player.InventoryManager.CloseInventory(traderInventory));

            SingleComposer.GetSlotGrid("traderSellingSlots").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("playerBuyingSlots").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("traderBuyingSlots").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("playerSellingSlots").OnGuiClosed(capi);
        }

        public override void OnBeforeRenderFrame3D(float deltaTime)
        {
            base.OnBeforeRenderFrame3D(deltaTime);

            if (notifyPlayerMoneyTextSeconds > 0)
            {
                notifyPlayerMoneyTextSeconds -= deltaTime;

                if (notifyPlayerMoneyTextSeconds <= 0)
                {
                    SingleComposer.GetDynamicText("playerMoneyText").Bounds.absFixedX = prevPlrAbsFixedX;
                    SingleComposer.GetDynamicText("playerMoneyText").Bounds.absFixedY = prevPlrAbsFixedY;
                } else
                {
                    SingleComposer.GetDynamicText("playerMoneyText").Bounds.absFixedX = prevPlrAbsFixedX + notifyPlayerMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                    SingleComposer.GetDynamicText("playerMoneyText").Bounds.absFixedY = prevPlrAbsFixedY + notifyPlayerMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                }
            }


            if (notifyTraderMoneyTextSeconds > 0)
            {
                notifyTraderMoneyTextSeconds -= deltaTime;

                if (notifyTraderMoneyTextSeconds <= 0)
                {
                    SingleComposer.GetDynamicText("traderMoneyText").Bounds.absFixedX = prevPlrAbsFixedX;
                    SingleComposer.GetDynamicText("traderMoneyText").Bounds.absFixedY = prevPlrAbsFixedY;
                }
                else
                {
                    SingleComposer.GetDynamicText("traderMoneyText").Bounds.absFixedX = prevTdrAbsFixedX + notifyTraderMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                    SingleComposer.GetDynamicText("traderMoneyText").Bounds.absFixedY = prevTdrAbsFixedY + notifyTraderMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                }
            }

        }

    }
}
