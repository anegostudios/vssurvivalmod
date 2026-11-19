using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;


namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorCombinedInventoryShape : CollectibleBehaviorWearableAttachment
    {
        public CollectibleBehaviorCombinedInventoryShape(CollectibleObject collObj) : base(collObj)
        {
        }


        public override TextureAtlasPosition? this[string textureCode]
        {
            get
            {
                AssetLocation? texturePath = null;

                if (texturesFromCombined.TryGetValue(textureCode, out CompositeTexture? tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                if (collObj is Item item)
                {
                    // Prio 1: Get from collectible textures
                    if (texturePath == null && item.Textures.TryGetValue(textureCode, out tex))
                    {
                        texturePath = tex.Baked.BakedName;
                    }

                    // Prio 2: Get from collectible textures, use "all" code
                    if (texturePath == null && item.Textures.TryGetValue("all", out tex))
                    {
                        texturePath = tex.Baked.BakedName;
                    }
                }

                // Prio 3: Get from currently tesselating shape
                if (texturePath == null)
                {
                    currentlyTesselatingShape?.Textures?.TryGetValue(textureCode, out texturePath);
                }

                // Prio 4: The code is the path
                if (texturePath == null)
                {
                    return (api as ICoreClientAPI)?.ItemTextureAtlas?.UnknownTexturePosition;
                    //texturePath = new AssetLocation(textureCode);
                }

                return getOrCreateTexPos(texturePath);
            }
        }

        public override Size2i? AtlasSize => (api as ICoreClientAPI)?.ItemTextureAtlas.Size;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            this.api = api;
        }


        public override string GetMeshCacheKey(ItemStack stack, string slotType)
        {
            ICombinedWearable? combinedWearable = stack.Collectible.GetCollectibleInterface<ICombinedWearable>();

            if (combinedWearable == null) return slotType + "-wearableModelRef";

            return slotType + "-wearableModelRef-" + combinedWearable.GetBitsCacheKey(stack);
        }



        protected Dictionary<string, CompositeTexture> texturesFromCombined { get; set; } = [];

        protected ICoreAPI? api { get; set; }

        protected Shape? currentlyTesselatingShape { get; set; }



        protected override MeshData? genFullBodyMesh(ICoreClientAPI capi, ItemStack carrier, ITexPositionSource textureSource)
        {
            ICombinedWearable? combinedWearable = carrier.Collectible.GetCollectibleInterface<ICombinedWearable>();

            if (combinedWearable == null) return null;

            JsonObject? carrierAttributes = carrier.Collectible.Attributes;
            EntityProperties? playerEntityProperties = capi.World.GetEntityType(new AssetLocation(carrierAttributes?["wearerEntityCode"].ToString() ?? "player"));
            Shape? entityShape = playerEntityProperties?.Client.LoadedShape;
            AssetLocation? shapePathForLogging = playerEntityProperties?.Client.Shape.Base;
            Shape newShape;

            if (entityShape == null || carrierAttributes == null)
            {
                throw new InvalidOperationException($"Failed to get entity shape in CollectibleBehaviorWearableAttachmentCombined.genFullBodyMesh");
            }

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

            MeshData meshdata;
            if (carrierAttributes["wearableInvShape"].Exists)
            {
                AssetLocation wearableShapePath = new("shapes/" + carrierAttributes["wearableInvShape"] + ".json");
                Shape shape = API.Common.Shape.TryGet(capi, wearableShapePath);
                capi.Tesselator.TesselateShape(carrier.Collectible, shape, out meshdata);
                return meshdata;
            }

            CompositeShape? compArmorShape;
            if (carrierAttributes["attachShape"].Exists)
            {
                compArmorShape = carrierAttributes["attachShape"].AsObject<CompositeShape>(null, carrier.Collectible.Code.Domain);
            }
            else
            {
                compArmorShape = carrier.Class == EnumItemClass.Item ? carrier.Item.Shape : carrier.Block.Shape;
            }

            if (compArmorShape == null)
            {
                capi.World.Logger.Warning("Wearable shape {0} {1} does not define a shape through either the shape property or the attachShape Attribute. Item will be invisible.", carrier.Class, carrier.Collectible.Code);
                return null;
            }

            AssetLocation shapePath = compArmorShape.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");

            Shape armorShape = API.Common.Shape.TryGet(capi, shapePath);
            if (armorShape == null)
            {
                capi.World.Logger.Warning("Wearable shape {0} defined in {1} {2} not found or errored, was supposed to be at {3}. Item will be invisible.", compArmorShape.Base, carrier.Class, carrier.Collectible.Code, shapePath);
                return null;
            }

            newShape.StepParentShape(armorShape, shapePath.ToShortString(), shapePathForLogging?.ToShortString(), capi.Logger, (key, code) => { });

            if (compArmorShape.Overlays != null)
            {
                foreach (CompositeShape? overlay in compArmorShape.Overlays)
                {
                    Shape oshape = API.Common.Shape.TryGet(capi, overlay.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json"));
                    if (oshape == null)
                    {
                        capi.World.Logger.Warning("Wearable shape {0} overlay {4} defined in {1} {2} not found or errored, was supposed to be at {3}. Item will be invisible.", compArmorShape.Base, carrier.Class, carrier.Collectible.Code, shapePath, overlay.Base);
                        continue;
                    }

                    newShape.StepParentShape(oshape, overlay.Base.ToShortString(), shapePathForLogging?.ToShortString(), capi.Logger, (key, Code) => { });
                }
            }

            string[] willDeleteElements = [];

            Dictionary<string, CompositeTexture> collectedTextures = [];

            string attributesHash = carrier.Attributes.GetHashCode().ToString();

            foreach (WearableBitEntry entry in combinedWearable.RetrieveBits(carrier))
            {
                IWearableBitAttachedShape? bitGearShape = entry.Stack.Collectible.GetCollectibleInterface<IWearableBitAttachedShape>();
                bitGearShape?.AddGearToShape(capi.ItemTextureAtlas, newShape, entry, shapePathForLogging, ref willDeleteElements, texturesFromCombined, additionalPrefix: attributesHash);
            }

            currentlyTesselatingShape = newShape;
            capi.Tesselator.TesselateShapeWithJointIds("entity", newShape, out meshdata, this, new Vec3f());
            currentlyTesselatingShape = null;

            return meshdata;
        }

        protected override TextureAtlasPosition? getOrCreateTexPos(AssetLocation texturePath)
        {
            if (api is not ICoreClientAPI clientApi) return null;

            clientApi.ItemTextureAtlas.GetOrInsertTexture(texturePath, out _, out TextureAtlasPosition? texpos, () => onTextureCreate(texturePath, clientApi), alphaTest: 0.1f);

            return texpos;
        }

        protected virtual BitmapRef? onTextureCreate(AssetLocation texturePath, ICoreClientAPI clientApi)
        {
            IAsset? texAsset = clientApi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));

            if (texAsset == null)
            {
                clientApi.World.Logger.Warning($"Item {collObj.Code} defined texture {texturePath}, not no such texture found.");
                return null;
            }

            return texAsset.ToBitmap(clientApi);
        }
    }
}
