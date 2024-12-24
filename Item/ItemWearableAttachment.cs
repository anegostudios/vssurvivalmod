using System.Collections.Generic;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemWearableAttachment : Item, IContainedMeshSource, ITexPositionSource
    {
        bool attachableToEntity;

        public override void OnLoaded(ICoreAPI api)
        {
            attachableToEntity = IAttachableToEntity.FromCollectible(this) != null;
            base.OnLoaded(api);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            var meshRefs = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, "wearableAttachmentMeshRefs");
            if (meshRefs?.Count > 0)
            {
                foreach (var (_, meshRef) in meshRefs)
                {
                    meshRef.Dispose();
                }
                ObjectCacheUtil.Delete(api, "wearableAttachmentMeshRefs");
            }
            base.OnUnloaded(api);
        }

        #region For ground storable mesh
        ITextureAtlasAPI curAtlas;
        Shape nowTesselatingShape;

        public Size2i AtlasSize => curAtlas.Size;

        public virtual TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath = null;
                CompositeTexture tex;

                // Prio 1: Get from collectible textures
                if (Textures.TryGetValue(textureCode, out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 2: Get from collectible textures, use "all" code
                if (texturePath == null && Textures.TryGetValue("all", out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 3: Get from currently tesselating shape
                if (texturePath == null)
                {
                    nowTesselatingShape?.Textures.TryGetValue(textureCode, out texturePath);
                }

                // Prio 4: The code is the path
                if (texturePath == null)
                {
                    texturePath = new AssetLocation(textureCode);
                }

                return getOrCreateTexPos(texturePath);
            }
        }


        protected TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            var capi = api as ICoreClientAPI;

            curAtlas.GetOrInsertTexture(texturePath, out _, out var texpos, () =>
            {
                IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (texAsset != null)
                {
                    return texAsset.ToBitmap(capi);
                }

                capi.World.Logger.Warning("Item {0} defined texture {1}, not no such texture found.", Code, texturePath);
                return null;
            }, 0.1f);

            return texpos;
        }

        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            var capi = api as ICoreClientAPI;
            if (targetAtlas == capi.ItemTextureAtlas)
            {
                ITexPositionSource texSource = capi.Tesselator.GetTextureSource(itemstack.Item);
                return genMesh(capi, itemstack, texSource);
            }


            curAtlas = targetAtlas;
            MeshData mesh = genMesh(api as ICoreClientAPI, itemstack, this);
            mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.OpaqueNoCull);
            return mesh;
        }

        public virtual string GetMeshCacheKey(ItemStack itemstack)
        {
            return "wearableAttachmentModelRef-" + itemstack.Collectible.Code.ToString();
        }

        #endregion


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (!attachableToEntity) return;

            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.GetOrCreate(capi, "wearableAttachmentMeshRefs", () => new Dictionary<string, MultiTextureMeshRef>());
            string key = GetMeshCacheKey(itemstack);

            if (!meshrefs.TryGetValue(key, out renderinfo.ModelRef))
            {
                ITexPositionSource texSource = capi.Tesselator.GetTextureSource(itemstack.Item);
                var mesh = genMesh(capi, itemstack, texSource);
                renderinfo.ModelRef = meshrefs[key] = mesh == null ? renderinfo.ModelRef : capi.Render.UploadMultiTextureMesh(mesh);
            }

            if (Attributes["visibleDamageEffect"].AsBool())
            {
                renderinfo.DamageEffect = Math.Max(0, 1 - (float)GetRemainingDurability(itemstack) / GetMaxDurability(itemstack) * 1.1f);
            }
        }

        protected MeshData genMesh(ICoreClientAPI capi, ItemStack itemstack, ITexPositionSource texSource)
        {
            JsonObject attrObj = itemstack.Collectible.Attributes;
            EntityProperties props = capi.World.GetEntityType(new AssetLocation(attrObj?["wearerEntityCode"].ToString() ?? "player"));
            Shape entityShape = props.Client.LoadedShape;
            AssetLocation shapePathForLogging = props.Client.Shape.Base;
            Shape newShape;


            if (!attachableToEntity)
            {
                // No need to step parent anything if its just a texture on the seraph
                newShape = entityShape;
            }
            else
            {
                newShape = new Shape()
                {
                    Elements = entityShape.CloneElements(),
                    Animations = entityShape.CloneAnimations(),
                    AnimationsByCrc32 = entityShape.AnimationsByCrc32,
                    JointsById = entityShape.JointsById,
                    TextureWidth = entityShape.TextureWidth,
                    TextureHeight = entityShape.TextureHeight,
                    Textures = null,
                };
            }

            MeshData meshdata;
            if (attrObj["wearableInvShape"].Exists)
            {
                var shapePath = new AssetLocation("shapes/" + attrObj["wearableInvShape"] + ".json");
                var shape = API.Common.Shape.TryGet(capi, shapePath);
                capi.Tesselator.TesselateShape(itemstack.Collectible, shape, out meshdata);
            }
            else
            {
                CompositeShape compArmorShape = !attrObj["attachShape"].Exists ? (itemstack.Class == EnumItemClass.Item ? itemstack.Item.Shape : itemstack.Block.Shape) : attrObj["attachShape"].AsObject<CompositeShape>(null, itemstack.Collectible.Code.Domain);

                if (compArmorShape == null)
                {
                    capi.World.Logger.Warning("Wearable shape {0} {1} does not define a shape through either the shape property or the attachShape Attribute. Item will be invisible.", itemstack.Class, itemstack.Collectible.Code);
                    return null;
                }

                AssetLocation shapePath = compArmorShape.Base.CopyWithPath("shapes/" + compArmorShape.Base.Path + ".json");

                Shape armorShape = API.Common.Shape.TryGet(capi, shapePath);
                if (armorShape == null)
                {
                    capi.World.Logger.Warning("Wearable shape {0} defined in {1} {2} not found or errored, was supposed to be at {3}. Item will be invisible.", compArmorShape.Base, itemstack.Class, itemstack.Collectible.Code, shapePath);
                    return null;
                }

                newShape.StepParentShape(armorShape, shapePath.ToShortString(), shapePathForLogging.ToShortString(), capi.Logger, (key, code) => { });

                if (compArmorShape.Overlays != null)
                {
                    foreach (var overlay in compArmorShape.Overlays)
                    {
                        Shape oshape = API.Common.Shape.TryGet(capi, overlay.Base.CopyWithPath("shapes/" + overlay.Base.Path + ".json"));
                        if (oshape == null)
                        {
                            capi.World.Logger.Warning("Wearable shape {0} overlay {4} defined in {1} {2} not found or errored, was supposed to be at {3}. Item will be invisible.", compArmorShape.Base, itemstack.Class, itemstack.Collectible.Code, shapePath, overlay.Base);
                            continue;
                        }

                        newShape.StepParentShape(oshape, overlay.Base.ToShortString(), shapePathForLogging.ToShortString(), capi.Logger, (key,Code) => { });
                    }
                }

                nowTesselatingShape = newShape;
                capi.Tesselator.TesselateShapeWithJointIds("entity", newShape, out meshdata, texSource, new Vec3f());
                nowTesselatingShape = null;
            }

            return meshdata;
        }
    }
}
