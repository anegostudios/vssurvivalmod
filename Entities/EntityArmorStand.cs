using System;
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

        public override ItemSlot LeftHandItemSlot => gearInv[15];
        public override ItemSlot RightHandItemSlot => gearInv[16];


        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (gearInv == null)
            {
                gearInv = new InventoryGeneric(17, "gear-" + EntityId, api, onNewSlot);
                gearInv.SlotModified += GearInv_SlotModified;
            }

            if (api.Side == EnumAppSide.Client) {
                WatchedAttributes.RegisterModifiedListener("inventory", readInventoryFromAttributes);
            }

            readInventoryFromAttributes();
        }

        private void readInventoryFromAttributes()
        {
            ITreeAttribute tree = WatchedAttributes["inventory"] as ITreeAttribute;
            if (gearInv != null && tree != null)
            {
                gearInv.FromTreeAttributes(tree);
            }

            (Properties.Client.Renderer as EntityShapeRenderer)?.MarkShapeModified();
        }

        private void GearInv_SlotModified(int slotid)
        {
            ITreeAttribute tree = new TreeAttribute();
            WatchedAttributes["inventory"] = tree;

            gearInv.ToTreeAttributes(tree);
            WatchedAttributes.MarkPathDirty("inventory");
        }

        private ItemSlot onNewSlot(int slotId, InventoryGeneric self)
        {
            if (slotId >= 15) return new ItemSlotSurvival(self);

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
            IPlayer plr = (byEntity as EntityPlayer)?.Player;
            if (plr != null && !byEntity.World.Claims.TryAccess(plr, Pos.AsBlockPos, EnumBlockAccessFlags.Use))
            {
                plr.InventoryManager.ActiveHotbarSlot.MarkDirty();
                WatchedAttributes.MarkAllDirty();
                return;
            }

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
                    if (slot.Itemstack.Collectible.Tool != null || slot.Itemstack.ItemAttributes?["toolrackTransform"].Exists == true)
                    {
                        handslot.TryPutInto(byEntity.World, RightHandItemSlot);
                        return;
                    }

                    if (!ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorBody) && !ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorHead) && !ItemSlotCharacter.IsDressType(slot.Itemstack, EnumCharacterDressType.ArmorLegs)) {

                        (byEntity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "cantplace", "Cannot place dresses or other non-armor items on armor stands");

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

                if (empty && byEntity.Controls.Sneak)
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
