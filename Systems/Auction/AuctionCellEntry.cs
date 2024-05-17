using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AuctionCellEntry : GuiElement, IGuiElementCell
    {
        public bool Visible => true;
        ElementBounds IGuiElementCell.Bounds => this.Bounds;


        public DummySlot dummySlot;
        ElementBounds scissorBounds;
        public Auction auction;

        public LoadedTexture hoverTexture;

        float unscaledIconSize = 35;
        float iconSize;

        double unScaledCellHeight = 35;

        GuiElementRichtext stackNameTextElem;
        GuiElementRichtext priceTextElem;
        GuiElementRichtext expireTextElem;
        GuiElementRichtext sellerTextElem;

        bool composed = false;

        public bool Selected;
        Action<int> onClick;

        float accum1Sec;
        string prevExpireText;

        public AuctionCellEntry(ICoreClientAPI capi, InventoryBase inventoryAuction, ElementBounds bounds, Auction auction, Action<int> onClick) : base(capi, bounds)
        {
            iconSize = (float)scaled(unscaledIconSize);

            dummySlot = new DummySlot(auction.ItemStack, inventoryAuction);
            this.onClick = onClick;
            this.auction = auction;

            CairoFont font = CairoFont.WhiteDetailText();
            double offY = (unScaledCellHeight - font.UnscaledFontsize) / 2;

            scissorBounds = ElementBounds.FixedSize(unscaledIconSize, unscaledIconSize).WithParent(Bounds);
            var stackNameTextBounds = ElementBounds.Fixed(0, offY, 270, 25).WithParent(Bounds).FixedRightOf(scissorBounds, 10);
            var priceTextBounds = ElementBounds.Fixed(0, offY, 75, 25).WithParent(Bounds).FixedRightOf(stackNameTextBounds, 10);
            var expireTextBounds = ElementBounds.Fixed(0, 0, 160, 25).WithParent(Bounds).FixedRightOf(priceTextBounds, 10);
            var sellerTextBounds = ElementBounds.Fixed(0, offY, 110, 25).WithParent(Bounds).FixedRightOf(expireTextBounds, 10);





            stackNameTextElem = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, dummySlot.Itemstack.GetName(), font), stackNameTextBounds);

            double fl = font.UnscaledFontsize;
            ItemStack gearStack = capi.ModLoader.GetModSystem<ModSystemAuction>().SingleCurrencyStack;
            var comps = new RichTextComponentBase[] {
                new RichTextComponent(capi, "" + auction.Price, font) { PaddingRight = 10, VerticalAlign = EnumVerticalAlign.Top },
                new ItemstackTextComponent(capi, gearStack, fl * 2.5f, 0, EnumFloat.Inline) { VerticalAlign = EnumVerticalAlign.Top, offX = -scaled(fl * 0.5f), offY = -scaled(fl * 0.75f) } 
            };

            priceTextElem = new GuiElementRichtext(capi, comps, priceTextBounds);
            expireTextElem = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, prevExpireText = auction.GetExpireText(capi), font.Clone().WithFontSize(14)), expireTextBounds);
            expireTextElem.BeforeCalcBounds();
            expireTextBounds.fixedY = 5 + (25 - expireTextElem.TotalHeight / RuntimeEnv.GUIScale) / 2;


            sellerTextElem = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, auction.SellerName, font.Clone().WithOrientation(EnumTextOrientation.Right)), sellerTextBounds);

            hoverTexture = new LoadedTexture(capi);
        }

        public void Recompose()
        {
            composed = true;

            stackNameTextElem.Compose();
            priceTextElem.Compose();
            expireTextElem.Compose();
            sellerTextElem.Compose();

            ImageSurface surface = new ImageSurface(Format.Argb32, (int)2, (int)2);
            Context ctx = genContext(surface);
            ctx.NewPath();
            ctx.LineTo(0, 0);
            ctx.LineTo(2, 0);
            ctx.LineTo(2, 2);
            ctx.LineTo(0, 2);
            ctx.ClosePath();
            ctx.SetSourceRGBA(0, 0, 0, 0.15);
            ctx.Fill();
            generateTexture(surface, ref hoverTexture);
            ctx.Dispose();
            surface.Dispose();
        }

        
        public void OnRenderInteractiveElements(ICoreClientAPI api, float deltaTime)
        {
            if (!composed) Recompose();

            accum1Sec += deltaTime;
            if (accum1Sec > 1)
            {
                var expireText = auction.GetExpireText(api);
                if (expireText != prevExpireText)
                {
                    expireTextElem.Components = VtmlUtil.Richtextify(api, expireText, CairoFont.WhiteDetailText().WithFontSize(14));
                    expireTextElem.RecomposeText();
                    prevExpireText = expireText;
                }
            }

            if (scissorBounds.InnerWidth <= 0 || scissorBounds.InnerHeight <= 0) return;

            // 1. Itemstack
            api.Render.PushScissor(scissorBounds, true);
            api.Render.RenderItemstackToGui(dummySlot, scissorBounds.renderX + iconSize / 2, scissorBounds.renderY + iconSize / 2, 100, iconSize * 0.55f, ColorUtil.WhiteArgb, true, false, true);
            api.Render.PopScissor();
            api.Render.Render2DTexturePremultipliedAlpha(hoverTexture.TextureId, scissorBounds.renderX, scissorBounds.renderY, scissorBounds.OuterWidth, scissorBounds.OuterHeight);

            // 2. ItemStack name, price and expire
            stackNameTextElem.RenderInteractiveElements(deltaTime);
            priceTextElem.RenderInteractiveElements(deltaTime);
            expireTextElem.RenderInteractiveElements(deltaTime);
            MouseOverCursor = expireTextElem.MouseOverCursor;

            sellerTextElem.RenderInteractiveElements(deltaTime);
            
            

            // 5. Hover overlay
            int dx = api.Input.MouseX;
            int dy = api.Input.MouseY;
            Vec2d pos = Bounds.PositionInside(dx, dy);

            if (Selected || (pos != null && IsPositionInside(api.Input.MouseX, api.Input.MouseY)))
            {
                api.Render.Render2DTexturePremultipliedAlpha(hoverTexture.TextureId, Bounds.absX, Bounds.absY, Bounds.OuterWidth, Bounds.OuterHeight);
                if (Selected)
                {
                    api.Render.Render2DTexturePremultipliedAlpha(hoverTexture.TextureId, Bounds.absX, Bounds.absY, Bounds.OuterWidth, Bounds.OuterHeight);
                }
            }


        }

        public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
        {
            int x = api.Input.MouseX;
            int y = api.Input.MouseY;
            bool nowHover = scissorBounds.PositionInside(x, y) != null;
            if (nowHover)
            {
                api.Input.TriggerOnMouseEnterSlot(dummySlot);
            } else
            {
                api.Input.TriggerOnMouseLeaveSlot(dummySlot);
            }

            args.Handled = true;
        }

        public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
        {
            int x = api.Input.MouseX;
            int y = api.Input.MouseY;
            if (expireTextElem.Bounds.PointInside(x, y))
            {
                expireTextElem.OnMouseDownOnElement(api, args);
            }
        }

        public void UpdateCellHeight()
        {
            Bounds.CalcWorldBounds();
            scissorBounds.CalcWorldBounds();

            stackNameTextElem.BeforeCalcBounds();
            priceTextElem.BeforeCalcBounds();
            expireTextElem.BeforeCalcBounds();
            sellerTextElem.BeforeCalcBounds();

            Bounds.fixedHeight = unScaledCellHeight;
        }

        public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
        {
            int x = api.Input.MouseX;
            int y = api.Input.MouseY;
            if (expireTextElem.Bounds.PointInside(x, y))
            {
                expireTextElem.OnMouseUp(api, args);
            }

            if (!args.Handled)
            {
                onClick?.Invoke(elementIndex);
            }
        }





        public override void Dispose()
        {
            stackNameTextElem.Dispose();
            priceTextElem.Dispose();
            expireTextElem.Dispose();
        }

    }

}
