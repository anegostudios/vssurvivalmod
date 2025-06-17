using Vintagestory.API.Client;
using Vintagestory.API.Common;
using System;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class GuiHandbookCommandPage : GuiHandbookPage
    {
        public IChatCommand Command;
        public string TextCacheTitle;
        public string TextCacheAll;
        public float searchWeightOffset;
        public LoadedTexture Texture;


        public override string PageCode => Command.FullName;
        string categoryCode;
        public override string CategoryCode => categoryCode;
        public override bool IsDuplicate => isDuplicate;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        private bool isDuplicate;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

        public GuiHandbookCommandPage(IChatCommand command, string fullname, string categoryCode,
            bool isRootAlias = false)
        {
            this.Command = command;

            TextCacheTitle = fullname;
            TextCacheAll = command.GetFullSyntaxHandbook(null, string.Empty, isRootAlias);
            this.categoryCode = categoryCode;
        }


        public void Recompose(ICoreClientAPI capi)
        {
            Texture?.Dispose();
            Texture = new TextTextureUtil(capi).GenTextTexture(TextCacheTitle, CairoFont.WhiteSmallText());
        }

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            float size = (float)GuiElement.scaled(5);

            if (Texture == null)
            {
                Recompose(capi);
            }

            capi.Render.Render2DTexturePremultipliedAlpha(
                Texture.TextureId,
                x + size, 
                y + size - GuiElement.scaled(3),
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
            return VtmlUtil.Richtextify(capi, "<font size=\"24\"><strong>" + Command.CallSyntax + "</strong></font>\n\n" + TextCacheAll, CairoFont.WhiteSmallText());
        }

        public override float GetTextMatchWeight(string searchText)
        {
            string title = TextCacheTitle;
            if (title.Equals(searchText, StringComparison.InvariantCultureIgnoreCase)) return searchWeightOffset + 3;
            if (title.StartsWith(searchText + " ", StringComparison.InvariantCultureIgnoreCase)) return searchWeightOffset + 2.75f + Math.Max(0, 15 - title.Length) / 100f;
            if (title.StartsWith(searchText, StringComparison.InvariantCultureIgnoreCase)) return searchWeightOffset + 2.5f + Math.Max(0, 15 - title.Length) / 100f;
            if (title.CaseInsensitiveContains(searchText)) return searchWeightOffset + 2;
            if (TextCacheAll.CaseInsensitiveContains(searchText)) return searchWeightOffset + 1;
            return 0;
        }
    }

}
