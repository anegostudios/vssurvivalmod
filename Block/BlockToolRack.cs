using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ToolTextures
    {
        public Dictionary<string, int> TextureSubIdsByCode = new Dictionary<string, int>();
    }

    public class BlockToolRack : Block
    {
        public static Dictionary<Item, ToolTextures> toolTextureSubIds = new Dictionary<Item, ToolTextures>();


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
        public override void OnCollectTextures(ICoreAPI api, OrderedDictionary<AssetLocationAndSource, bool> textureDict)
        {
            base.OnCollectTextures(api, textureDict);

            for (int i = 0; i < api.World.Items.Length; i++)
            {
                Item item = api.World.Items[i];
                if (item.Tool == null) continue;

                ToolTextures tt = new ToolTextures();

                foreach (var val in item.Textures)
                {
                    val.Value.Bake(api.Assets);
                    textureDict[new AssetLocationAndSource(val.Value.Baked.BakedName, "Item code " + item.Code)] = true;
                    tt.TextureSubIdsByCode[val.Key] = textureDict.IndexOfKey(new AssetLocationAndSource(val.Value.Baked.BakedName));
                }

                /*if (item.Shape != null)
                {
                    Shape shape = api.Assets.Get(item.Shape.Base).ToObject<Shape>();
                    foreach(var val in shape.Textures)
                    {
                        val.Value.Bake(api.Assets);
                        textureDict[val.Value.Baked.BakedName] = true;
                        tt.TextureSubIdsByCode[val.Key] = textureDict.IndexOfKey(val.Value.Baked.BakedName);
                    }
                }*/

                toolTextureSubIds[item] = tt;
            }
        }
    }
}
