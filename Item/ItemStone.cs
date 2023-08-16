using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemStone : Item
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override string GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity)
        {
            if (slot.Itemstack?.Collectible == this)
            {
                return "knap";
            }

            return base.GetHeldTpHitAnimation(slot, byEntity);
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockDisplayCase)
            {
                handling = EnumHandHandling.NotHandled;
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            EnumHandHandling bhHandHandling = EnumHandHandling.NotHandled;
            foreach (CollectibleBehavior bh in CollectibleBehaviors)
            {
                EnumHandling hd = EnumHandling.PassThrough;
                bh.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref bhHandHandling, ref hd);

                if (hd == EnumHandling.PreventSubsequent) break;
            }
            if (bhHandHandling != EnumHandHandling.NotHandled)
            {
                handling = bhHandHandling;
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            bool knappable = itemslot.Itemstack.Collectible.Attributes != null && itemslot.Itemstack.Collectible.Attributes["knappable"].AsBool(false);
            bool haveKnappableStone = false;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (byEntity.Controls.ShiftKey && blockSel != null)
            {
                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                haveKnappableStone = 
                    block.Code.Path.StartsWith("loosestones") && 
                    block.FirstCodePart(1).Equals(itemslot.Itemstack.Collectible.FirstCodePart(1))
                ;
            }

            if (haveKnappableStone)
            {
                if (!knappable)
                {
                    if (byEntity.World.Side == EnumAppSide.Client)
                    {
                        (this.api as ICoreClientAPI).TriggerIngameError(this, "toosoft", Lang.Get("This type of stone is too soft to be used for knapping."));
                    }

                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                    return;
                }

                if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                {
                    itemslot.MarkDirty();

                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                    return;
                }

                IWorldAccessor world = byEntity.World;
                Block knappingBlock = world.GetBlock(new AssetLocation("knappingsurface"));
                if (knappingBlock == null)
                {
                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                    return;
                }

                string failCode = "";

                BlockPos pos = blockSel.Position;
                knappingBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failCode);

                if (failCode == "entityintersecting")
                {
                    bool selfBlocked = false;
                    bool entityBlocked = world.GetIntersectingEntities(pos, knappingBlock.GetCollisionBoxes(world.BlockAccessor, pos), e => { selfBlocked = e == byEntity; return !(e is EntityItem); }).Length != 0;

                    string err =
                        entityBlocked ?
                            (selfBlocked ? Lang.Get("Cannot place a knapping surface here, too close to you") : Lang.Get("Cannot place a knapping surface here, to close to another player or creature.")) :
                            Lang.Get("Cannot place a knapping surface here")
                    ;

                    (api as ICoreClientAPI).TriggerIngameError(this, "cantplace", err);

                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                    return;
                }

                world.BlockAccessor.SetBlock(knappingBlock.BlockId, pos);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);

                (api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                if (knappingBlock.Sounds != null)
                {
                    world.PlaySoundAt(knappingBlock.Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);
                }

                BlockEntityKnappingSurface bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityKnappingSurface;
                if (bec != null)
                {
                    bec.BaseMaterial = itemslot.Itemstack.Clone();
                    bec.BaseMaterial.StackSize = 1;

                    if (byEntity.World is IClientWorldAccessor)
                    {
                        bec.OpenDialog(world as IClientWorldAccessor, pos, itemslot.Itemstack);
                    }
                }

                handling = EnumHandHandling.PreventDefault;
                byEntity.Attributes.SetInt("aimingCancel", 1);
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (blockSel != null && byEntity?.World != null && byEntity.Controls.ShiftKey)
            {
                IWorldAccessor world = byEntity.World;
                Block block = world.GetBlock(CodeWithPath("loosestones-" + LastCodePart() + "-free"));
                if (block == null)
                {
                    block = world.GetBlock(CodeWithPath("loosestones-" + LastCodePart(1) + "-" + LastCodePart(0) + "-free"));
                }
                if (block == null)
                {
                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                    return;
                }
                

                BlockPos targetpos = blockSel.Position.AddCopy(blockSel.Face);
                targetpos.Y--;
                if (!world.BlockAccessor.GetMostSolidBlock(targetpos.X, targetpos.Y, targetpos.Z).CanAttachBlockAt(world.BlockAccessor, block, targetpos, BlockFacing.UP)) return;
                targetpos.Y++;

                BlockSelection placeSel = blockSel.Clone();
                placeSel.Position = targetpos;
                placeSel.DidOffset = true;
                string error = "";

                if (!block.TryPlaceBlock(world, byPlayer, itemslot.Itemstack, placeSel, ref error))
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        (api as ICoreClientAPI).TriggerIngameError(this, "cantplace", Lang.Get("placefailure-" + error));
                    }

                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                    return;

                }

                world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);

                if (block.Sounds != null) world.PlaySoundAt(block.Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);
                (api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                itemslot.Itemstack.StackSize--;

                handling = EnumHandHandling.PreventDefault;
                byEntity.Attributes.SetInt("aimingCancel", 1);

                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;

            }

            if (byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }
            

        
            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.StartAnimation("aim");

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            bool result = true;
            bool preventDefault = false;
            foreach (CollectibleBehavior behavior in CollectibleBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                bool behaviorResult = behavior.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handled);
                if (handled != EnumHandling.PassThrough)
                {
                    result &= behaviorResult;
                    preventDefault = true;
                }

                if (handled == EnumHandling.PreventSubsequent) return result;
            }
            if (preventDefault) return result;

            if (byEntity.Attributes.GetInt("aimingCancel") == 1)
            {
                return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Clamp(secondsUsed * 3, 0, 1.5f);

                tf.Translation.Set(offset / 4f, offset / 2f, 0);
                tf.Rotation.Set(0, 0, GameMath.Min(90, secondsUsed * 360/1.5f));

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        
        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.Attributes.SetInt("aimingCancel", 1);
            }

            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            bool preventDefault = false;

            foreach (CollectibleBehavior behavior in CollectibleBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handled);
                if (handled != EnumHandling.PassThrough) preventDefault = true;

                if (handled == EnumHandling.PreventSubsequent) return;
            }

            if (preventDefault) return;

            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;

            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (secondsUsed < 0.35f) return;

            float damage = 1;
            
            ItemStack stack = slot.TakeOut(1);
            slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("thrownstone-" + Variant["rock"]));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityThrownStone)entity).FiredBy = byEntity;
            ((EntityThrownStone)entity).Damage = damage;
            ((EntityThrownStone)entity).ProjectileStack = stack;


            float acc = (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * 0.5;

            entity.ServerPos.SetPos(
                byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0)
            );

            entity.ServerPos.Motion.Set(velocity);

            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;

            byEntity.World.SpawnEntity(entity);
            byEntity.StartAnimation("throw");

            //byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(2f);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            dsc.AppendLine(Lang.Get("1 blunt damage when thrown"));
        }


        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            if (!(byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockKnappingSurface)) return;

            BlockEntityKnappingSurface bea = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityKnappingSurface;
            if (bea == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            bea.OnBeginUse(byPlayer, blockSel);

            handling = EnumHandHandling.PreventDefaultAction;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return false;
        }

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;
            if (!(byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockKnappingSurface)) return;

            BlockEntityKnappingSurface bea = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityKnappingSurface;
            if (bea == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            int curMode = GetToolMode(slot, byPlayer, blockSel);


            // The server side call is made using a custom network packet
            if (byEntity.World is IClientWorldAccessor)
            {
                bea.OnUseOver(byPlayer, blockSel.SelectionBoxIndex, blockSel.Face, true);
            }
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-throw",
                    MouseButton = EnumMouseButton.Right,
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
