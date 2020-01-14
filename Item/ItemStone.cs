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

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            bool knappable = itemslot.Itemstack.Collectible.Attributes != null && itemslot.Itemstack.Collectible.Attributes["knappable"].AsBool(false);
            bool haveKnappableStone = false;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (byEntity.Controls.Sneak && blockSel != null)
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
                    return;
                }

                if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                {
                    itemslot.MarkDirty();
                    return;
                }

                IWorldAccessor world = byEntity.World;
                Block knappingBlock = world.GetBlock(new AssetLocation("knappingsurface"));
                if (knappingBlock == null) return;

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

                    return;
                }

                world.BlockAccessor.SetBlock(knappingBlock.BlockId, pos);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);

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
                //itemslot.Take(1);

                handling = EnumHandHandling.PreventDefault;
                byEntity.Attributes.SetInt("aimingCancel", 1);
                return;
            }

            if (blockSel != null && byEntity?.World != null && byEntity.Controls.Sneak)
            {
                IWorldAccessor world = byEntity.World;
                Block block = world.GetBlock(CodeWithPath("loosestones-" + LastCodePart()));
                if (block == null)
                {
                    block = world.GetBlock(CodeWithPath("loosestones-" + LastCodePart(1) + "-" + LastCodePart(0)));
                }
                if (block == null) return;

                if (!world.BlockAccessor.GetBlock(blockSel.Position).SideSolid[BlockFacing.UP.Index]) return;

                BlockPos targetpos = blockSel.Position.AddCopy(blockSel.Face);
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
                    return;
                }

                world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);

                if (block.Sounds != null) world.PlaySoundAt(block.Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);

                itemslot.Itemstack.StackSize--;

                handling = EnumHandHandling.PreventDefault;
                byEntity.Attributes.SetInt("aimingCancel", 1);
                return;
            }

            if (byEntity.Controls.Sneak) return;

        
            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.StartAnimation("aim");

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return false;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Clamp(secondsUsed * 3, 0, 1.5f);

                tf.Translation.Set(offset / 4f, offset / 2f, 0);
                tf.Rotation.Set(0, 0, GameMath.Min(90, secondsUsed * 360/1.5f));

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }


            return true;
        }

        
        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.Attributes.SetInt("aimingCancel", 1);
            }

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;

            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (secondsUsed < 0.35f) return;

            float damage = 1;
            string rockType = slot.Itemstack.Collectible.FirstCodePart(1);
            
            ItemStack stack = slot.TakeOut(1);
            slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("thrownstone"));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityThrownStone)entity).FiredBy = byEntity;
            ((EntityThrownStone)entity).Damage = damage;
            ((EntityThrownStone)entity).ProjectileStack = stack;


            int? texIndex = type.Attributes?["texturealternateMapping"]?[rockType].AsInt(0);
            entity.WatchedAttributes.SetInt("textureIndex", texIndex == null ? 0 : (int)texIndex);

            float acc = (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.EyeHeight - 0.2, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * 0.5;

            entity.ServerPos.SetPos(
                byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(0, byEntity.EyeHeight - 0.2, 0)
            );

            //.Ahead(0.25, 0, byEntity.ServerPos.Yaw + GameMath.PIHALF)

            entity.ServerPos.Motion.Set(velocity);

            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;
            ((EntityThrownStone)entity).SetRotation();

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
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}