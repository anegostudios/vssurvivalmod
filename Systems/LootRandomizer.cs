using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SaveLootRandomizerAttributes
    {
        public string InventoryId;
        public int SlotId;

        public byte[] attributes;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SaveStackRandomizerAttributes
    {
        public string InventoryId;
        public int SlotId;

        public float TotalChance;
    }



    public class ModLootRandomizer : ModSystem
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        Dictionary<ItemSlot, GuiDialogGeneric> dialogs = new Dictionary<ItemSlot, GuiDialogGeneric>();

        IServerNetworkChannel serverChannel;
        IClientNetworkChannel clientChannel;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.Event.RegisterEventBusListener(OnEventLootRandomizer, 0.5, "OpenLootRandomizerDialog");
            api.Event.RegisterEventBusListener(OnEventStackRandomizer, 0.5, "OpenStackRandomizerDialog");
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            
            clientChannel =
                api.Network.RegisterChannel("lootrandomizer")
               .RegisterMessageType(typeof(SaveLootRandomizerAttributes))
               .RegisterMessageType(typeof(SaveStackRandomizerAttributes))
            ;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            serverChannel =
               api.Network.RegisterChannel("lootrandomizer")
               .RegisterMessageType(typeof(SaveLootRandomizerAttributes))
               .RegisterMessageType(typeof(SaveStackRandomizerAttributes))
               .SetMessageHandler<SaveLootRandomizerAttributes>(OnLootRndMsg)
               .SetMessageHandler<SaveStackRandomizerAttributes>(OnStackRndMsg);
            ;
        }


        private void OnLootRndMsg(IServerPlayer fromPlayer, SaveLootRandomizerAttributes networkMessage)
        {
            ItemSlot slot = fromPlayer.InventoryManager.GetInventory(networkMessage.InventoryId)?[networkMessage.SlotId];
            if (slot == null) return;

            using (MemoryStream ms = new MemoryStream(networkMessage.attributes))
            {
                slot.Itemstack.Attributes.FromBytes(new BinaryReader(ms));
            }
            
        }

        
        private void OnStackRndMsg(IServerPlayer fromPlayer, SaveStackRandomizerAttributes networkMessage)
        {
            ItemSlot slot = fromPlayer.InventoryManager.GetInventory(networkMessage.InventoryId)?[networkMessage.SlotId];
            if (slot == null) return;

            slot.Itemstack.Attributes.SetFloat("totalChance", networkMessage.TotalChance);
        }

        private void OnEventLootRandomizer(string eventName, ref EnumHandling handling, IAttribute data)
        {
            if (capi == null) return;

            string inventoryd = (data as TreeAttribute).GetString("inventoryId");
            int slotId = (data as TreeAttribute).GetInt("slotId");
            ItemSlot slot = capi.World.Player.InventoryManager.GetInventory(inventoryd)[slotId];

            if (dialogs.ContainsKey(slot)) return;

            float[] chances = new float[10];
            ItemStack[] stacks = new ItemStack[10];
            int i = 0;
            foreach (var val in slot.Itemstack.Attributes)
            {
                if (!val.Key.StartsWith("stack") || !(val.Value is TreeAttribute)) continue;

                TreeAttribute subtree = val.Value as TreeAttribute;

                chances[i] = subtree.GetFloat("chance");
                stacks[i] = subtree.GetItemstack("stack");
                stacks[i].ResolveBlockOrItem(capi.World);
                i++;
            }
            

            dialogs[slot] = new GuiDialogItemLootRandomizer(stacks, chances, capi);
            dialogs[slot].TryOpen();
            dialogs[slot].OnClosed += () => DidCloseLootRandomizer(slot, dialogs[slot]);
        }


        private void OnEventStackRandomizer(string eventName, ref EnumHandling handling, IAttribute data)
        {
            if (capi == null) return;

            string inventoryd = (data as TreeAttribute).GetString("inventoryId");
            int slotId = (data as TreeAttribute).GetInt("slotId");
            ItemSlot slot = capi.World.Player.InventoryManager.GetInventory(inventoryd)[slotId];
            if (dialogs.ContainsKey(slot)) return;

            dialogs[slot] = new GuiDialogItemStackRandomizer((data as TreeAttribute).GetFloat("totalChance"), capi);
            dialogs[slot].TryOpen();
            dialogs[slot].OnClosed += () => DidCloseStackRandomizer(slot, dialogs[slot]);
        }

        private void DidCloseStackRandomizer(ItemSlot slot, GuiDialogGeneric dialog)
        {
            dialogs.Remove(slot);
            if (slot.Itemstack == null) return;

            if (dialog.Attributes.GetInt("save") == 0) return;

            slot.Itemstack.Attributes.SetFloat("totalChance", dialog.Attributes.GetFloat("totalChance"));

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                slot.Itemstack.Attributes.ToBytes(writer);

                clientChannel.SendPacket(new SaveStackRandomizerAttributes()
                {
                    TotalChance = dialog.Attributes.GetFloat("totalChance"),
                    InventoryId = slot.Inventory.InventoryID,
                    SlotId = slot.Inventory.GetSlotId(slot)
                });
            }
        }

        private void DidCloseLootRandomizer(ItemSlot slot, GuiDialogGeneric dialog)
        {
            dialogs.Remove(slot);
            if (slot.Itemstack == null) return;

            if (dialog.Attributes.GetInt("save") == 0) return;

            foreach (var val in dialog.Attributes)
            {
                slot.Itemstack.Attributes[val.Key] = val.Value;
            }

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                slot.Itemstack.Attributes.ToBytes(writer);

                clientChannel.SendPacket(new SaveLootRandomizerAttributes()
                {
                    attributes = ms.ToArray(),
                    InventoryId = slot.Inventory.InventoryID,
                    SlotId = slot.Inventory.GetSlotId(slot)
                });
            }

        }


    }
}
