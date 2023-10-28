using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ToolTextures
    {
        public Dictionary<string, int> TextureSubIdsByCode = new Dictionary<string, int>();
    }

    public class BlockToolRack : Block
    {
        private static bool collectedToolTextures;

        public static Dictionary<Item, ToolTextures> ToolTextureSubIds(ICoreAPI api)
        {
            Dictionary<Item, ToolTextures> toolTextureSubIds;
            object obj;

            if (api.ObjectCache.TryGetValue("toolTextureSubIds", out obj)) {

                toolTextureSubIds = obj as Dictionary<Item, ToolTextures>;
            } else
            {
                api.ObjectCache["toolTextureSubIds"] = toolTextureSubIds = new Dictionary<Item, ToolTextures>();
            }

            return toolTextureSubIds;
        }


        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            collectedToolTextures = false;

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;


            interactions = ObjectCacheUtil.GetOrCreate(api, "toolrackBlockInteractions", () =>
            {
                List<ItemStack> rackableStacklist = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Tool == null && obj.Attributes?["rackable"].AsBool() != true) continue;
                    
                    List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                    if (stacks != null) rackableStacklist.AddRange(stacks);
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolrack-place",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = rackableStacklist.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolrack-take",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                    }
                };
            });
        }



        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityToolrack)
            {
                BlockEntityToolrack rack = (BlockEntityToolrack)be;
                return rack.OnPlayerInteract(byPlayer, blockSel.HitPosition);
            }

            return false;
        }

        // We need the tool item textures also in the block atlas
        public override void OnCollectTextures(ICoreAPI api, ITextureLocationDictionary textureDict)
        {
            base.OnCollectTextures(api, textureDict);

            if (collectedToolTextures) return;   // The tool texture gathering is called once per client game session, reset in the first OnLoaded() call for this block type
            collectedToolTextures = true;

            var toolTexturesDict = ToolTextureSubIds(api);
            toolTexturesDict.Clear();
            IList<Item> Items = api.World.Items;
            for (int i = 0; i < Items.Count; i++)
            {
                Item item = Items[i];   // item is never null, client side
                if (item.Tool == null && item.Attributes?["rackable"].AsBool() != true) continue;

                ToolTextures tt = new ToolTextures();

                if (item.Shape != null)
                {
                    Shape shape = (api as ICoreClientAPI).TesselatorManager.GetCachedShape(item.Shape.Base);
                    // Have to add the item textures to the block textureatlas as well, because when tesselating blocks we only look at the block textureatlas!
                    if (shape != null)
                    {
                        foreach (var val in shape.Textures)
                        {
                            CompositeTexture ctex = new CompositeTexture(val.Value.Clone());
                            ctex.Bake(api.Assets);

                            textureDict.GetOrAddTextureLocation(new AssetLocationAndSource(ctex.Baked.BakedName, "Shape code ", item.Shape.Base));
                            tt.TextureSubIdsByCode[val.Key] = textureDict[new AssetLocationAndSource(ctex.Baked.BakedName)];
                        }
                    }
                }

                foreach (var val in item.Textures)
                {
                    val.Value.Bake(api.Assets);
                    textureDict.GetOrAddTextureLocation(new AssetLocationAndSource(val.Value.Baked.BakedName, "Item code ", item.Code));
                    tt.TextureSubIdsByCode[val.Key] = textureDict[new AssetLocationAndSource(val.Value.Baked.BakedName)];
                }



                toolTexturesDict[item] = tt;
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
