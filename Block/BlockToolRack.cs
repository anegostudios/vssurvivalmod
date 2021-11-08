 using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ToolTextures
    {
        public Dictionary<string, int> TextureSubIdsByCode = new Dictionary<string, int>();
    }

    public class BlockToolRack : Block
    {
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

            for (int i = 0; i < api.World.Items.Count; i++)
            {
                Item item = api.World.Items[i];
                if (item.Tool == null && item.Attributes?["rackable"].AsBool() != true) continue;

                ToolTextures tt = new ToolTextures();


                if (item.Shape != null)
                {
                    IAsset asset = api.Assets.TryGet(item.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                    if (asset != null)
                    {
                        Shape shape = asset.ToObject<Shape>();
                        foreach (var val in shape.Textures)
                        {
                            CompositeTexture ctex = new CompositeTexture(val.Value.Clone());
                            ctex.Bake(api.Assets);

                            textureDict.AddTextureLocation(new AssetLocationAndSource(ctex.Baked.BakedName, "Shape code ", item.Shape.Base));
                            tt.TextureSubIdsByCode[val.Key] = textureDict[new AssetLocationAndSource(ctex.Baked.BakedName)];
                        }
                    }
                }

                foreach (var val in item.Textures)
                {
                    val.Value.Bake(api.Assets);
                    textureDict.AddTextureLocation(new AssetLocationAndSource(val.Value.Baked.BakedName, "Item code ", item.Code));
                    tt.TextureSubIdsByCode[val.Key] = textureDict[new AssetLocationAndSource(val.Value.Baked.BakedName)];
                }



                ToolTextureSubIds(api)[item] = tt;
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
