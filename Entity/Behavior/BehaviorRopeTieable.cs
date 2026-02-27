using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorRopeTieable : EntityBehavior
    {
        int minGeneration = 0;

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

            minGeneration = attributes?["minGeneration"].AsInt(0) ?? 0;
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);
            EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi != null)
            {
                taskAi.TaskManager.OnShouldExecuteTask += (t) => (entity as EntityAgent)?.MountedOn == null || (t is AiTaskIdle);
            }

            var clothids = ClothIds;
            if (clothids == null || clothids.value.Length == 0) return;
            foreach (int clothid in clothids.value)
            {
                var sys = entity.World.Api.ModLoader.GetModSystem<ClothManager>().GetClothSystem(clothid);
                if (sys != null)
                {
                    sys.OnPinnnedEntityLoaded(entity);
                }
            }
        }


        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            var clothids = ClothIds;
            if (clothids == null || clothids.value.Length == 0) return;

            int clothid = ClothIds.value[0];
            ClothIds.RemoveInt(clothid);

            var cm = byEntity.World.Api.ModLoader.GetModSystem<ClothManager>();
            var sys = cm.GetClothSystem(clothid);
            if (sys != null)
            {
                Detach(sys);

                ItemSlot ropeInhandsSlot = null;
                var ends = sys.Ends;
                byEntity.WalkInventory(slot =>
                {
                    if (slot.Empty) return true;
                    if (slot.Itemstack.Collectible is ItemRope)
                    {
                        if (slot.Itemstack.Attributes.GetInt("clothId") == sys.ClothId)
                        {
                            ropeInhandsSlot = slot;
                        }
                        return false;
                    }

                    return true;
                });

                if (ropeInhandsSlot == null)
                {
                    Vec3d lpos = new Vec3d(0, byEntity.LocalEyePos.Y - 0.25f, 0);
                    Vec3d aheadPos = lpos.AheadCopy(0.25f, byEntity.Pos.Pitch, byEntity.Pos.Yaw);
                    (ends[0].Pinned ? ends[1] : ends[0]).PinTo(byEntity, aheadPos.ToVec3f());

                    if ((ends[0].PinnedToEntity as EntityItem)?.Itemstack?.Collectible is ItemRope || (ends[1].PinnedToEntity as EntityItem)?.Itemstack?.Collectible is ItemRope)
                    {
                        ItemStack stack = new ItemStack(entity.World.GetItem(new AssetLocation("rope")));
                        stack.Attributes.SetInt("clothId", sys.ClothId);
                        stack.Attributes.SetLong("ropeHeldByEntityId", byEntity.EntityId);

                        if (!byEntity.TryGiveItemStack(stack))
                        {
                            entity.World.SpawnItemEntity(stack, byEntity.Pos.XYZ);
                        }
                    }

                } else
                {
                    ends[0].UnPin();
                    ends[1].UnPin();
                    ropeInhandsSlot.Itemstack.Attributes.RemoveAttribute("clothId");
                    ropeInhandsSlot.Itemstack.Attributes.RemoveAttribute("ropeHeldByEntityId");
                    cm.UnregisterCloth(sys.ClothId);
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

        public bool CanAttach()
        {
            if (entity.WatchedAttributes.GetInt("generation") < minGeneration)
            {
                var capi = entity.World.Api as ICoreClientAPI;
                capi?.TriggerIngameError(this, "toowild", Lang.Get("Animal is too wild to attach a rope to"));
                return false;
            }

            return true;
        }

        public void Attach(ClothSystem sys, ClothPoint point)
        {
            if (!entity.WatchedAttributes.HasAttribute("clothIds"))
            {
                entity.WatchedAttributes["clothIds"] = new IntArrayAttribute(new int[] { sys.ClothId });
            }

            if (!ClothIds.value.Contains(sys.ClothId))
            {
                ClothIds.AddInt(sys.ClothId);
            }

            point.PinTo(entity, new Vec3f(0, 0.5f, 0));
        }
    }
}
