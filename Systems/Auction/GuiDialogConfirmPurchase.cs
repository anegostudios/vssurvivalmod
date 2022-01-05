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
    public class GuiDialogConfirmPurchase : GuiDialog
    {
        ModSystemAuction auctionSys;
        EntityAgent buyerEntity;
        EntityAgent traderEntity;
        Auction auction;

        ElementBounds dialogBounds;

        public override double InputOrder => 0.1;
        public override double DrawOrder => 1;

        public override bool UnregisterOnClose => true;

        public GuiDialogConfirmPurchase(ICoreClientAPI capi, EntityAgent buyerEntity, EntityAgent auctioneerEntity, Auction auction) : base(capi)
        {
            this.buyerEntity = buyerEntity;
            this.traderEntity = auctioneerEntity;
            this.auction = auction;

            auctionSys = capi.ModLoader.GetModSystem<ModSystemAuction>();
            Init();
        }

        public override string ToggleKeyCombinationCode => null;


        public void Init()
        {
            var descBounds = ElementBounds.Fixed(0, 30, 400, 80);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            bgBounds.verticalSizing = ElementSizing.FitToChildren;
            bgBounds.horizontalSizing = ElementSizing.Fixed;
            bgBounds.fixedWidth = 300;


            #region Text
            var deliveryCosts = auctionSys.DeliveryCostsByDistance(traderEntity.Pos.XYZ, auction.SrcAuctioneerEntityPos);

            RichTextComponentBase[] stackComps = new RichTextComponentBase[] { 
                new ItemstackTextComponent(capi, auction.ItemStack, 60, 10), 
                new RichTextComponent(capi, auction.ItemStack.GetName() + "\r\n", CairoFont.WhiteSmallText())
            };

            stackComps = stackComps.Append(VtmlUtil.Richtextify(capi, auction.ItemStack.GetDescription(capi.World, new DummySlot(auction.ItemStack)), CairoFont.WhiteDetailText()));


            var font = CairoFont.WhiteDetailText();
            double fl = font.UnscaledFontsize;
            ItemStack gearStack = auctionSys.SingleCurrencyStack;
            var deliveryCostComps = new RichTextComponentBase[] {
                new RichTextComponent(capi, Lang.Get("Delivery: {0}", deliveryCosts), font) { PaddingRight = 10, VerticalAlign = EnumVerticalAlign.Top },
                new ItemstackTextComponent(capi, gearStack, fl * 2.5f, 0, EnumFloat.Inline) { VerticalAlign = EnumVerticalAlign.Top, offX = -GuiElement.scaled(fl * 0.5f), offY = -GuiElement.scaled(fl * 0.75f) }
            };

            

            RichTextComponentBase[] totalCostComps = new RichTextComponentBase[]
            {
                new RichTextComponent(capi, Lang.Get("Total Cost: {0}", auction.Price+deliveryCosts), font) { PaddingRight = 10, VerticalAlign = EnumVerticalAlign.Top },
                new ItemstackTextComponent(capi, gearStack, fl * 2.5f, 0, EnumFloat.Inline) { VerticalAlign = EnumVerticalAlign.Top, offX = -GuiElement.scaled(fl * 0.5f), offY = -GuiElement.scaled(fl * 0.75f) }
            };
            #endregion



            Composers["confirmauctionpurchase"] = capi.Gui
                .CreateCompo("tradercreateauction-" + buyerEntity.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("Purchase this item?"), OnCreateAuctionClose)
                .BeginChildElements(bgBounds)
                        .AddRichtext(stackComps, descBounds, "itemstack")
            ;

            var ri = Composers["confirmauctionpurchase"].GetRichtext("itemstack");
            ri.BeforeCalcBounds();

            double y = Math.Max(110, descBounds.fixedHeight + 20);

            ElementBounds deliverySwitchBounds = ElementBounds.Fixed(0, y, 35, 25);
            ElementBounds deliveryTextBounds = ElementBounds.Fixed(0, y + 3, 250, 25).FixedRightOf(deliverySwitchBounds, 0);


            ElementBounds deliveryCostBounds = ElementBounds.Fixed(0, 0, 200, 30).FixedUnder(deliveryTextBounds, 20);
            ElementBounds totalCostBounds = ElementBounds.Fixed(0, 0, 150, 30).FixedUnder(deliveryCostBounds, 0);

            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5).FixedUnder(totalCostBounds, 15);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(8, 5).FixedUnder(totalCostBounds, 15);


            Composers["confirmauctionpurchase"]
                        .AddSwitch(onDeliveryModeChanged, deliverySwitchBounds, "delivery", 25)
                        .AddStaticText(Lang.Get("Deliver to current trader"), CairoFont.WhiteSmallText(), deliveryTextBounds)


                        .AddRichtext(deliveryCostComps, deliveryCostBounds, "deliveryCost")
                        .AddRichtext(totalCostComps, totalCostBounds, "totalCost")

                        .AddSmallButton(Lang.Get("Cancel"), OnCancel, leftButton)
                        .AddSmallButton(Lang.Get("Purchase"), OnPurchase, rightButton, EnumButtonStyle.Normal, EnumTextOrientation.Left, "buysellButton")
                .EndChildElements()
                .Compose()
            ;

            Composers["confirmauctionpurchase"].GetSwitch("delivery").On = true;
        }


        private void onDeliveryModeChanged(bool on)
        {
            int deliveryCosts = (int)Math.Ceiling((traderEntity.Pos.XYZ.DistanceTo(auction.SrcAuctioneerEntityPos) - 200) / 2000f);
            var rtele = Composers["confirmauctionpurchase"].GetRichtext("totalCost");

            (rtele.Components[0] as RichTextComponent).displayText = Lang.Get("Total Cost: {0}", auction.Price + (on ? deliveryCosts : 0));
            rtele.RecomposeText();
        }

        private bool OnCancel()
        {
            TryClose();
            return true;
        }

        private bool OnPurchase()
        {
            auctionSys.BuyAuctionClient(traderEntity, auction.AuctionId, Composers["confirmauctionpurchase"].GetSwitch("delivery").On);
            TryClose();
            return true;
        }


        public override bool CaptureAllInputs()
        {
            return IsOpened();
        }

        private void OnCreateAuctionClose()
        {
            TryClose();
        }

        public override void OnMouseMove(MouseEvent args)
        {
            base.OnMouseMove(args);

            args.Handled = dialogBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY);
        }

        public override void OnKeyDown(KeyEvent args)
        {
            base.OnKeyDown(args);

            if (focused && args.KeyCode == (int)GlKeys.Escape)
            {
                TryClose();
            }

            args.Handled = dialogBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY);
        }

        public override void OnKeyUp(KeyEvent args)
        {
            base.OnKeyUp(args);

            args.Handled = dialogBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY);
        }

        public override void OnMouseDown(MouseEvent args)
        {
            base.OnMouseDown(args);

            args.Handled = dialogBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY);
        }

        public override void OnMouseUp(MouseEvent args)
        {
            base.OnMouseUp(args);

            args.Handled = dialogBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY);
        }

    }
}
