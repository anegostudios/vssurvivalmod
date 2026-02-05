using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityArmorStand : EntityAgent
    {
        float fireDamage;

        public override bool IsCreature => false;

        int CurPose
        {
            get { return WatchedAttributes.GetInt("curPose"); }
            set { WatchedAttributes.SetInt("curPose", value); }
        }

        string[] poses = new string[] { "idle", "lefthandup", "righthandup", "twohandscross" };

        public EntityArmorStand() { }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            TryEarlyUpdateOldArmorStandInventory(api.World);
            base.Initialize(properties, api, InChunkIndex3d);

            AnimManager.StartAnimation(new AnimationMetaData() { Animation = poses[CurPose], Code = poses[CurPose] }.Init());
        }

        /// <summary>
        /// update the old inventory to new wearablesinv
        /// TryEarlyLoadCollectibleMappings will take care of properly updating the items to their new ID's if needed
        /// </summary>
        /// <param name="world"></param>
        public void TryEarlyUpdateOldArmorStandInventory(IWorldAccessor world)
        {
            if (world.Api.Side == EnumAppSide.Server && WatchedAttributes.HasAttribute("inventory"))
            {
                var slots = WatchedAttributes.GetTreeAttribute("inventory").GetTreeAttribute("slots");
                if (slots.Count > 0)
                {
                    var oldSlots = new string[] { "12", "13", "14", "15", "16"};
                    var newInventory = new TreeAttribute();
                    newInventory.SetInt("qslots", 5);
                    var newSlots = new TreeAttribute();
                    newInventory.SetAttribute("slots", newSlots);

                    for (var index = 0; index < oldSlots.Length; index++)
                    {
                        var slotKey = oldSlots[index];
                        if (!slots.HasAttribute(slotKey)) continue;

                        var newStack = slots.GetItemstack(slotKey);
                        newSlots.SetItemstack(index.ToString(), newStack);
                    }
                    WatchedAttributes.SetAttribute("wearablesInv", newInventory);
                }

                WatchedAttributes.RemoveAttribute("inventory");
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (!Alive || World.Side == EnumAppSide.Client || mode == EnumInteractMode.Attack)
            {
                return;
            }

            var player = (byEntity as EntityPlayer)?.Player;
            if (player != null && !byEntity.World.Claims.TryAccess(player, Pos.AsBlockPos, EnumBlockAccessFlags.Use))
            {
                player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                WatchedAttributes.MarkAllDirty();
                return;
            }

            if (mode == EnumInteractMode.Interact && byEntity.RightHandItemSlot?.Itemstack?.Collectible is ItemWrench)
            {
                AnimManager.StopAnimation(poses[CurPose]);
                CurPose = (CurPose + 1) % poses.Length;
                AnimManager.StartAnimation(new AnimationMetaData() { Animation = poses[CurPose], Code = poses[CurPose] }.Init());
                return;
            }

            base.OnInteract(byEntity, slot, hitPosition, mode);
        }

        public override bool ReceiveDamage(DamageSource damageSource, float damage)
        {
            if (damageSource.Source == EnumDamageSource.Internal && damageSource.Type == EnumDamageType.Fire)
            {
                fireDamage += damage;
            }

            if (fireDamage > 4)
            {
                Die();
            }

            return base.ReceiveDamage(damageSource, damage);
        }
    }
}
