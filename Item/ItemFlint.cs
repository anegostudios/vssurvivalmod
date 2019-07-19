using System;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemFlint : Item
    {
        public override string GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity)
        {
            if (slot.Itemstack?.Collectible == this)
            {
                return (byEntity as EntityAgent).Controls.FloorSitting || (byEntity as EntityAgent).Controls.Sneak ? base.GetHeldTpHitAnimation(slot, byEntity) : "knapstanding";
            }

            return base.GetHeldTpHitAnimation(slot, byEntity);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);


            if (byEntity.Controls.Sneak && blockSel != null)
            {
                IWorldAccessor world = byEntity.World;
                Block knappingBlock = world.GetBlock(new AssetLocation("knappingsurface"));
                if (knappingBlock == null) return;

                Block block = world.BlockAccessor.GetBlock(blockSel.Position);
                if (!block.CanAttachBlockAt(byEntity.World.BlockAccessor, knappingBlock, blockSel.Position, BlockFacing.UP))
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        (api as ICoreClientAPI).TriggerIngameError(this, "cantplace", Lang.Get("Cannot place a knapping surface here"));
                    }
                    return;
                }

                BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);
                if (!world.BlockAccessor.GetBlock(pos).IsReplacableBy(knappingBlock)) return;

                BlockSelection placeSel = blockSel.Clone();
                placeSel.Position = pos;
                placeSel.DidOffset = true;
                string useless = "";

                if (!knappingBlock.TryPlaceBlock(world, byPlayer, slot.Itemstack, placeSel, ref useless))
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        bool selfBlocked = false;
                        bool entityBlocked = world.GetIntersectingEntities(pos, knappingBlock.GetCollisionBoxes(world.BlockAccessor, pos), e => { selfBlocked = e == byEntity; return !(e is EntityItem); }).Length != 0;

                        string err =
                            entityBlocked ?
                                (selfBlocked ? Lang.Get("Cannot place a knapping surface here, too close to you") : Lang.Get("Cannot place a knapping surface here, to close to another player or creature.")) :
                                Lang.Get("Cannot place a knapping surface here")
                        ;

                        (api as ICoreClientAPI).TriggerIngameError(this, "cantplace", err);
                        
                    }

                    return;
                }

                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);

                if (knappingBlock.Sounds != null)
                {
                    world.PlaySoundAt(knappingBlock.Sounds.Place, pos.X, pos.Y, pos.Z);
                }

                BlockEntityKnappingSurface bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityKnappingSurface;
                if (bec != null)
                {
                    bec.BaseMaterial = slot.Itemstack.Clone();
                    bec.BaseMaterial.StackSize = 1;

                    if (byEntity.World is IClientWorldAccessor)
                    {
                        bec.OpenDialog(world as IClientWorldAccessor, pos, slot.Itemstack);
                    }
                }

                slot.TakeOut(1);

                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
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

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return;
            }

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

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return;
            }

            // The server side call is made using a custom network packet
            if (byEntity.World is IClientWorldAccessor)
            {
                bea.OnUseOver(byPlayer, blockSel.SelectionBoxIndex, blockSel.Face, true);
            }
        }

        
    }
}
