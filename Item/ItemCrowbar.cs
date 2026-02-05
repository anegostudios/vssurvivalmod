using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;


#nullable disable
namespace Vintagestory.GameContent
{
    public class ItemCrowbar : Item
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            var blockSele = (forEntity as EntityPlayer).BlockSelection;
            if (blockSele == null)
            {
                return "interactstatic";
            }
            var beh = api.World.BlockAccessor.GetBlockEntity(blockSele.Position)?.GetBehavior<BEBehaviorSupportBeam>();
            if (beh?.Beams == null || beh.Beams.Length == 0) return "interactstatic";

            return base.GetHeldTpUseAnimation(activeHotbarSlot, forEntity);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent && (byEntity as EntityPlayer).Player.WorldData.CurrentGameMode == EnumGameMode.Creative) return;

            var beh = getBeh(blockSel);
            if (beh != null)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        protected BEBehaviorSupportBeam getBeh(BlockSelection blockSel)
        {
            if (blockSel == null) return null;
            var beh = api.World.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorSupportBeam>();
            if (beh?.Beams == null || beh.Beams.Length == 0) return null;

            return beh;
        }

        protected BlockSounds getSounds(BEBehaviorSupportBeam beh)
        {
            return beh.Beams[beh.Beams.Length - 1].Block.Sounds;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            var beh = getBeh(blockSel);
            if (beh != null)
            {
                if ((byEntity as EntityPlayer).Player.WorldData.CurrentGameMode == EnumGameMode.Creative) return secondsUsed < 0.1;

                return secondsUsed < 1;
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            var beh = getBeh(blockSel);
            bool creativeMode = (byEntity as EntityPlayer).Player.WorldData.CurrentGameMode == EnumGameMode.Creative;

            if (beh != null && (secondsUsed > 0.5f || creativeMode) && beh.Beams.Length > 0)
            {
                api.World.PlaySoundAt(getSounds(beh).Break, blockSel.Position, 0.5, (byEntity as EntityPlayer)?.Player);
                beh.BreakBeam(beh.Beams.Length - 1, !creativeMode);
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }
    }
}
