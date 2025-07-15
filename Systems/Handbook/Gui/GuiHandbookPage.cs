﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface IHandBookPageCodeProvider
    {
        string HandbookPageCodeForStack(IWorldAccessor world, ItemStack stack);
    }

    public struct WeightedHandbookPage
    {
        public float Weight;
        public GuiHandbookPage Page;
    }

    public abstract class GuiHandbookPage : IFlatListItem
    {
        public int PageNumber;
        public abstract string PageCode { get; }

        public abstract string CategoryCode { get; }

        /// <summary>
        /// Render the list item
        /// </summary>
        /// <param name="capi"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public abstract void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWdith, double cellHeight);
        public abstract void Dispose();
        public bool Visible { get; set; } = true;

        public abstract float GetTextMatchWeight(string text);
        public abstract bool IsDuplicate { get; }

        public abstract void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor);
    }
}
