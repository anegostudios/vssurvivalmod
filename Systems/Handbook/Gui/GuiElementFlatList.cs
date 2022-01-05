using System.Collections.Generic;
using Cairo;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using System;
using System.Linq;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public interface IFlatListItem
    {
        bool Visible { get; }
        void RenderTo(ICoreClientAPI capi, double x, double y);

        void Dispose();
    }


    public class GuiElementFlatList : GuiElement
    {
        public List<IFlatListItem> Elements = new List<IFlatListItem>();

        public int unscaledCellSpacing = 5;
        public int unscaledCellHeight = 40;
        public int unscalledYPad = 8;

        public Action<int> onLeftClick;

        LoadedTexture hoverOverlayTexture;
        public ElementBounds insideBounds;

        public GuiElementFlatList(ICoreClientAPI capi, ElementBounds bounds, Action<int> onLeftClick, List<IFlatListItem> elements = null) : base(capi, bounds)
        {
            hoverOverlayTexture = new LoadedTexture(capi);

            insideBounds = new ElementBounds().WithFixedPadding(unscaledCellSpacing).WithEmptyParent();
            insideBounds.CalcWorldBounds();

            this.onLeftClick = onLeftClick;
            if (elements != null)
            {
                Elements = elements;
            }

            CalcTotalHeight();
        }
        

        public void CalcTotalHeight()
        {
            double height = Elements.Where(e => e.Visible).Count() * (unscaledCellHeight + unscaledCellSpacing);
            insideBounds.fixedHeight = height + unscaledCellSpacing;
        }

        public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
        {
            insideBounds = new ElementBounds().WithFixedPadding(unscaledCellSpacing).WithEmptyParent();
            insideBounds.CalcWorldBounds();
            CalcTotalHeight();
            Bounds.CalcWorldBounds();

            ImageSurface surface = new ImageSurface(Format.Argb32, (int)Bounds.InnerWidth, (int)GuiElement.scaled(unscaledCellHeight));
            Context ctx = new Context(surface);

            ctx.SetSourceRGBA(1, 1, 1, 0.5);
            ctx.Paint();

            generateTexture(surface, ref hoverOverlayTexture);

            ctx.Dispose();
            surface.Dispose();
        }


        bool wasMouseDownOnElement = false;
        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (!Bounds.ParentBounds.PointInside(args.X, args.Y)) return;
            base.OnMouseDownOnElement(api, args);

            wasMouseDownOnElement = true;
        }


        public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (!Bounds.ParentBounds.PointInside(args.X, args.Y)) return;
            if (!wasMouseDownOnElement) return;

            wasMouseDownOnElement = false;

            int i = 0;

            int mx = api.Input.MouseX;
            int my = api.Input.MouseY;
            double posY = insideBounds.absY;


            foreach (IFlatListItem element in Elements)
            {
                if (!element.Visible)
                {
                    i++;
                    continue;
                }

                float y = (float)(5 + Bounds.absY + posY);

                double ypad = GuiElement.scaled(unscalledYPad);

                if (mx > Bounds.absX && mx <= Bounds.absX + Bounds.InnerWidth && my >= y - ypad && my <= y + scaled(unscaledCellHeight) - ypad)
                {
                    api.Gui.PlaySound("menubutton_press");
                    onLeftClick?.Invoke(i);
                    args.Handled = true;
                    return;
                }
                
                posY += scaled(unscaledCellHeight + unscaledCellSpacing);
                i++;
            }
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            int mx = api.Input.MouseX;
            int my = api.Input.MouseY;
            bool inbounds = Bounds.ParentBounds.PointInside(mx, my);

            double posY = insideBounds.absY;
            double ypad = GuiElement.scaled(unscalledYPad);

            foreach (IFlatListItem element in Elements)
            {
                if (!element.Visible) continue;

                float y = (float)(5 + Bounds.absY + posY);
                

                if (inbounds && mx > Bounds.absX && mx <= Bounds.absX + Bounds.InnerWidth && my >= y-ypad && my <= y + scaled(unscaledCellHeight)- ypad)
                {
                    api.Render.Render2DLoadedTexture(hoverOverlayTexture, (float)Bounds.absX, y - (float)ypad);
                }

                if (posY > -50 && posY < Bounds.OuterHeight + 50)
                {
                    element.RenderTo(api, Bounds.absX, y);
                }

                posY += scaled(unscaledCellHeight + unscaledCellSpacing);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            hoverOverlayTexture.Dispose();

            foreach (var val in Elements)
            {
                val.Dispose();
            }
        }

    }

    public static partial class GuiComposerHelpers
    {

        public static GuiComposer AddFlatList(this GuiComposer composer, ElementBounds bounds, Action<int> onleftClick = null, List<IFlatListItem> stacks = null, string key = null)
        {
            if (!composer.Composed)
            {
                composer.AddInteractiveElement(new GuiElementFlatList(composer.Api, bounds, onleftClick, stacks), key);
            }

            return composer;
        }

        public static GuiElementFlatList GetFlatList(this GuiComposer composer, string key)
        {
            return (GuiElementFlatList)composer.GetElement(key);
        }
    }

}
