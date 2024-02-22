using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSupportBeam : Block
    {
        ModSystemSupportBeamPlacer bp;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            bp = api.ModLoader.GetModSystem<ModSystemSupportBeamPlacer>();
            PartialSelection = true;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null) return be.GetCollisionBoxes();

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null) return be.GetCollisionBoxes();

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;
            bp.OnInteract(this, slot, byEntity, blockSel);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (bp.CancelPlace(this, byEntity))
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }


        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null)
            {
                int beamIndex = (api as ICoreClientAPI)?.World.Player?.CurrentBlockSelection?.SelectionBoxIndex ?? 0;
                if (beamIndex >= be.Beams.Length)
                {
                    return;
                }
                blockModelData = be.genMesh(beamIndex, null, null);
                decalModelData = be.genMesh(beamIndex, decalTexSource, "decal");
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            int? beamIndex = byPlayer?.CurrentBlockSelection?.SelectionBoxIndex;
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();

            if (beamIndex != null && be != null && be.Beams.Length > 1)
            {
                be.BreakBeam((int)beamIndex, byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative);
                return;
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }



        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            return false;
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "Set Beam Start/End Point (Snap to 4x4 grid)",
                    MouseButton = EnumMouseButton.Right
                },
                new WorldInteraction()
                {
                    ActionLangCode = "Set Beam Start/End Point (Snap to 16x16 grid)",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sprint"
                },
                new WorldInteraction()
                {
                    ActionLangCode = "Cancel placement",
                    MouseButton = EnumMouseButton.Left
                },
            };
        }
    }
}
