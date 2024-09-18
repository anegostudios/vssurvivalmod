using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityArmorStand : EntityHumanoid
    {
        EntityBehaviorArmorStandInventory invbh;
        float fireDamage;
        public override bool IsCreature { get { return false; } }

        int CurPose
        {
            get { return WatchedAttributes.GetInt("curPose"); }
            set { WatchedAttributes.SetInt("curPose", value); }
        }

        public EntityArmorStand() { }

        public override ItemSlot RightHandItemSlot => invbh?.Inventory[15];
        public override ItemSlot LeftHandItemSlot => invbh?.Inventory[16];


        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            invbh = GetBehavior<EntityBehaviorArmorStandInventory>();
        }

        string[] poses = new string[] { "idle", "lefthandup", "righthandup", "twohandscross" };

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            IPlayer plr = (byEntity as EntityPlayer)?.Player;
            if (plr != null && !byEntity.World.Claims.TryAccess(plr, Pos.AsBlockPos, EnumBlockAccessFlags.Use))
            {
                plr.InventoryManager.ActiveHotbarSlot.MarkDirty();
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

            if (mode == EnumInteractMode.Interact && byEntity.RightHandItemSlot != null)
            {
                ItemSlot handslot = byEntity.RightHandItemSlot;
                if (handslot.Empty)
                {
                    // Start from armor slot because it can't wear clothes atm
                    for (int i = 0; i < invbh.Inventory.Count; i++)
                    {
                        ItemSlot gslot = invbh.Inventory[i];
                        if (gslot.Empty) continue;
                        if (gslot.Itemstack.Collectible?.Code == null) { gslot.Itemstack = null; continue; }

                        if (gslot.TryPutInto(byEntity.World, handslot) > 0)
                        {
                            return;
                        }
                    }
                } else
                {
                    if (slot.Itemstack.Collectible.Tool != null || slot.Itemstack.ItemAttributes?["toolrackTransform"].Exists == true)
                    {
                        if (handslot.TryPutInto(byEntity.World, RightHandItemSlot) == 0)
                        {
                            handslot.TryPutInto(byEntity.World, LeftHandItemSlot);
                        }

                        return;
                    }

                    if (!ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorBody) && !ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorHead) && !ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorLegs)) {

                        (byEntity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "cantplace", "Cannot place dresses or other non-armor items on armor stands");

                        return;
                    }
                }


                WeightedSlot sinkslot = invbh.Inventory.GetBestSuitedSlot(handslot);
                if (sinkslot.weight > 0 && sinkslot.slot != null)
                {
                    handslot.TryPutInto(byEntity.World, sinkslot.slot);
                    return;
                }

                bool empty = true;
                for (int i = 0; i < invbh.Inventory.Count; i++)
                {
                    ItemSlot gslot = invbh.Inventory[i];
                    empty &= gslot.Empty;
                }

                if (empty && byEntity.Controls.ShiftKey)
                {
                    ItemStack stack = new ItemStack(byEntity.World.GetItem(Code));
                    if (!byEntity.TryGiveItemStack(stack))
                    {
                        byEntity.World.SpawnItemEntity(stack, ServerPos.XYZ);
                    }
                    Die();
                    return;
                }
            }



            if (!Alive || World.Side == EnumAppSide.Client || mode == 0)
            {
                return;
            }


            base.OnInteract(byEntity, slot, hitPosition, mode);
        }



        public override bool ReceiveDamage(DamageSource damageSource, float damage)
        {
            if (damageSource.Source == EnumDamageSource.Internal && damageSource.Type == EnumDamageType.Fire) fireDamage += damage;
            if (fireDamage > 4) Die();

            return base.ReceiveDamage(damageSource, damage);
        }

    }
}
