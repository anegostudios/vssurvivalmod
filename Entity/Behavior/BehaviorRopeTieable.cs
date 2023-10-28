using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRopeTieable : EntityBehavior
    {
        public IntArrayAttribute ClothIds
        {
            get
            {
                return (entity.WatchedAttributes["clothIds"] as IntArrayAttribute);
            }
        }

        
        public EntityBehaviorRopeTieable(Entity entity) : base(entity)
        {

        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
        }


        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            var clothids = ClothIds;
            if (clothids == null || clothids.value.Length == 0) return;

            int clothid = ClothIds.value[0];
            ClothIds.RemoveInt(clothid);

            var sys = byEntity.World.Api.ModLoader.GetModSystem<ClothManager>().GetClothSystem(clothid);
            if (sys != null)
            {
                Detach(sys);

                var ends = sys.Ends;

                Vec3d lpos = new Vec3d(0, byEntity.LocalEyePos.Y - 0.25f, 0);
                Vec3d aheadPos = lpos.AheadCopy(0.25f, byEntity.SidedPos.Pitch, byEntity.SidedPos.Yaw);
                (ends[0].Pinned ? ends[1] : ends[0]).PinTo(byEntity, aheadPos.ToVec3f());

                ItemStack stack = new ItemStack(entity.World.GetItem(new AssetLocation("rope")));
                stack.Attributes.SetInt("clothId", sys.ClothId);

                if (!byEntity.TryGiveItemStack(stack))
                {
                    entity.World.SpawnItemEntity(stack, byEntity.Pos.XYZ);
                }
            }
        }

        public override string PropertyName()
        {
            return "ropetieable";
        }

        public void Detach(ClothSystem sys)
        {
            if (ClothIds == null) return;

            ClothIds.RemoveInt(sys.ClothId);


            if (ClothIds.value.Length == 0)
            {
                entity.WatchedAttributes.RemoveAttribute("clothIds");
            }

            sys.WalkPoints(point =>
            {
                if (point.PinnedToEntity?.EntityId == entity.EntityId)
                {
                    point.UnPin();
                }
            });
        }

        public void Attach(ClothSystem sys, ClothPoint point)
        {
            if (!entity.WatchedAttributes.HasAttribute("clothIds"))
            {
                entity.WatchedAttributes["clothIds"] = new IntArrayAttribute(new int[] { sys.ClothId });
            }

            ClothIds.AddInt(sys.ClothId);

            point.PinTo(entity, new Vec3f(0, 0.5f, 0));
        }
    }
}
