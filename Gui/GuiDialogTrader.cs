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

        double prevAbsFixedX, prevAbsFixedY;
        double notifyMoneyTextSeconds;

        public GuiDialogTrader(InventoryTrader traderInventory, EntityAgent owningEntity, ICoreClientAPI capi) : base(capi)
        {
            this.traderInventory = traderInventory;
            this.owningEntity = owningEntity;

            traderInventory.SlotModified += TraderInventory_SlotModified;


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

                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, ElementBounds.Fixed(pad, 55 + 2 * pad + leftTopSlotBounds.fixedHeight + leftBotSlotBounds.fixedHeight, 150, 25), 1, "costText")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Left, ElementBounds.Fixed(leftTopSlotBounds.fixedWidth + pad + 20, 55 + 2 * pad + leftTopSlotBounds.fixedHeight + leftBotSlotBounds.fixedHeight, 150, 25), 1, "gainText")
                    

                    .AddSmallButton("Goodbye!", OnByeClicked, leftButton.FixedUnder(leftBotSlotBounds, 30).WithFixedPadding(8, 5))
                    .AddSmallButton("Buy / Sell", OnByeSellClicked, rightButton.FixedUnder(rightBotSlotBounds, 30).WithFixedPadding(8, 5), EnumButtonStyle.Normal, EnumTextOrientation.Left, "buysellButton")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), EnumTextOrientation.Center, ElementBounds.Fixed(EnumDialogArea.CenterFixed, 0, leftButton.fixedY + 6, 200, 25), 1, "moneyText")

                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetButton("buysellButton").Enabled = false;

            CalcOwnMoney();
        }

        void CalcOwnMoney()
        {
            int totalAssets = 0;

            capi.World.Player.Entity.WalkInventory((invslot) =>
            {
                if (invslot is CreativeSlot) return true;
                if (invslot.Itemstack == null || invslot.Itemstack.Collectible.Attributes == null) return true;

                JsonObject obj = invslot.Itemstack.Collectible.Attributes["currency"];
                if (obj.Exists && obj["value"].Exists)
                {
                    totalAssets += obj["value"].AsInt(0) * invslot.StackSize;
                }

                return true;
            });

            SingleComposer.GetDynamicText("moneyText").SetNewText(Lang.Get("You have {0} Gears", totalAssets));
        }

        private void TraderInventory_SlotModified(int slotid)
        {
            int totalCost = traderInventory.GetTotalCost();
            int totalGain = traderInventory.GetTotalGain();

            SingleComposer.GetDynamicText("costText").SetNewText(totalCost > 0 ? Lang.Get("Total Cost: {0} Gears", totalCost) : "");
            SingleComposer.GetDynamicText("gainText").SetNewText(totalGain > 0 ? Lang.Get("Total Gain: {0} Gears", totalGain) : "");

            SingleComposer.GetButton("buysellButton").Enabled = totalCost > 0 || totalGain > 0;

            CalcOwnMoney();
        }

        private bool OnByeSellClicked()
        {
            if (traderInventory.DoBuySell(capi.World.Player))
            {
                capi.Gui.PlaySound(new AssetLocation("sounds/effect/cashregister"), false);
            } else
            {
                (owningEntity as EntityTrader).talkUtil.Talk(EnumTalkType.Complain);
                if (notifyMoneyTextSeconds <= 0)
                {
                    prevAbsFixedX = SingleComposer.GetDynamicText("moneyText").Bounds.absFixedX;
                    prevAbsFixedY = SingleComposer.GetDynamicText("moneyText").Bounds.absFixedY;
                }
                notifyMoneyTextSeconds = 1.5f;

            }

            capi.Network.SendEntityPacket(owningEntity.EntityId, 1000, null);

            TraderInventory_SlotModified(0);
            CalcOwnMoney();

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

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();

            capi.Network.SendPacketClient(capi.World.Player.InventoryManager.CloseInventory(traderInventory));

            SingleComposer.GetSlotGrid("traderSellingSlots").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("playerBuyingSlots").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("traderBuyingSlots").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("playerSellingSlots").OnGuiClosed(capi);
        }

        public override void OnBeforeRenderFrame3D(float deltaTime)
        {
            base.OnBeforeRenderFrame3D(deltaTime);

            if (notifyMoneyTextSeconds > 0)
            {
                notifyMoneyTextSeconds -= deltaTime;

                if (notifyMoneyTextSeconds <= 0)
                {
                    SingleComposer.GetDynamicText("moneyText").Bounds.absFixedX = prevAbsFixedX;
                    SingleComposer.GetDynamicText("moneyText").Bounds.absFixedY = prevAbsFixedY;
                } else
                {
                    SingleComposer.GetDynamicText("moneyText").Bounds.absFixedX = prevAbsFixedX + notifyMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                    SingleComposer.GetDynamicText("moneyText").Bounds.absFixedY = prevAbsFixedY + notifyMoneyTextSeconds * (capi.World.Rand.NextDouble() * 4 - 2);
                }
            }

        }

    }
}
