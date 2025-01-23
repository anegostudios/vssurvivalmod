using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using System;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{

    public class GuiHandbookItemStackPage : GuiHandbookPage
    {
        public ItemStack Stack;
        public LoadedTexture Texture;
        public string TextCacheTitle;
        public string TextCacheAll;
        public float searchWeightOffset;

        

        public override string PageCode => PageCodeForStack(Stack);

        public InventoryBase unspoilableInventory;
        public DummySlot dummySlot;

        ElementBounds scissorBounds;

        public override string CategoryCode => "stack";
        public override bool IsDuplicate => isDuplicate;
        private bool isDuplicate;

        public GuiHandbookItemStackPage(ICoreClientAPI capi, ItemStack stack)
        {
            this.Stack = stack;
            unspoilableInventory = new CreativeInventoryTab(1, "not-used", null);
            dummySlot = new DummySlot(stack, unspoilableInventory);

            TextCacheTitle = stack.GetName().ToSearchFriendly();
            TextCacheAll = stack.GetName() + " " + stack.GetDescription(capi.World, dummySlot, false);
            isDuplicate = stack.Collectible.Attributes?["handbook"]?["isDuplicate"].AsBool(false) == true;

            searchWeightOffset = stack.Collectible.Attributes?["handbook"]?["searchWeightOffset"].AsFloat(0) ?? 0;
        }

        public static string PageCodeForStack(ItemStack stack)
        {
            if (stack.Attributes != null && stack.Attributes.Count > 0)
            {
                ITreeAttribute tree = stack.Attributes.Clone();
                foreach (var val in GlobalConstants.IgnoredStackAttributes) tree.RemoveAttribute(val);
                tree.RemoveAttribute("durability");

                var sortedtree = tree.SortedCopy(true);
                if (tree.Count != 0)
                {
                    string treeStr = TreeAttribute.ToJsonToken(sortedtree);
                    return (stack.Class.Name()) + "-" + stack.Collectible.Code.ToShortString() + "-" + treeStr;
                }
            }

            return (stack.Class.Name()) + "-" + stack.Collectible.Code.ToShortString();
        }

        public void Recompose(ICoreClientAPI capi)
        {
            Texture?.Dispose();
            Texture = new TextTextureUtil(capi).GenTextTexture(Stack.GetName(), CairoFont.WhiteSmallText());

            scissorBounds = ElementBounds.FixedSize(50, 50);
            scissorBounds.ParentBounds = capi.Gui.WindowBounds;
        }

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            float size = (float)GuiElement.scaled(25);
            float pad = (float)GuiElement.scaled(10);

            if (Texture == null)
            {
                Recompose(capi);
            }

            scissorBounds.fixedX = (pad + x - size / 2) / RuntimeEnv.GUIScale;
            scissorBounds.fixedY = (y - size / 2) / RuntimeEnv.GUIScale;
            scissorBounds.CalcWorldBounds();

            if (scissorBounds.InnerWidth <= 0 || scissorBounds.InnerHeight <= 0) return;

            capi.Render.PushScissor(scissorBounds, true);
            capi.Render.RenderItemstackToGui(dummySlot, x + pad + size/2 , y + size / 2, 100, size, ColorUtil.WhiteArgb, true, false, false);
            capi.Render.PopScissor();

            capi.Render.Render2DTexturePremultipliedAlpha(
                Texture.TextureId,
                (x + size + GuiElement.scaled(25)), 
                y + size / 4 - GuiElement.scaled(3),
                Texture.Width,
                Texture.Height,
                50
            );
        }

        public override void Dispose() {
            Texture?.Dispose();
            Texture = null;
        }

        public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
        {
            RichTextComponentBase[] cmps = GetPageText(detailViewGui.Api, allstacks, openDetailPageFor);
            detailViewGui.AddRichtext(cmps, textBounds, "richtext");
        }

        protected virtual RichTextComponentBase[] GetPageText(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            return Stack.Collectible.GetBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>()?.GetHandbookInfo(dummySlot, capi, allStacks, openDetailPageFor) ?? new RichTextComponentBase[0];
        }

        public override PageText GetPageText()
        {
            return new PageText
            {
                Title = TextCacheTitle,
                Text = TextCacheAll,
            };
        }
    }

}
