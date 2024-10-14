using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class EntityDeconstructTool : CollectibleBehavior
    {
        public EntityDeconstructTool(CollectibleObject collObj) : base(collObj) { }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (entitySel == null) return;

            var attr = entitySel.Entity.Properties.Attributes;
            if (attr?.IsTrue("deconstructible") == true)
            {
                handHandling = EnumHandHandling.PreventDefault;
                handling = EnumHandling.PreventDefault;
                byEntity.StartAnimation("saw");
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (entitySel == null)
            {
                byEntity.StopAnimation("saw");
                return false;
            }

            handling = EnumHandling.PreventDefault;

            if (byEntity.World.Side == EnumAppSide.Server) return true;

            return secondsUsed < 4;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (entitySel == null)
            {
                byEntity.StopAnimation("saw");
                return;
            }

            var entity = entitySel.Entity;
            entity.Die();
            byEntity.StopAnimation("saw");

            var world = byEntity.World;
            if (world.Side == EnumAppSide.Server)
            {
                var dropStacks = entitySel.Entity.Properties.Attributes["deconstructDrops"].AsObject<JsonItemStack[]>();
                foreach (var dropStack in dropStacks)
                {
                    if (dropStack.Resolve(world, byEntity.Code + " entity deconstruction drop.", true))
                    {
                        world.SpawnItemEntity(dropStack.ResolvedItemstack, entitySel.Entity.ServerPos.XYZ);
                    }
                }
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
        {
            byEntity.StopAnimation("saw");
            return true;
        }
    }
}