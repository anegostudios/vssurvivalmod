﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class MealTextureSource : ITexPositionSource
    {
        public Block textureSourceBlock;
        public ItemStack? ForStack;
        public string[]? customTextureMapping;
        private ICoreClientAPI capi;
        ITexPositionSource blockTextureSource;


        public MealTextureSource(ICoreClientAPI capi, Block textureSourceBlock)
        {
            this.capi = capi;
            
            this.textureSourceBlock = textureSourceBlock;

            blockTextureSource = capi.Tesselator.GetTextureSource(textureSourceBlock);
        }

        // Problem description
        // ====================
        // We have:
        // - Shape with 5 (in future more) elements that have child elements
        // - Some 20-30 different kinds of food items
        // - 5-7 Textures with some 5-6 different subtextures for different food items
        // We need:
        // - Correctly select the right texture for a given food item
        // - Correctly render (or hide!) the right elements for a given food item, which is basicially the same as the first point.

        // To translate from haves to needs:
        // - By default everything is hidden
        // - One large map that connects every food item to a value tuple in the form of [textureCode of target Element, textureName]
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "rot-solids" || textureCode == "rot-spill")
                {
                    return blockTextureSource[textureCode];
                }

                if (ForStack != null)
                {
                    string itemcode = ForStack.Collectible.Code.Path;
                    JsonObject? mappingListCollection = textureSourceBlock.Attributes?["textureMapping"];

                    string[]? mapping = mappingListCollection?[itemcode]?.AsArray<string>(null);
                    if (customTextureMapping != null) mapping = customTextureMapping;

                    if (mapping != null && mapping[0] == textureCode)
                    {
                        return blockTextureSource[mapping[1]];
                    }
                }

                if (textureCode == "ceramic" || textureCode == "mat")
                {
                    return blockTextureSource["ceramic"];
                }
                return blockTextureSource["transparent"];
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}
