using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class TapestryTextureSource : ITexPositionSource
    {
        public bool rotten;
        public string type;
        int rotVariant;
        ICoreClientAPI capi;

        public TapestryTextureSource(ICoreClientAPI capi, bool rotten, string type, int rotVariant = 0)
        {
            this.capi = capi;
            this.rotten = rotten;
            this.type = type;
            this.rotVariant = rotVariant;
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath;

                if (textureCode == "ropedcloth" || type == null || type == "") texturePath = new AssetLocation("block/cloth/ropedcloth");
                else texturePath = new AssetLocation("block/cloth/tapestry/" + type);

                AssetLocation cachedPath = texturePath.Clone();

                AssetLocation rotLoc = null;

                if (rotten)
                {
                    rotLoc = new AssetLocation("block/cloth/tapestryoverlay/rotten" + rotVariant);
                    cachedPath.Path += "++" + rotLoc.Path;
                }

                TextureAtlasPosition texpos = capi.BlockTextureAtlas[cachedPath];
                
                if (texpos == null)
                {
                    IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        BitmapRef bmp = texAsset.ToBitmap(capi);

                        if (rotten)
                        {
                            BakedBitmap bakedBmp = new BakedBitmap() { Width = bmp.Width, Height = bmp.Height };
                            bakedBmp.TexturePixels = bmp.Pixels;

                            int[] texturePixelsOverlay = capi.Assets.TryGet(rotLoc.WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi)?.Pixels;
                            if (texturePixelsOverlay == null)
                            {
                                throw new Exception("Texture file " + rotLoc + " is missing");
                            }

                            for (int p = 0; p < bakedBmp.TexturePixels.Length; p++)
                            {
                                bakedBmp.TexturePixels[p] = ColorUtil.ColorOver(texturePixelsOverlay[p], bakedBmp.TexturePixels[p]);
                            }

                            capi.BlockTextureAtlas.InsertTextureCached(cachedPath, bakedBmp, out _, out texpos);
                        }
                        else
                        {
                            capi.BlockTextureAtlas.InsertTextureCached(cachedPath, bmp, out _, out texpos);
                        }


                    }
                    else
                    {
                        capi.World.Logger.Warning("Tapestry type '{0}' defined texture '{1}', but no such texture found.", type, texturePath);
                    }
                }

                return texpos;
            }
        }
    }

    public class BlockTapestry : Block
    {
        ICoreClientAPI capi;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            capi = api as ICoreClientAPI;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            Dictionary<string, MeshRef> tapestryMeshes;
            tapestryMeshes = ObjectCacheUtil.GetOrCreate(capi, "tapestryMeshes", () => new Dictionary<string, MeshRef>());

            MeshRef meshref;

            string type = itemstack.Attributes.GetString("type", "");

            if (!tapestryMeshes.TryGetValue(type, out meshref))
            {
                MeshData mesh = genMesh(false, type, 0);
                //mesh.Rgba2 = null;
                meshref = capi.Render.UploadMesh(mesh);
                tapestryMeshes[type] = meshref;
            }

            renderinfo.ModelRef = meshref;
        }


        public MeshData genMesh(bool rotten, string type, int rotVariant)
        {
            MeshData mesh;

            TapestryTextureSource txs = new TapestryTextureSource(capi, rotten, type, rotVariant);
            Shape shape = capi.TesselatorManager.GetCachedShape(Shape.Base);
            capi.Tesselator.TesselateShape("tapestryblock", shape, out mesh, txs);

            return mesh;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            ItemStack[] stacks = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

            BlockEntityTapestry bet = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityTapestry;

            if (bet.Rotten) return new ItemStack[0];

            stacks[0].Attributes.SetString("type", bet?.Type);

            return stacks;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            return base.GetHeldItemName(itemStack);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityTapestry bet = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityTapestry;
            if (bet?.Rotten == true) return Lang.Get("Rotten Tapestry");

            return base.GetPlacedBlockName(world, pos);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string type = inSlot.Itemstack.Attributes.GetString("type", "");

            dsc.AppendLine(Lang.GetMatching("tapestry-" + type));
        }


    }
}
