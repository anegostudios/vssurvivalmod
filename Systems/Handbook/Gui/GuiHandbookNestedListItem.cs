using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class GuiHandbookNestedListItem : INestedListItemWithChildren
    {

        public bool Visible => true;
        public List<IFlatListItem> Children { get; set; } = [];
        public List<IFlatListItem> FilteredChildren { get; set; } = [];
        public bool IsExpanded { get => setExpanded ?? IsFiltered; set => setExpanded = value; }
        bool? setExpanded;
        public bool IsFiltered { get; set; }
        public LoadedTexture? TitleTexture;
        public LoadedTexture? TotalTexture;
        public LoadedTexture? ExpandedIconTexture;
        public LoadedTexture? FlattenedIconTexture;
        public string Title => Lang.Get(titleCode);

        private string titleCode;

        ElementBounds scissorBounds;

        int getTotalChildren(List<IFlatListItem> elements)
        {
            int total = 0;
            foreach (var element in elements)
            {
                if (element is INestedListItemWithChildren listItem)
                {
                    total += getTotalChildren(listItem.FilteredChildren);
                    continue;
                }

                total++;
            }

            return total;
        }

        public GuiHandbookNestedListItem(string listGroupCode)
        {
            titleCode = "handbook-listitem-" + listGroupCode;
        }

        public void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            float size = (float)GuiElement.scaled(25);

            if (TitleTexture == null || ExpandedIconTexture == null || FlattenedIconTexture == null)
            {
                Recompose(capi);
            }

            if (TotalTexture == null) RecomposeTotal(capi);

            renderIcon(capi, x, y, size);

            capi.Render.Render2DTexturePremultipliedAlpha(
                TitleTexture.TextureId,
                (x + size + GuiElement.scaled(25)),
                y + size / 2 - TitleTexture.Height / 2,
                TitleTexture.Width,
                TitleTexture.Height,
                50
            );

            capi.Render.Render2DTexturePremultipliedAlpha(
                TotalTexture.TextureId,
                x + cellWidth - TotalTexture.Width - GuiElement.scaled(3),
                y + size / 2 - TotalTexture.Height / 2,
                TotalTexture.Width,
                TotalTexture.Height,
                50
            );
        }

        void renderIcon(ICoreClientAPI capi, double x, double y, float size)
        {
            var previewStacks = getPreviewStacks();
            if (previewStacks != null)
            {
                float pad = (float)GuiElement.scaled(10);
                scissorBounds.fixedX = (pad + x - size / 2) / RuntimeEnv.GUIScale;
                scissorBounds.fixedY = (y - size / 2) / RuntimeEnv.GUIScale;
                scissorBounds.CalcWorldBounds();

                if (scissorBounds.InnerWidth > 0 && scissorBounds.InnerHeight > 0)
                {
                    capi.Render.PushScissor(scissorBounds, true);
                    capi.Render.RenderItemstackToGui(previewStacks[0], x + pad + size / 2, y + size / 2, 100, size, ColorUtil.WhiteArgb, true, false, false);
                    capi.Render.PopScissor();
                }
            }

            var iconTexture = IsExpanded ? ExpandedIconTexture : FlattenedIconTexture;
            capi.Render.Render2DTexturePremultipliedAlpha(
                iconTexture.TextureId,
                (x + size / 2) + size - iconTexture.Width / 2f,
                y + size - iconTexture.Height / 2f,
                iconTexture.Width,
                iconTexture.Height,
                500
            );
        }

        ItemSlot[]? getPreviewStacks()
        {
            ItemSlot[] slots = new ItemSlot[3];

            int i = 0;
            recursiveItemStackSearch(ref slots, ref i, FilteredChildren);

            return i == 0 ? null : slots;
        }

        void recursiveItemStackSearch(ref ItemSlot[] slots, ref int index, List<IFlatListItem> elements)
        {
            foreach (var item in elements)
            {
                if (item is INestedListItemWithChildren listItem)
                {
                    recursiveItemStackSearch(ref slots, ref index, listItem.FilteredChildren);
                    if (index >= slots.Length) break;
                }
                if (item is not GuiHandbookItemStackPage page) continue;
                if (page.dummySlot?.Itemstack == null) continue;

                slots[index] = page.dummySlot;
                index++;
                if (index == slots.Length) break;
            }
        }

        public void Recompose(ICoreClientAPI capi)
        {
            TitleTexture?.Dispose();
            ExpandedIconTexture?.Dispose();
            FlattenedIconTexture?.Dispose();
            var titleFont = CairoFont.WhiteSmallText();
            titleFont.FontWeight = FontWeight.Bold;
            titleFont.UnscaledFontsize = 14;
            titleFont.Color = [183 / 255d, 150 / 255d, 117 / 255d, 1];
            TitleTexture = new TextTextureUtil(capi).GenTextTexture(Title.ToUpper(), titleFont);

            double size = GuiElement.scaled(15);
            ImageSurface surface = new ImageSurface(Format.Argb32, (int)Math.Ceiling(size), (int)Math.Ceiling(size));
            Context ctx = GuiElement.GenContext(surface);

            double height = surface.Height;
            double width = surface.Width;
            ctx.MoveTo(0, height / 2 - height / 4);
            ctx.LineTo(width, height / 2 - height / 4);
            ctx.LineTo(width / 2, height - height / 4);
            ctx.LineTo(0, height / 2 - height / 4);
            ctx.ClosePath();
            ctx.SetSourceRGBA(GuiStyle.DialogDefaultTextColor);
            ctx.FillPreserve();
            ctx.SetSourceRGBA(0, 0, 0, 1);
            ctx.LineWidth = GuiElement.scaled(1.25);
            ctx.Stroke();

            ExpandedIconTexture = new LoadedTexture(capi);
            capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref ExpandedIconTexture);

            ctx.Dispose();
            surface.Dispose();

            surface = new ImageSurface(Format.Argb32, (int)Math.Ceiling(size), (int)Math.Ceiling(size));
            ctx = GuiElement.GenContext(surface);

            height = surface.Height;
            width = surface.Width;
            ctx.MoveTo(width / 2 - width / 4, 0);
            ctx.LineTo(width - width / 4, height / 2f);
            ctx.LineTo(width / 2 - width / 4, height);
            ctx.LineTo(width / 2 - width / 4, 0);
            ctx.ClosePath();
            ctx.SetSourceRGBA(GuiStyle.DialogDefaultTextColor);
            ctx.FillPreserve();
            ctx.SetSourceRGBA(0, 0, 0, 1);
            ctx.LineWidth = GuiElement.scaled(1.25);
            ctx.Stroke();

            FlattenedIconTexture = new LoadedTexture(capi);
            capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref FlattenedIconTexture);

            ctx.Dispose();
            surface.Dispose();

            scissorBounds = ElementBounds.FixedSize(50, 50);
            scissorBounds.ParentBounds = capi.Gui.WindowBounds;
        }

        public void RecomposeTotal(ICoreClientAPI capi)
        {
            TotalTexture?.Dispose();
            var titleFont = CairoFont.WhiteSmallText();
            titleFont.FontWeight = FontWeight.Bold;
            titleFont.UnscaledFontsize = 14;
            titleFont.Color = [183 / 255d, 150 / 255d, 117 / 255d, 1];
            TotalTexture = new TextTextureUtil(capi).GenTextTexture($"({getTotalChildren(FilteredChildren)})", titleFont);
        }

        public void Dispose()
        {
            TitleTexture?.Dispose();
            TotalTexture?.Dispose();
            ExpandedIconTexture?.Dispose();
            FlattenedIconTexture?.Dispose();
            TitleTexture = null;
            TotalTexture = null;
            ExpandedIconTexture = null;
            FlattenedIconTexture = null;
            Children.ForEach(item => item.Dispose());
        }

        public void AddWeightedHandbookListItem(Regex? regex, Regex? strictRegex, ref List<WeightedHandbookListItem> weightedListItems, List<WeightedHandbookListItem> nestedWeightedListItems)
        {
            if (nestedWeightedListItems.Count == 0) return;

            TotalTexture?.Dispose();
            TotalTexture = null;

            if (nestedWeightedListItems.Count == 1 && nestedWeightedListItems[0].ListItem is not INestedListItemWithChildren)
            {
                weightedListItems.Add(nestedWeightedListItems[0]);
                return;
            }

            weightedListItems.Add(new WeightedHandbookListItem
            {
                ListItem = this,
                TitleMatches = nestedWeightedListItems.Max(item => item.TitleMatches) + GuiDialog.CountMatches(Title, regex),
                StrictTitleMatches = nestedWeightedListItems.Max(item => item.StrictTitleMatches) + GuiDialog.CountMatches(Title, strictRegex),
                TitleLength = Title.ToSearchFriendly().Length,
                TextMatches = nestedWeightedListItems.Sum(item => item.TextMatches),
                SearchWeight = nestedWeightedListItems.Average(item => item.SearchWeight)
            });
        }
    }


}
