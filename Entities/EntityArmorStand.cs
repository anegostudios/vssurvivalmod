﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityArmorStand : EntityHumanoid
    {
        InventoryGeneric gearInv;
        public override IInventory GearInventory => gearInv;

        public EntityArmorStand()
        {
            
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (gearInv == null)
            {
                gearInv = new InventoryGeneric(15, "gear-" + EntityId, api, onNewSlot);
                gearInv.SlotModified += GearInv_SlotModified;
            }

            ITreeAttribute tree = WatchedAttributes["inventory"] as ITreeAttribute;
            if (gearInv != null && tree != null)
            {
                gearInv.FromTreeAttributes(tree);
            }
        }

        private void GearInv_SlotModified(int t1)
        {
            ITreeAttribute tree = new TreeAttribute();
            WatchedAttributes["inventory"] = tree;

            gearInv.ToTreeAttributes(tree);
            WatchedAttributes.MarkPathDirty("inventory");
        }

        private ItemSlot onNewSlot(int slotId, InventoryGeneric self)
        {
            EnumCharacterDressType type = (EnumCharacterDressType)slotId;
            ItemSlotCharacter slot = new ItemSlotCharacter(type, self);
            iconByDressType.TryGetValue(type, out slot.BackgroundIcon);

            return slot;
        }

        Dictionary<EnumCharacterDressType, string> iconByDressType = new Dictionary<EnumCharacterDressType, string>()
        {
            { EnumCharacterDressType.Foot, "boots" },
            { EnumCharacterDressType.Hand, "gloves" },
            { EnumCharacterDressType.Shoulder, "cape" },
            { EnumCharacterDressType.Head, "hat" },
            { EnumCharacterDressType.LowerBody, "trousers" },
            { EnumCharacterDressType.UpperBody, "shirt" },
            { EnumCharacterDressType.UpperBodyOver, "pullover" },
            { EnumCharacterDressType.Neck, "necklace" },
            { EnumCharacterDressType.Arm, "bracers" },
            { EnumCharacterDressType.Waist, "belt" },
            { EnumCharacterDressType.Emblem, "medal" },
            { EnumCharacterDressType.Face, "mask" },
        };


        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            ITreeAttribute tree = WatchedAttributes["inventory"] as ITreeAttribute;
            if (gearInv != null && tree != null)
            {
                gearInv.FromTreeAttributes(tree);
            }
        }



        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode == EnumInteractMode.Interact && byEntity.RightHandItemSlot != null)
            {
                ItemSlot handslot = byEntity.RightHandItemSlot;
                if (handslot.Empty)
                {
                    // Start from armor slot because it can't wear clothes atm
                    for (int i = 0; i < GearInventory.Count; i++)
                    {
                        ItemSlot gslot = GearInventory[i];
                        if (gslot.Empty) continue;

                        if (gslot.TryPutInto(byEntity.World, handslot) > 0)
                        {
                            return;
                        }   
                    }
                } else
                {
                    if (!ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorBody) && !ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorHead) && !ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorLegs)) {

                        (byEntity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "cantplace", "Cannot place dresses on armor stands");

                        return;
                    }
                }

                
                WeightedSlot sinkslot = GearInventory.GetBestSuitedSlot(handslot);
                if (sinkslot.weight > 0 && sinkslot.slot != null)
                {
                    handslot.TryPutInto(byEntity.World, sinkslot.slot);
                    return;
                }

                
                bool empty = true;
                for (int i = 0; i < GearInventory.Count; i++)
                {
                    ItemSlot gslot = GearInventory[i];
                    empty &= gslot.Empty;
                }

                if (empty)
                {
                    ItemStack stack = new ItemStack(byEntity.World.GetItem(new AssetLocation("armorstand")));
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
                //base.OnInteract(byEntity, slot, hitPosition, mode);
                return;
            }


            base.OnInteract(byEntity, slot, hitPosition, mode);
        }


        float fireDamage;

        public override bool ReceiveDamage(DamageSource damageSource, float damage)
        {
            if (damageSource.Source == EnumDamageSource.Internal && damageSource.Type == EnumDamageType.Fire) fireDamage += damage;
            if (fireDamage > 4) Die();

            return base.ReceiveDamage(damageSource, damage);
        }

    }
}
