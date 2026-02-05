using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemStone : Item
    {
        float damage;
    
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
    
            damage = this.Attributes["damage"].AsFloat(1);
        }
        
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
            var lookedAtBlock = blockSel == null ? null : byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (lookedAtBlock is BlockDisplayCase || lookedAtBlock is BlockSign || lookedAtBlock is BlockBloomery)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                handling = EnumHandHandling.NotHandled;
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
                return;
            }

            bool knappable = itemslot.Itemstack.Collectible.Attributes != null && itemslot.Itemstack.Collectible.Attributes["knappable"].AsBool(false);
            bool haveKnappableStone = false;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (byEntity.Controls.ShiftKey && blockSel != null)
            {
                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                haveKnappableStone = 
                    block.Code.PathStartsWith("loosestones") && 
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

                (api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                if (knappingBlock.Sounds != null)
                {
                    world.PlaySoundAt(knappingBlock.Sounds.Place, blockSel.Position, -0.5);
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
                if (block == null) return;

                BlockPos targetpos = blockSel.Position.AddCopy(blockSel.Face);
                targetpos.Y--;
                if (!world.BlockAccessor.GetMostSolidBlock(targetpos).CanAttachBlockAt(world.BlockAccessor, block, targetpos, BlockFacing.UP)) return;
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
                    return;
                }

                world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);

                if (block.Sounds != null) world.PlaySoundAt(block.Sounds.Place, blockSel.Position, -0.5);
                (api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                itemslot.Itemstack.StackSize--;

                handling = EnumHandHandling.PreventDefault;
                byEntity.Attributes.SetInt("aimingCancel", 1);
                return;
            }
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
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
