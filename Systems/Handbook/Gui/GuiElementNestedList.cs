using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface INestedListItemWithChildren : IFlatListItem
    {
        public List<IFlatListItem> Children { get; set; }
        public List<IFlatListItem> FilteredChildren { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsFiltered { get; set; }
        public void AddWeightedHandbookListItem(Regex regex, Regex strictRegex, ref List<WeightedHandbookListItem> weightedListItems, List<WeightedHandbookListItem> nestedWeightedListItems);
    }


    public class GuiElementNestedList : GuiElement
    {
        public List<IFlatListItem> Elements = new List<IFlatListItem>();

        public int unscaledCellSpacing = 5;
        public int unscaledCellHeight = 40;
        public int unscalledYPad = 8;

        public Action<List<IFlatListItem>, int> onLeftClick;

        LoadedTexture hoverOverlayTexture;
        LoadedTexture nestedSegmentTexture;
        LoadedTexture nestedLineTexture;
        LoadedTexture nestedEndTexture;
        public ElementBounds insideBounds;

        public GuiElementNestedList(ICoreClientAPI capi, ElementBounds bounds, Action<List<IFlatListItem>, int> onLeftClick, List<IFlatListItem> elements = null) : base(capi, bounds)
        {
            hoverOverlayTexture = new LoadedTexture(capi);
            nestedSegmentTexture = new LoadedTexture(capi);
            nestedLineTexture = new LoadedTexture(capi);
            nestedEndTexture = new LoadedTexture(capi);

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
            int count = 0;
            countListItems(Elements, ref count);
            double height = count * (unscaledCellHeight + unscaledCellSpacing);
            insideBounds.fixedHeight = height + unscaledCellSpacing;
        }

        private void countListItems(List<IFlatListItem> elements, ref int count)
        {
            foreach (var element in elements)
            {
                if (element.Visible) count++;
                if (element is INestedListItemWithChildren nestedListItem && nestedListItem.IsExpanded)
                {
                    countListItems(nestedListItem.FilteredChildren, ref count);
                }
            }
        }

        public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
        {
            insideBounds = new ElementBounds().WithFixedPadding(unscaledCellSpacing).WithEmptyParent();
            insideBounds.CalcWorldBounds();
            CalcTotalHeight();
            Bounds.CalcWorldBounds();

            double width = scaled(25);
            double height = scaled(unscaledCellHeight + unscaledCellSpacing);
            double lineWidth = scaled(2.5);
            double y = scaled(unscaledCellSpacing + unscalledYPad);
            double[] lineColor = [183 / 255d, 150 / 255d, 117 / 255d, 1];
            ImageSurface surface = new ImageSurface(Format.Argb32, (int)width, (int)height);
            Context ctx = new Context(surface);

            ctx.MoveTo(width/2,-1);
            ctx.LineTo(width/2, height + 1);
            ctx.LineWidth = lineWidth;
            ctx.SetSourceRGBA(lineColor);
            ctx.Stroke();

            ctx.MoveTo(width/2, y);
            ctx.LineTo(width + 1, y);
            ctx.LineWidth = lineWidth;
            ctx.SetSourceRGBA(lineColor);
            ctx.Stroke();

            generateTexture(surface, ref nestedSegmentTexture);

            ctx.Dispose();
            surface.Dispose();

            surface = new ImageSurface(Format.Argb32, (int)width, (int)height);
            ctx = new Context(surface);

            ctx.MoveTo(width/2,-1);
            ctx.LineTo(width/2, height + 1);
            ctx.LineWidth = lineWidth;
            ctx.SetSourceRGBA(lineColor);
            ctx.Stroke();

            generateTexture(surface, ref nestedLineTexture);

            ctx.Dispose();
            surface.Dispose();

            surface = new ImageSurface(Format.Argb32, (int)width, (int)height);
            ctx = new Context(surface);

            ctx.MoveTo(width / 2, -5);
            ctx.LineTo(width / 2, 1.25);
            ctx.ArcNegative(width / 2 + y, 0, y, GameMath.PI, GameMath.PI * 1.5);
            ctx.LineWidth = lineWidth;
            ctx.SetSourceRGBA(lineColor);
            ctx.Stroke();

            generateTexture(surface, ref nestedEndTexture);

            ctx.Dispose();
            surface.Dispose();

            surface = new ImageSurface(Format.Argb32, (int)Bounds.InnerWidth, (int)scaled(unscaledCellHeight));
            ctx = new Context(surface);

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

            int mx = api.Input.MouseX;
            int my = api.Input.MouseY;
            double posY = insideBounds.absY;

            onMouseUpOnListItem(args, Elements, mx, my, ref posY);
        }

        private void onMouseUpOnListItem(MouseEvent args, List<IFlatListItem> elements, int mx, int my, ref double posY)
        {
            int i = 0;

            foreach (IFlatListItem element in elements)
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
                    api.World.FrameProfiler.Enter("guielement-clicked");
                    try
                    {
                        api.Gui.PlaySound("menubutton_press");
                        onLeftClick?.Invoke(elements, i);
                        args.Handled = true;
                    }
                    finally
                    {
                        api.World.FrameProfiler.Leave();
                    }
                    return;
                }

                posY += scaled(unscaledCellHeight + unscaledCellSpacing);
                i++;

                if (element is INestedListItemWithChildren nestedListItem && nestedListItem.IsExpanded)
                {
                    onMouseUpOnListItem(args, nestedListItem.FilteredChildren, mx, my, ref posY);
                    if (args.Handled) break;
                }
            }
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            if (!Bounds.ParentBounds.PointInside(args.X, args.Y)) return;

            EachVisibleElem(elem =>
            {
                if (elem is IFlatListItemInteractable iflii)
                {
                    iflii.OnMouseMove(api, args);
                }
            });
        }

        public override void OnMouseDown(ICoreClientAPI api, MouseEvent args)
        {
            if (!Bounds.ParentBounds.PointInside(args.X, args.Y)) return;

            EachVisibleElem(elem =>
            {
                if (elem is IFlatListItemInteractable iflii)
                {
                    iflii.OnMouseDown(api, args);
                }
            });

            if (!args.Handled)
            {
                base.OnMouseDown(api, args);
            }
        }

        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            if (!Bounds.ParentBounds.PointInside(args.X, args.Y)) return;

            EachVisibleElem(elem =>
            {
                if (elem is IFlatListItemInteractable iflii)
                {
                    iflii.OnMouseUp(api, args);
                }
            });

            if (!args.Handled)
            {
                base.OnMouseUp(api, args);
            }

        }

        protected void EachVisibleElem(Action<IFlatListItem> onElem)
        {
            foreach (IFlatListItem element in Elements)
            {
                if (!element.Visible)
                {
                    continue;
                }

                onElem(element);
            }
        }



        public override void RenderInteractiveElements(float deltaTime)
        {
            int mx = api.Input.MouseX;
            int my = api.Input.MouseY;
            bool inbounds = Bounds.ParentBounds.PointInside(mx, my);

            double posY = insideBounds.absY;
            double ypad = scaled(unscalledYPad);
            double height = scaled(unscaledCellHeight);

            renderListItems(deltaTime, Elements, mx, my, inbounds, 0, ref posY, ypad, height);
        }

        private void renderListItems(float deltaTime, List<IFlatListItem> elements, int mx, int my, bool inbounds, double xOffset, ref double posY, double ypad, double height, bool[] extendLine = null)
        {
            foreach (IFlatListItem element in elements)
            {
                if (!element.Visible) continue;

                float y = (float)(5 + Bounds.absY + posY);

                if (inbounds && mx > Bounds.absX && mx <= Bounds.absX + Bounds.InnerWidth && my >= y-ypad && my <= y + height - ypad)
                {
                    api.Render.Render2DLoadedTexture(hoverOverlayTexture, (float)Bounds.absX, y - (float)ypad);
                }

                if (posY > -50 && posY < Bounds.OuterHeight + 50)
                {
                    element.RenderListEntryTo(api, deltaTime, Bounds.absX + xOffset, y, Bounds.InnerWidth - xOffset, height);
                    if (xOffset > 0)
                    {
                        double padding = xOffset - scaled(25) + scaled(10);
                        y -= (float)scaled(unscaledCellSpacing);
                        var texture = elements.LastOrDefault() == element ? nestedEndTexture : nestedSegmentTexture;
                        api.Render.Render2DLoadedTexture(texture, (float)(Bounds.absX + padding), y, 55);
                        if (extendLine != null)
                        {
                            foreach (bool line in extendLine)
                            {
                                padding -= scaled(25);
                                if (!line) continue;
                                api.Render.Render2DLoadedTexture(nestedLineTexture, (float)(Bounds.absX + padding), y, 55);
                            }
                        }
                    }
                }

                posY += scaled(unscaledCellHeight + unscaledCellSpacing);

                if (element is INestedListItemWithChildren nestedListItem && nestedListItem.IsExpanded)
                {
                    renderListItems(deltaTime, nestedListItem.FilteredChildren, mx, my, inbounds, xOffset + scaled(25), ref posY, ypad, height, [elements.LastOrDefault() != element, ..extendLine ?? []]);
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            hoverOverlayTexture?.Dispose();
            nestedSegmentTexture?.Dispose();
            nestedLineTexture?.Dispose();
            nestedEndTexture?.Dispose();
            hoverOverlayTexture = null;
            nestedSegmentTexture = null;
            nestedLineTexture = null;
            nestedEndTexture = null;

            foreach (var val in Elements)
            {
                val.Dispose();
            }
        }

    }

    public static partial class GuiComposerHelpers
    {

        public static GuiComposer AddNestedList(this GuiComposer composer, ElementBounds bounds, Action<List<IFlatListItem>, int> onleftClick = null, List<IFlatListItem> stacks = null, string key = null)
        {
            if (!composer.Composed)
            {
                composer.AddInteractiveElement(new GuiElementNestedList(composer.Api, bounds, onleftClick, stacks), key);
            }

            return composer;
        }

        public static GuiElementNestedList GetNestedList(this GuiComposer composer, string key)
        {
            return (GuiElementNestedList)composer.GetElement(key);
        }
    }

}
