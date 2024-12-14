using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ModSystemGliding : ModSystem
    {
        ICoreClientAPI capi;
        protected ILoadedSound glideSound;

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
            ToggleglideSounds(capi.World.Player.Entity.Controls.Gliding);

            foreach (var plr in capi.World.AllOnlinePlayers)
            {
                if (plr.Entity == null) continue;

                float speed = 15f;

                float glidingAccum = plr.Entity.Attributes.GetFloat("glidingAccum");
                int unfoldStep = plr.Entity.Attributes.GetInt("unfoldStep");

                if (plr.Entity.Controls.Gliding)
                {
                    glidingAccum = Math.Min(3.01f / speed, glidingAccum + dt);
                    if (!HasGlider)
                    {
                        plr.Entity.Controls.Gliding = false;
                        plr.Entity.WalkPitch = 0;
                    }
                }
                else
                {
                    glidingAccum = Math.Max(0, glidingAccum - dt);
                }

                int nowUnfoldStep = (int)(glidingAccum * speed);
                if (unfoldStep != nowUnfoldStep)
                {
                    unfoldStep = nowUnfoldStep;
                    plr.Entity.MarkShapeModified();
                    plr.Entity.Attributes.SetInt("unfoldStep", unfoldStep);
                }

                plr.Entity.Attributes.SetFloat("glidingAccum", glidingAccum);
            }
        }


        public void ToggleglideSounds(bool on)
        {
            if (on)
            {
                if (glideSound == null || !glideSound.IsPlaying)
                {
                    glideSound = capi.World.LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/effect/gliding.ogg"),
                        ShouldLoop = true,
                        Position = null,
                        RelativePosition=true,
                        DisposeOnFinish = false,
                        Volume = 0
                    });

                    if (glideSound != null)
                    {
                        glideSound.Start();
                        glideSound.PlaybackPosition = glideSound.SoundLengthSeconds * (float)capi.World.Rand.NextDouble();
                        glideSound.FadeIn(1, (s) => { });
                    }
                }
            }
            else
            {
                glideSound?.Stop();
                glideSound?.Dispose();
                glideSound = null;
            }
        }


        private void Input_InWorldAction(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            var eplr = capi.World.Player.Entity;
            if (action == EnumEntityAction.Jump && on && !eplr.OnGround && HasGlider && !eplr.Controls.IsFlying)
            {
                eplr.Controls.Gliding = true;
                eplr.Controls.IsFlying = true;
                eplr.MarkShapeModified();
            }

            if (action == EnumEntityAction.Glide && !on)
            {
                eplr.MarkShapeModified();
            }
        }

        bool HasGlider {  
            get
            {
                var inv = capi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
                foreach (var slot in inv)
                {
                    if (!(slot is ItemSlotBackpack)) continue; // Don't search inside backpacks
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

        bool subclassed = false;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            gliderShape_unfoldStep1 = API.Common.Shape.TryGet(api, Attributes["unfoldShapeStep1"].AsObject<CompositeShape>().Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
            gliderShape_unfoldStep2 = API.Common.Shape.TryGet(api, Attributes["unfoldShapeStep2"].AsObject<CompositeShape>().Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
            gliderShape_unfolded = API.Common.Shape.TryGet(api, Attributes["unfoldedShape"].AsObject<CompositeShape>().Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
        }
            
        public Shape GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            if (!subclassed)
            {
                gliderShape_unfolded.SubclassForStepParenting(texturePrefixCode);
                gliderShape_unfoldStep1.SubclassForStepParenting(texturePrefixCode);
                gliderShape_unfoldStep2.SubclassForStepParenting(texturePrefixCode);
                subclassed = true;
            }

            int unfoldStep = forEntity.Attributes.GetInt("unfoldStep");
            
            if (unfoldStep == 1) return gliderShape_unfoldStep1;
            if (unfoldStep == 2) return gliderShape_unfoldStep2;
            if (unfoldStep == 3) return gliderShape_unfolded;
            return null;
        }
    }
}
