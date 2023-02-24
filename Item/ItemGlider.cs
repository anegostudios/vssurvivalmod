using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ModSystemGliding : ModSystem
    {
        ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Input.InWorldAction += Input_InWorldAction;
            api.Event.RegisterGameTickListener(onClientTick, 20, 1);
        }
        private void onClientTick(float dt)
        {
            foreach (var plr in capi.World.AllOnlinePlayers)
            {
                if (plr.Entity == null) continue;

                float speed = 15f;

                float glidingAccum = plr.Entity.Attributes.GetFloat("glidingAccum");
                int unfoldStep = plr.Entity.Attributes.GetInt("unfoldStep");

                if (plr.Entity.Controls.Gliding)
                {
                    glidingAccum = Math.Min(3.01f / speed, glidingAccum + dt);
                }
                else
                {
                    glidingAccum = Math.Max(0, glidingAccum - dt);
                }

                int nowUnfoldStep = (int)(glidingAccum * speed);
                if (unfoldStep != nowUnfoldStep)
                {
                    unfoldStep = nowUnfoldStep;
                    (plr.Entity.Properties.Client.Renderer as EntityShapeRenderer)?.MarkShapeModified();
                    plr.Entity.Attributes.SetInt("unfoldStep", unfoldStep);
                }

                plr.Entity.Attributes.SetFloat("glidingAccum", glidingAccum);
            }
        }

        private void Input_InWorldAction(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            var eplr = capi.World.Player.Entity;
            if (action == EnumEntityAction.Jump && on && !eplr.OnGround && HasGilder && !eplr.Controls.IsFlying)
            {
                eplr.Controls.Gliding = true;
                eplr.Controls.IsFlying = true;
                (eplr.Properties.Client.Renderer as EntityShapeRenderer)?.MarkShapeModified();
            }

            if (action == EnumEntityAction.Glide && !on)
            {
                (eplr.Properties.Client.Renderer as EntityShapeRenderer)?.MarkShapeModified();
            }
        }

        bool HasGilder {  
            get
            {
                var inv = capi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
                foreach (var slot in inv)
                {
                    if (slot.Itemstack?.Collectible is ItemGlider)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    public class ItemGlider : Item, IWearableShapeSupplier
    {
        Shape gliderShape_unfoldStep1;
        Shape gliderShape_unfoldStep2;
        Shape gliderShape_unfolded;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client)
            {
                gliderShape_unfoldStep1 = API.Common.Shape.TryGet(api, Attributes["unfoldShapeStep1"].AsObject<CompositeShape>().Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                gliderShape_unfoldStep2 = API.Common.Shape.TryGet(api, Attributes["unfoldShapeStep2"].AsObject<CompositeShape>().Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                gliderShape_unfolded = API.Common.Shape.TryGet(api, Attributes["unfoldedShape"].AsObject<CompositeShape>().Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
            }
        }
            
        public Shape GetShape(ItemStack stack, EntityAgent forEntity)
        {
            int unfoldStep = forEntity.Attributes.GetInt("unfoldStep");

            if (unfoldStep == 1) return gliderShape_unfoldStep1;
            if (unfoldStep == 2) return gliderShape_unfoldStep2;
            if (unfoldStep == 3) return gliderShape_unfolded;
            return null;
        }
    }
}
