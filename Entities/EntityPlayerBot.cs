using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityPlayerBot : EntityAnimalBot
    {
        //public override double EyeHeight => base.Properties.EyeHeight - (controls.Sneak ? 0.1 : 0.0);

        protected InventoryBase inv;
       

        public override bool StoreWithChunk
        {
            get { return true; }
        }
        

        public override IInventory GearInventory
        {
            get
            {
                return inv;
            }
        }

        public override ItemSlot RightHandItemSlot {
            get
            {
                return inv[15];
            }
        }

        public EntityPlayerBot() : base()
        {
            inv = new InventoryGear(null, null);
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long chunkindex3d)
        {
            base.Initialize(properties, api, chunkindex3d);

            inv.LateInitialize("gearinv-" + EntityId, api);

            Name = WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name");
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (World.Side == EnumAppSide.Client)
            {
                (Properties.Client.Renderer as EntityShapeRenderer).DoRenderHeldItem = true;
            }

        }
        




        /*public override void SetName(string playername)
        {
            base.SetName(playername);
            this.Name = playername;
        }*/

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            TreeAttribute tree;
            WatchedAttributes["gearInv"] = tree = new TreeAttribute();
            inv.ToTreeAttributes(tree);
            

            base.ToBytes(writer, forClient);
        }


        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            TreeAttribute tree = WatchedAttributes["gearInv"] as TreeAttribute;
            if (tree != null) inv.FromTreeAttributes(tree);
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            base.OnInteract(byEntity, slot, hitPosition, mode);

            if ((byEntity as EntityPlayer)?.Controls.Sneak == true && mode == EnumInteractMode.Interact && byEntity.World.Side == EnumAppSide.Server)
            {
                inv.DiscardAll();
                WatchedAttributes.MarkAllDirty();
            }
        }

    }




    public class InventoryGear : InventoryBase
    {
        ItemSlot[] slots;

        public InventoryGear(string className, string id, ICoreAPI api) : base(className, id, api)
        {
            slots = GenEmptySlots(16);
            baseWeight = 2.5f;
        }

        public InventoryGear(string inventoryId, ICoreAPI api) : base(inventoryId, api)
        {
            slots = GenEmptySlots(16);
            baseWeight = 2.5f;
        }

        public override void OnItemSlotModified(ItemSlot slot)
        {
            base.OnItemSlotModified(slot);
        }


        public override int Count
        {
            get { return slots.Length; }
        }

        public override ItemSlot this[int slotId] { get { return slots[slotId]; }  set { slots[slotId] = value; } }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            List<ItemSlot> modifiedSlots = new List<ItemSlot>();
            slots = SlotsFromTreeAttributes(tree, slots, modifiedSlots);
            for (int i = 0; i < modifiedSlots.Count; i++) DidModifyItemSlot(modifiedSlots[i]);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);
            //ResolveBlocksOrItems();
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
            { EnumCharacterDressType.Face, "face" },
        };


        protected override ItemSlot NewSlot(int slotId)
        {
            if (slotId == 15) return new ItemSlotSurvival(this);

            EnumCharacterDressType type = (EnumCharacterDressType)slotId;
            ItemSlotCharacter slot = new ItemSlotCharacter(type, this);
            iconByDressType.TryGetValue(type, out slot.BackgroundIcon);

            return slot;
        }

        public override void DiscardAll()
        {
            base.DiscardAll();
            for (int i = 0; i < Count; i++)
            {
                DidModifyItemSlot(this[i]);
            }
        }

        public override void OnOwningEntityDeath(Vec3d pos)
        {
            // Don't drop contents on death
        }
    }
}
