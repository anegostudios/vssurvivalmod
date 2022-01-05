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
 
    public class GuiHandbookGroupedItemstackPage : GuiHandbookItemStackPage
    {
        public List<ItemStack> Stacks = new List<ItemStack>();
        public string Name;

        public GuiHandbookGroupedItemstackPage(ICoreClientAPI capi, ItemStack stack) : base(capi, null)
        {
        }

        public override string PageCode => Name;

        public override void RenderTo(ICoreClientAPI capi, double x, double y)
        {
            float size = (float)GuiElement.scaled(25);
            float pad = (float)GuiElement.scaled(10);

            int index = (int)((capi.ElapsedMilliseconds / 1000) % Stacks.Count);

            dummySlot.Itemstack = Stacks[index];
            capi.Render.RenderItemstackToGui(dummySlot, x + pad + size / 2, y + size / 2, 100, size, ColorUtil.WhiteArgb, true, false, false);

            if (Texture == null)
            {
                Texture = new TextTextureUtil(capi).GenTextTexture(Name, CairoFont.WhiteSmallText());
            }

            capi.Render.Render2DTexturePremultipliedAlpha(
                Texture.TextureId,
                (x + size + GuiElement.scaled(25)),
                y + size / 4 - 3,
                Texture.Width,
                Texture.Height,
                50
            );
        }

        public override RichTextComponentBase[] GetPageText(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            dummySlot.Itemstack = Stacks[0];

            return Stacks[0].Collectible.GetBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>().GetHandbookInfo(dummySlot, capi, allStacks, openDetailPageFor);
        }
    }


}
