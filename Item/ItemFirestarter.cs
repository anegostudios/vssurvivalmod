using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemFirestarter : Item
    {
        string igniteAnimation;

        public override void OnLoaded(ICoreAPI api)
        {
            igniteAnimation = Attributes["igniteAnimation"].AsString();
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            if (blockSel?.Position is not { } pos) return;
            var world = byEntity.World;

            var byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!world.Claims.TryAccess(byPlayer, pos, EnumBlockAccessFlags.Use))
            {
                return;
            }

            var ign = world.BlockAccessor.GetBlock(pos).GetInterface<IIgnitable>(world, pos);
            EnumIgniteState state = ign?.OnTryIgniteBlock(byEntity, pos, 0) ?? EnumIgniteState.NotIgnitable;
            if (state != EnumIgniteState.Ignitable)
            {
                if (state == EnumIgniteState.NotIgnitablePreventDefault) handling = EnumHandHandling.PreventDefault;
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            byEntity.AnimManager.StartAnimation(igniteAnimation);

            if (api.Side != EnumAppSide.Client) return;

            api.Event.UnregisterCallback(ObjectCacheUtil.TryGet<long>(api, "firestartersound"));
            api.ObjectCache["firestartersound"] = api.Event.RegisterCallback(_ => byEntity.World.PlaySoundAt("sounds/player/handdrill", byEntity, byPlayer, false, 16), 500);
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel?.Position is not { } pos)
            {
                api.Event.UnregisterCallback(ObjectCacheUtil.TryGet<long>(api, "firestartersound"));
                return false;
            }
            var world = byEntity.World;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!world.Claims.TryAccess(byPlayer, pos, EnumBlockAccessFlags.Use))
            {
                api.Event.UnregisterCallback(ObjectCacheUtil.TryGet<long>(api, "firestartersound"));
                return false;
            }

            var ign = world.BlockAccessor.GetBlock(pos).GetInterface<IIgnitable>(world, pos);
            EnumIgniteState igniteState = ign?.OnTryIgniteBlock(byEntity, pos, secondsUsed) ?? EnumIgniteState.NotIgnitable;

            if (igniteState is EnumIgniteState.NotIgnitable or EnumIgniteState.NotIgnitablePreventDefault)
            {
                api.Event.UnregisterCallback(ObjectCacheUtil.TryGet<long>(api, "firestartersound"));
                return false;
            }

            if (world is not IClientWorldAccessor cWorld) return igniteState is EnumIgniteState.Ignitable;

            ModelTransform tf = new ModelTransform();
            tf.EnsureDefaultValues();

            float f = GameMath.Clamp(1 - 2 * secondsUsed, 0, 1);
            Random rand = cWorld.Rand;
            tf.Translation.Set(f * f * f * 1.6f - 1.6f, 0, 0);
            tf.Rotation.Y = -Math.Min(secondsUsed * 120, 30);

            if (secondsUsed > 0.5f)
            {
                tf.Translation.Add((float)rand.NextDouble() * 0.1f, (float)rand.NextDouble() * 0.1f, (float)rand.NextDouble() * 0.1f);

                cWorld.SetCameraShake(0.04f);
            }


            if (!(secondsUsed > 0.25f) || (int)(30 * secondsUsed) % 2 != 1) return igniteState is EnumIgniteState.Ignitable;

            Block blockFire = cWorld.GetBlock(new AssetLocation("fire"));

            AdvancedParticleProperties props = blockFire.ParticleProperties[^1].Clone();
            props.basePos = pos.ToVec3d().Add(blockSel.HitPosition);
            props.Quantity.avg = 0.3f;
            props.Size.avg = 0.03f;

            cWorld.SpawnParticles(props, byPlayer);

            props.Quantity.avg = 0;

            return igniteState == EnumIgniteState.Ignitable;
        }


        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.AnimManager.StopAnimation(igniteAnimation);

            if (blockSel?.Position is not { } pos || byEntity.World is not IServerWorldAccessor world || world.Rand.NextDouble() > 0.25) return;

            var ign = world.BlockAccessor.GetBlock(pos).GetInterface<IIgnitable>(world, pos);
            EnumIgniteState igniteState = ign?.OnTryIgniteBlock(byEntity, pos, secondsUsed) ?? EnumIgniteState.NotIgnitable;

            if (igniteState != EnumIgniteState.IgniteNow)
            {
                api.Event.UnregisterCallback(ObjectCacheUtil.TryGet<long>(api, "firestartersound"));
                return;
            }

            if (!world.Claims.TryAccess((byEntity as EntityPlayer)?.Player, pos, EnumBlockAccessFlags.Use))
            {
                return;
            }

            DamageItem(world, byEntity, slot);

            EnumHandling handled = EnumHandling.PassThrough;
            ign?.OnTryIgniteBlockOver(byEntity, pos, secondsUsed, ref handled);
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.AnimManager.StopAnimation(igniteAnimation);
            api.Event.UnregisterCallback(ObjectCacheUtil.TryGet<long>(api, "firestartersound"));
            return true;
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            WorldInteraction[] interactions = new WorldInteraction[]
            {
                new WorldInteraction
                {
                    HotKeyCode = "shift",
                    ActionLangCode = "heldhelp-igniteblock",
                    MouseButton = EnumMouseButton.Right
                }
            };

            return base.GetHeldInteractionHelp(inSlot).Append(interactions);
        }

    }
}
