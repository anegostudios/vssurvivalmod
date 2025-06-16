using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using System;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class GuiHandbookTextPage : GuiHandbookPage
    {
        public string pageCode;
        public string Title;
        public string Text = "";
        public string categoryCode = "guide";

        public LoadedTexture Texture;
        public override string PageCode => pageCode;

        public override string CategoryCode => categoryCode;

        public override void Dispose() { Texture?.Dispose(); Texture = null; }

        RichTextComponentBase[] comps;

        string titleCached;
        public override bool IsDuplicate => false;

        public GuiHandbookTextPage()
        {
            
        }

        public void Init(ICoreClientAPI capi)
        {
            if (Text.Length < 255)
            {
                Text = Lang.Get(Text);
            }
            
            comps = VtmlUtil.Richtextify(capi, Text, CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2));

            titleCached = Lang.Get(Title).ToSearchFriendly();
        }

        public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
        {
            detailViewGui.AddRichtext(comps, textBounds, "richtext");
        }

        public void Recompose(ICoreClientAPI capi)
        {
            Texture?.Dispose();
            Texture = new TextTextureUtil(capi).GenTextTexture(Lang.Get(Title), CairoFont.WhiteSmallText());
        }

        public override float GetTextMatchWeight(string searchText)
        {
            if (titleCached.Equals(searchText, StringComparison.InvariantCultureIgnoreCase)) return 4;
            if (titleCached.StartsWith(searchText + " ", StringComparison.InvariantCultureIgnoreCase)) return 3.5f;
            if (titleCached.StartsWith(searchText, StringComparison.InvariantCultureIgnoreCase)) return 3f;
            if (titleCached.CaseInsensitiveContains(searchText)) return 2.75f;
            if (Text.CaseInsensitiveContains(searchText)) return 1.25f;
            return 0;
        }

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            float size = (float)GuiElement.scaled(25);
            float pad = (float)GuiElement.scaled(10);

            if (Texture == null)
            {
                Recompose(capi);
            }

            capi.Render.Render2DTexturePremultipliedAlpha(
                Texture.TextureId,
                (x + pad),
                y + size / 4 - GuiElement.scaled(3),
                Texture.Width,
                Texture.Height,
                50
            );
        }
    }
}
