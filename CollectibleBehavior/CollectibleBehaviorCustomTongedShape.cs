using System.Collections;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class CollectibleBehaviorCustomTongedShape : CollectibleBehavior
    {
        CompositeShape tongedShape;

        public CollectibleBehaviorCustomTongedShape(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            tongedShape = properties["shape"].AsObject<CompositeShape>(null, collObj.Code.Domain);

            base.Initialize(properties);
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target == EnumItemRenderTarget.HandTpOff && tongedShape != null && itemstack.Collectible.GetTemperature(capi.World, itemstack) > GlobalConstants.TooHotToTouchTemperature)
            {
                var hslot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
                var key = collObj.Code.ToShortString();

                var meshrefs = ObjectCacheUtil.GetOrCreate(capi, "tongedMeshrefsByOpening", () => new Dictionary<string, MultiTextureMeshRef>());
                if (meshrefs.TryGetValue(key, out var mtmeshref))
                {
                    renderinfo.ModelRef = mtmeshref;
                }
                else
                {
                    tongedShape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                    var shape = capi.Assets.TryGet(tongedShape.Base).ToObject<Shape>();
                    capi.Tesselator.TesselateShape(collObj, shape, out var meshdata);
                    renderinfo.ModelRef = meshrefs[key] = capi.Render.UploadMultiTextureMesh(meshdata);
                }
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            var dict = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, "tongedMeshrefsByOpening");
            if (dict != null)
            {
                foreach (var val in dict) val.Value.Dispose();
            }

            base.OnUnloaded(api);
        }
    }
}
