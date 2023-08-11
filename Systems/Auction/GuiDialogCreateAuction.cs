using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class GuiDialogCreateAuction : GuiDialog
    {
        int lastPrice = 1;
        
        ModSystemAuction auctionSys;
        EntityAgent owningEntity;

        public override double InputOrder => 0.1;
        public override double DrawOrder => 1;

        public override bool UnregisterOnClose => true;

        InventoryGeneric auctionSlotInv;

        public GuiDialogCreateAuction(ICoreClientAPI capi, EntityAgent owningEntity, InventoryGeneric auctionSlotInv) : base(capi)
        {
            this.owningEntity = owningEntity;
            this.auctionSlotInv = auctionSlotInv;

            auctionSys = capi.ModLoader.GetModSystem<ModSystemAuction>();

            Init();
        }

        public override string ToggleKeyCombinationCode => null;

        protected string gearIcon = "<itemstack type='item' code='gear-rusty' rsize='1.75' offy='2'>";

        ElementBounds dialogBounds;

        public void Init()
        {
            var slotBounds = ElementBounds.Fixed(0, 30, 50, 50);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            bgBounds.verticalSizing = ElementSizing.FitToChildren;
            bgBounds.horizontalSizing = ElementSizing.Fixed;
            bgBounds.fixedWidth = 300;

            ElementBounds leftButton = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);
            ElementBounds rightButton = ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, 0, 0, 0).WithFixedPadding(10, 1);

            ElementBounds priceLabelBounds = ElementBounds.Fixed(0, 0, 250, 25).FixedUnder(slotBounds, 20);
            ElementBounds priceBounds = ElementBounds.Fixed(0, 0, 100, 30).FixedUnder(priceLabelBounds, 0);
            ElementBounds durationLabelBounds = ElementBounds.Fixed(0, 0, 250, 25).FixedUnder(priceBounds, 20);
            ElementBounds dropDownBounds = ElementBounds.Fixed(0, 0, 150, 25).FixedUnder(durationLabelBounds, 0);

            ElementBounds costLabelBounds = ElementBounds.Fixed(0, 0, 300, 25).FixedUnder(dropDownBounds, 20);
            ElementBounds cutLabelBounds = ElementBounds.Fixed(0, 0, 300, 25).FixedUnder(costLabelBounds, 0);

            int[] weeks = new int[] {1,2,3,4,5 };
            string[] codes = new string[5];
            string[] values = new string[5];
            for (int i = 0; i < weeks.Length; i++)
            {
                weeks[i] *= auctionSys.DurationWeeksMul;
                codes[i] = "" + weeks[i];
                values[i] = weeks[i] == 1 ? Lang.Get("{0} week", weeks[i]) : Lang.Get("{0} weeks", weeks[i]);
            }

            Composers["tradercreateauction"] = capi.Gui
                .CreateCompo("tradercreateauction-" + owningEntity.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("Create Auction"), OnCreateAuctionClose)
                .BeginChildElements(bgBounds)
                        .AddItemSlotGrid(auctionSlotInv, (p) => { 
                            capi.Network.SendPacketClient(p); 
                        }, 1, null, slotBounds, "traderSellingSlots")

                        .AddStaticText(Lang.Get("Price in rusty gears"), CairoFont.WhiteSmallText(), priceLabelBounds)
                        .AddNumberInput(priceBounds, onPriceChanged, CairoFont.WhiteSmallText(), "price")

                        .AddStaticText(Lang.Get("Duration"), CairoFont.WhiteSmallText(), durationLabelBounds)
                        .AddDropDown(codes, values, 0, onDurationChanged, dropDownBounds, CairoFont.WhiteSmallText(), "duration")

                        .AddRichtext(Lang.Get("Deposit: {0}", 1) + " " + gearIcon, CairoFont.WhiteSmallText(), costLabelBounds, "depositText")
                        .AddRichtext(Lang.Get("Trader cut on sale (10%): {0}" + " " + gearIcon, 1), CairoFont.WhiteSmallText(), cutLabelBounds, "cutText")

                        .AddSmallButton(Lang.Get("Cancel"), OnCancelAuctionClose, leftButton.FixedUnder(cutLabelBounds, 20).WithFixedPadding(8, 5))
                        .AddSmallButton(Lang.Get("Create Auction"), OnCreateAuctionConfirm, rightButton.FixedUnder(cutLabelBounds, 20).WithFixedPadding(8, 5), EnumButtonStyle.Normal, "buysellButton")
                .EndChildElements()
                .Compose()
            ;

            Composers["tradercreateauction"].GetNumberInput("price").SetValue(lastPrice);
        }

        private void onPriceChanged(string text)
        {
            int cost = Composers["tradercreateauction"].GetNumberInput("price").GetText().ToInt(1);
            float gearcut = cost * auctionSys.SalesCutRate + auctionSys.debtClient;
            Composers["tradercreateauction"].GetRichtext("cutText").SetNewText(Lang.Get("Trader cut on sale (10%): {0}" + " " + gearIcon, (int)gearcut), CairoFont.WhiteSmallText());
        }

        private void onDurationChanged(string code, bool selected)
        {
            int cost = code.ToInt(1) / auctionSys.DurationWeeksMul;
            Composers["tradercreateauction"].GetRichtext("depositText").SetNewText(Lang.Get("Deposit: {0}x", cost) + " " + gearIcon, CairoFont.WhiteSmallText());
        }

        private bool OnCancelAuctionClose()
        {
            TryClose();
            return true;
        }

        private bool OnCreateAuctionConfirm()
        {
            var cmp = Composers["tradercreateauction"]; 
            int monehs = InventoryTrader.GetPlayerAssets(capi.World.Player.Entity);
            int weeks = cmp.GetDropDown("duration").SelectedValue.ToInt(1);
            int price = (int)cmp.GetNumberInput("price").GetValue();

            if (price < 1)
            {
                capi.TriggerIngameError(this, "atleast1gear", Lang.Get("Must sell item for at least 1 gear"));
                return true;
            }

            if (monehs < auctionSys.GetDepositCost(auctionSlotInv[0]) * weeks / auctionSys.DurationWeeksMul)
            {
                capi.TriggerIngameError(this, "notenoughgears", Lang.Get("Not enough gears to pay the deposit"));
                return true;
            }
            
            auctionSys.PlaceAuctionClient(owningEntity, price, weeks);
            OnCreateAuctionClose();

            lastPrice = price;

            auctionSlotInv[0].Itemstack = null;

            capi.Gui.PlaySound(new AssetLocation("effect/receptionbell.ogg"));


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


        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
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
