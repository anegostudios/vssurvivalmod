using System;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemFlint : Item
    {
        public override string GetHeldTpHitAnimation(IItemSlot slot, Entity byEntity)
        {
            /*if (slot.Itemstack?.Collectible == this)
            {
                return (byEntity as EntityAgent).Controls.FloorSitting ? /*"knapsitting" - bleh not blending correctly  base.GetHeldTpHitAnimation(slot, byEntity) : "knapstanding";
            }*/

            return base.GetHeldTpHitAnimation(slot, byEntity);
        }

        public override void OnHeldInteractStart(IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);


            if (byEntity.Controls.Sneak && blockSel != null)
            {
                IWorldAccessor world = byEntity.World;
                Block knappingBlock = world.GetBlock(new AssetLocation("knappingsurface"));
                if (knappingBlock == null) return;

                Block block = world.BlockAccessor.GetBlock(blockSel.Position);
                if (!block.CanAttachBlockAt(byEntity.World.BlockAccessor, knappingBlock, blockSel.Position, BlockFacing.UP)) return;

                BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);
                if (!world.BlockAccessor.GetBlock(pos).IsReplacableBy(knappingBlock)) return;

                BlockSelection placeSel = blockSel.Clone();
                placeSel.Position = pos;
                placeSel.DidOffset = true;
                if (!knappingBlock.TryPlaceBlock(world, byPlayer, slot.Itemstack, placeSel))
                {
                    return;
                }

                if (knappingBlock.Sounds != null)
                {
                    world.PlaySoundAt(knappingBlock.Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);
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

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, ref handling);
        }



        public override void OnHeldAttackStart(IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            if (!(byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockKnappingSurface)) return;

            BlockEntityKnappingSurface bea = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityKnappingSurface;
            if (bea == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            if (!byEntity.World.TryAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return;
            }

            bea.OnBeginUse(byPlayer, blockSel);

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsUsed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return false;
        }

        public override void OnHeldAttackStop(float secondsPassed, IItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;
            if (!(byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockKnappingSurface)) return;

            BlockEntityKnappingSurface bea = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityKnappingSurface;
            if (bea == null) return;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            int curMode = GetToolMode(slot, byPlayer, blockSel);

            if (!byEntity.World.TryAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
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
