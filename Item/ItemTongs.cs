using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemSubTongsDurability : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        ICoreServerAPI sapi;
        ICoreClientAPI capi;
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterEventBusListener(onCollected, 0.5f, "onitemcollected");
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterEventBusListener(OnEventBusEvent);
        }

        private void OnEventBusEvent(string eventName, ref EnumHandling handling, IAttribute data)
        {
            var tree = data as TreeAttribute;
            if ((eventName == "onsettransform" || eventName == "ongettransform") && tree.GetString("target") == "onTongTransform")
            {
                var tongStack = capi.World.Player.Entity?.LeftHandItemSlot?.Itemstack;
                string transformCode = tongStack?.ItemAttributes["transformCode"].AsString();
                var collobj = capi.World.Player.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible;

                if (!collobj.Attributes[transformCode].Exists) transformCode = "onTongTransform";

                if (eventName == "ongettransform")
                {
                    var defaultmt = EntityPlayerShapeRenderer.DefaultTongTransform;
                    var mt = collobj.Attributes?[transformCode].AsObject(defaultmt) ?? defaultmt;
                    mt.ToTreeAttribute(tree);
                    tree.SetBool("preventDefault", true);
                }
                else
                {
                    collobj.Attributes.Token[transformCode] = JToken.FromObject(ModelTransform.CreateFromTreeAttribute(tree));
                    tree.SetBool("preventDefault", true);
                }                    
            }
        }

        private void onCollected(string eventName, ref EnumHandling handling, IAttribute data)
        {
            var tree = data as TreeAttribute;
            OnItemPickedUp(sapi.World.GetEntityById(tree.GetLong("byentityid")), tree.GetItemstack("itemstack"));
        }

        public void OnItemPickedUp(Entity byEntity, ItemStack stack = null)
        {
            var eplr = byEntity as EntityPlayer;
            if (eplr == null) return;

            if (eplr.Player is not IServerPlayer srvplr) return;

            var hotbarslot = srvplr.InventoryManager.ActiveHotbarSlot;
            var offhandslot = srvplr.InventoryManager.OffhandHotbarSlot;

            bool subDurability =
               srvplr.InventoryManager.OffhandTool == EnumTool.Tongs // Must have tongs in off hand 
               && (stack == null || stack.Equals(sapi.World, hotbarslot.Itemstack, GlobalConstants.IgnoredStackAttributes)) // stack must be in active hands
               && hotbarslot.Itemstack.Collectible.GetTemperature(sapi.World, hotbarslot.Itemstack) > GlobalConstants.TooHotToTouchTemperature // stack must be too hot to touch
           ;

            if (subDurability)
            {
                offhandslot.Itemstack.Collectible.DamageItem(sapi.World, eplr, offhandslot);
            }
        }
    }

    public class ItemTongs : Item, IHeldHandAnimOverrider
    {

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target == EnumItemRenderTarget.HandTpOff && Attributes["shapeByOpening"].Exists)
            {
                var hslot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
                var opening = hslot.Empty ? null : hslot.Itemstack.Collectible.Attributes?["tongOpening"].AsString(null);

                if (!string.IsNullOrEmpty(opening)) {
                    var meshrefs = ObjectCacheUtil.GetOrCreate(capi, Code + "tongMeshrefsByOpening", () => new Dictionary<string, MultiTextureMeshRef>());
                    if (meshrefs.TryGetValue(opening, out var mtmeshref))
                    {
                        renderinfo.ModelRef = mtmeshref;
                    }
                    else
                    {
                        var cshape = Attributes["shapeByOpening"][opening]?.AsObject<CompositeShape>();
                        if (cshape != null)
                        {
                            cshape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                            var shape = capi.Assets.TryGet(cshape.Base).ToObject<Shape>();

                            capi.Tesselator.TesselateShape(this, shape, out var meshdata);
                            renderinfo.ModelRef = meshrefs[opening] = capi.Render.UploadMultiTextureMesh(meshdata);
                        }
                    }
                }
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            var dict = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, Code + "tongMeshrefsByOpening");
            if (dict != null)
            {
                foreach (var val in dict) val.Value.Dispose();
            }
            
            base.OnUnloaded(api);
        }

        public bool AllowHeldIdleHandAnim(Entity forEntity, ItemSlot slot, EnumHand hand)
        {
            return slot.BackgroundIcon != "left_hand" || !isHoldingHotItem(forEntity, out _);
        }

        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
        {
            if (isHoldingHotItem(forEntity, out string tpIdleAnim))
            {
                return tpIdleAnim;
            }

            return base.GetHeldTpIdleAnimation(activeHotbarSlot, forEntity, hand);
        }

        private static bool isHoldingHotItem(Entity forEntity, out string tpIdleAnim)
        {
            tpIdleAnim = "holdbothhands-tongs1";

            if (forEntity is EntityPlayer eplr && !eplr.RightHandItemSlot.Empty)
            {
                var stack = eplr.RightHandItemSlot.Itemstack;
                if (stack.Collectible.GetTemperature(forEntity.World, stack) > GlobalConstants.TooHotToTouchTemperature)
                {
                    var opening = stack.Collectible.Attributes["tongOpening"].AsString("1");
                    tpIdleAnim = "holdbothhands-tongs" + opening;
                    return true;
                }
            }

            return false;
        }
    }
}
