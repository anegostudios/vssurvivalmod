using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Cairo;
using Vintagestory.API.MathTools;


// Requirements:
// Activity Collections
// List/Add/Remove
// Modify

// Activity Collection
// List/Add/Remove collection
// Modify Activity

// Activity
// Slot field
// Priority field
// Name field
// List of Actions + Add/Remove
// List of Conditions + Add/Remove
// Visualize button (shows path like cinematic camera)

// Action
// Action specific config fields

// Condition
// Condition specific config fields

namespace Vintagestory.GameContent
{
    public class ActivityCellEntry : GuiElement, IGuiElementCell
    {
        public bool Visible => true;
        ElementBounds IGuiElementCell.Bounds => this.Bounds;
        public LoadedTexture hoverTexture;
        double unScaledCellHeight = 35;
        GuiElementRichtext nameTextElem;
        GuiElementRichtext detailTextElem;
        bool composed = false;
        public bool Selected;
        Action<int> onClick;
        float accum1Sec;
        string prevExpireText;

        public ActivityCellEntry(ICoreClientAPI capi, ElementBounds bounds, string name, string detail, Action<int> onClick) : base(capi, bounds)
        {
            this.onClick = onClick;

            CairoFont font = CairoFont.WhiteDetailText();
            double offY = (unScaledCellHeight - font.UnscaledFontsize) / 2;

            var nameTextBounds = ElementBounds.Fixed(0, offY, 200, 25).WithParent(Bounds);
            var activitiesBounds = ElementBounds.Fixed(0, offY, 300, 25).WithParent(Bounds).FixedRightOf(nameTextBounds, 10);

            nameTextElem = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, name, font), nameTextBounds);
            detailTextElem = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, detail, font), activitiesBounds);

            hoverTexture = new LoadedTexture(capi);
        }

        public void Recompose()
        {
            composed = true;

            nameTextElem.Compose();
            detailTextElem.Compose();

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

            nameTextElem.RenderInteractiveElements(deltaTime);
            detailTextElem.RenderInteractiveElements(deltaTime);

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


        public void UpdateCellHeight()
        {
            Bounds.CalcWorldBounds();
            nameTextElem.BeforeCalcBounds();
            detailTextElem.BeforeCalcBounds();
            Bounds.fixedHeight = unScaledCellHeight;
        }

        public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
        {
            int x = api.Input.MouseX;
            int y = api.Input.MouseY;
            if (!args.Handled)
            {
                onClick?.Invoke(elementIndex);
            }
        }
        public override void Dispose()
        {
            nameTextElem.Dispose();
            detailTextElem.Dispose();
            hoverTexture?.Dispose();
        }

        public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
        {
        }

        public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
        {
        }
    }




}
