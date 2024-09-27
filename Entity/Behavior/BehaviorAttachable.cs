using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public interface IRopeTiedCreatureCarrier
    {
        bool TryMount(EntityAgent pinnedToEntity);
    }

    public class ItemSlotWearable : ItemSlot
    {
        public string[] canHoldWearableCodes;
        public ItemSlotWearable(InventoryBase inventory, string[] canHoldWearableCodes) : base(inventory)
        {
            this.canHoldWearableCodes = canHoldWearableCodes;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            if (!IsDressType(sourceSlot.Itemstack, canHoldWearableCodes)) return false;
            return base.CanTakeFrom(sourceSlot, priority);
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            if (!IsDressType(itemstackFromSourceSlot.Itemstack, canHoldWearableCodes)) return false;

            return base.CanHold(itemstackFromSourceSlot);
        }

        /// <summary>
        /// Checks to see what dress type the given item is.
        /// </summary>
        /// <param name="itemstack"></param>
        /// <param name="dressType"></param>
        /// <returns></returns>
        public bool IsDressType(ItemStack itemstack, string[] slotWearableCodes)
        {
            if (itemstack == null) return false;

            var iatta = IAttachableToEntity.FromCollectible(itemstack.Collectible, inventory?.Api.Logger);

            return iatta != null && slotWearableCodes.IndexOf(iatta.GetCategoryCode(itemstack)) >= 0;
        }
    }

    // An entity can provide seats and slots
    // A slot however if filled with a chair can also provide a seat
    // Or a slot can also be configured to be usable as a seat if empty
    public class WearableSlotConfig
    {
        public SeatConfig SeatConfig;
        public string Code;
        public string[] ForCategoryCodes;
        public string[] CanMergeWith;
        public string AttachmentPointCode;
        public Dictionary<string, StepParentElementTo> StepParentTo;
        public string ProvidesSeatId = null;
        public bool EmptyInteractPassThrough { get; set; }

        public bool CanHold(string code)
        {
            for (int i = 0; i < ForCategoryCodes.Length; i++)
            {
                if (ForCategoryCodes[i] == code) return true;
            }

            return false;
        }
    }




    public class EntityBehaviorAttachable : EntityBehaviorContainer
    {
        protected WearableSlotConfig[] wearableSlots;
        public override InventoryBase Inventory => inv;
        protected InventoryGeneric inv;

        public override string InventoryClassName => "wearablesInv";

        public EntityBehaviorAttachable(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            Api = entity.World.Api;
            wearableSlots = attributes["wearableSlots"].AsObject<WearableSlotConfig[]>();

            inv = new InventoryGeneric(wearableSlots.Length, InventoryClassName + "-" + entity.EntityId, entity.Api, (id, inv) => new ItemSlotWearable(inv, wearableSlots[id].ForCategoryCodes));
            loadInv();

            entity.WatchedAttributes.RegisterModifiedListener("wearablesInv", wearablesModified);

            base.Initialize(properties, attributes);
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);
            updateSeats();
        }

        private void wearablesModified()
        {
            loadInv();
            updateSeats();
            entity.MarkShapeModified();
        }

        void updateSeats()
        {
            var ivsm = entity.GetInterface<IVariableSeatsMountable>();
            if (ivsm == null) return;

            for (int i = 0; i < wearableSlots.Length; i++)
            {
                var slotcfg = wearableSlots[i];
                slotcfg.SeatConfig = null;

                var itemslot = inv[i];
                if (itemslot.Empty)
                {
                    if (slotcfg.ProvidesSeatId != null)
                    {
                        ivsm.RemoveSeat(slotcfg.ProvidesSeatId);
                        slotcfg.ProvidesSeatId = null;
                    }
                    continue;
                }
                
                var attrseatconfig = itemslot.Itemstack?.ItemAttributes?["attachableToEntity"]?["seatConfig"]?.AsObject<SeatConfig>();
                if (attrseatconfig != null)
                {
                    attrseatconfig.SeatId = "attachableseat-" + i;
                    attrseatconfig.APName = slotcfg.AttachmentPointCode;

                    slotcfg.SeatConfig = attrseatconfig;
                    
                    ivsm.RegisterSeat(slotcfg.SeatConfig);
                    slotcfg.ProvidesSeatId = slotcfg.SeatConfig.SeatId;
                } else
                {
                    if (slotcfg.ProvidesSeatId != null)
                    {
                        ivsm.RemoveSeat(slotcfg.ProvidesSeatId);
                    }
                }
            }
        }

        public override bool TryGiveItemStack(ItemStack itemstack, ref EnumHandling handling)
        {
            int index = 0;
            DummySlot sourceslot = new DummySlot(itemstack);
            foreach (var slot in inv)
            {
                var targetSlot = GetSlotFromSelectionBoxIndex(index);
                if (targetSlot != null && TryAttach(sourceslot, index, null))
                {
                    handling = EnumHandling.PreventDefault;
                    return true;
                }

                index++;
            }

            return base.TryGiveItemStack(itemstack, ref handling);
        }
        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            int seleBox = (byEntity as EntityPlayer).EntitySelection?.SelectionBoxIndex ?? -1;
            if (seleBox <= 0) return;

            var index = GetSlotIndexFromSelectionBoxIndex(seleBox - 1);
            var slot = index >= 0 ? inv[index] : null;
            if (slot == null) return;
            handled = EnumHandling.PreventSubsequent;

            var controls = byEntity.MountedOn?.Controls ?? byEntity.Controls;

            if (mode == EnumInteractMode.Interact && !controls.Sprint)
            {
                if (slot.Itemstack?.Collectible.Attributes?.IsTrue("interactPassthrough") == true)
                {
                    handled = EnumHandling.PassThrough;
                    return;
                }
                if (slot.Empty && wearableSlots[index].EmptyInteractPassThrough)
                {
                    handled = EnumHandling.PassThrough;
                    return;
                }
                if (wearableSlots[index].SeatConfig != null)
                {
                    handled = EnumHandling.PassThrough;
                    return;
                }
            }
            
            var iai = slot.Itemstack?.Collectible.GetCollectibleInterface<IAttachedInteractions>();
            if (iai != null)
            {
                EnumHandling itemhanndled = EnumHandling.PassThrough;
                iai.OnInteract(slot, seleBox - 1, entity, byEntity, hitPosition, mode, ref itemhanndled, storeInv);
                if (itemhanndled == EnumHandling.PreventDefault || itemhanndled == EnumHandling.PreventSubsequent) return;
            }


            if (mode != EnumInteractMode.Interact || !controls.Sprint)
            {
                handled = EnumHandling.PassThrough; // Can't attack an elk with a falx otherwise
                /*if (itemslot.Empty && GetSlotFromSelectionBoxIndex(seleBox - 1).Empty) 
                {
                    handled = EnumHandling.PassThrough;
                    return;
                }*/
                return;
            }

            if (!itemslot.Empty)
            {
                if (TryAttach(itemslot, seleBox - 1, byEntity))
                {
                    onAttachmentToggled(byEntity, itemslot);
                    return;
                }
            } else
            {
                if (TryRemoveAttachment(byEntity, seleBox - 1)) // -1 because index 0 is the elk itself
                {
                    onAttachmentToggled(byEntity, itemslot);
                    return;
                }
            }

            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
        }

        private void onAttachmentToggled(EntityAgent byEntity, ItemSlot itemslot)
        {
            var sound = itemslot.Itemstack?.Block?.Sounds.Place ?? new AssetLocation("sounds/player/build");
            Api.World.PlaySoundAt(sound, entity, (byEntity as EntityPlayer).Player, true, 16);
            entity.MarkShapeModified();
            // Tell server to save this chunk to disk again
            entity.World.BlockAccessor.GetChunkAtBlockPos(entity.ServerPos.AsBlockPos).MarkModified();
        }

        private bool TryRemoveAttachment(EntityAgent byEntity, int slotIndex)
        {
            var slot = GetSlotFromSelectionBoxIndex(slotIndex);

            if (slot == null || slot.Empty) return false;
            
            if (slot.StackSize == 0 || byEntity.TryGiveItemStack(slot.Itemstack))
            {
                var iai = slot.Itemstack.Collectible.GetCollectibleInterface<IAttachedListener>();
                iai?.OnDetached(slot, slotIndex, entity);

                slot.Itemstack = null;
                storeInv();
                return true;
            }

            return false;
        }

        private bool TryAttach(ItemSlot itemslot, int slotIndex, EntityAgent byEntity)
        {
            var iatta = IAttachableToEntity.FromCollectible(itemslot.Itemstack.Collectible, entity.World.Logger);
            if (iatta == null || !iatta.IsAttachable(itemslot.Itemstack)) return false;

            var targetSlot = GetSlotFromSelectionBoxIndex(slotIndex);

            string code = iatta.GetCategoryCode(itemslot.Itemstack);
            var slotConfig = wearableSlots[slotIndex];

            if (!slotConfig.CanHold(code)) return false;
            if (!targetSlot.Empty) return false;

            var iai = itemslot.Itemstack.Collectible.GetCollectibleInterface<IAttachedInteractions>();
            if (iai?.OnTryAttach(itemslot, slotIndex, entity) == false) return false;

            var ial = itemslot.Itemstack?.Collectible.GetCollectibleInterface<IAttachedListener>();

            if (entity.World.Side == EnumAppSide.Server)
            {
                var moved = itemslot.TryPutInto(entity.World, targetSlot) > 0;
                if (moved)
                {
                    ial?.OnAttached(itemslot, slotIndex, entity, byEntity);
                    storeInv();
                }

                return moved;
            }

            return true;           
        }


        public ItemSlot GetSlotFromSelectionBoxIndex(int seleBoxIndex)
        {
            var index = GetSlotIndexFromSelectionBoxIndex(seleBoxIndex);
            if (index == -1) return null;
            return inv[index];
        }

        public int GetSlotIndexFromSelectionBoxIndex(int seleBoxIndex)
        {
            var seleBoxes = entity.GetBehavior<EntityBehaviorSelectionBoxes>().selectionBoxes;
            if (seleBoxes.Length <= seleBoxIndex || seleBoxIndex < 0) return -1;

            string apCode = seleBoxes[seleBoxIndex].AttachPoint.Code;

            return wearableSlots.IndexOf(elem => elem.AttachmentPointCode == apCode);
        }


        protected override Shape addGearToShape(Shape entityShape, ItemSlot gearslot, string slotCode, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements, Dictionary<string, StepParentElementTo> overrideStepParent = null)
        {
            int index = gearslot.Inventory.IndexOf((slot) => slot == gearslot);
            overrideStepParent = wearableSlots[index].StepParentTo;
            slotCode = wearableSlots[index].Code;

            return base.addGearToShape(entityShape, gearslot, slotCode, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements, overrideStepParent);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            int i = 0;
            foreach (var slot in inv)
            {
                var iai = slot.Itemstack?.Collectible.GetCollectibleInterface<IAttachedInteractions>();
                iai?.OnEntityDespawn(slot, i++, entity, despawn);
            }

            base.OnEntityDespawn(despawn);
        }


        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            int i = 0;
            foreach (var slot in inv)
            {
                var iai = slot.Itemstack?.Collectible.GetCollectibleInterface<IAttachedInteractions>();
                iai?.OnReceivedClientPacket(slot, i, entity, player, packetid, data, ref handled, storeInv);
                i++;
                if (handled == EnumHandling.PreventSubsequent) break;
            }
        }


        public override string PropertyName() => "dressable";
        public void Dispose() { }

        
    }
}
