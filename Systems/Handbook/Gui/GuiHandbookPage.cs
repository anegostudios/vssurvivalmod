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
    public struct WeightedHandbookPage
    {
        public float Weight;
        public GuiHandbookPage Page;
    }

    public abstract class GuiHandbookPage : IFlatListItem
    {
        public abstract string PageCode { get; }

        public abstract string CategoryCode { get; }

        public abstract void RenderTo(ICoreClientAPI capi, double x, double y);
        public abstract void Dispose();
        public bool Visible { get; set; } = true;

        public abstract RichTextComponentBase[] GetPageText(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor);
        public abstract float TextMatchWeight(string text);
        public abstract bool IsDuplicate { get; }
    }
}
