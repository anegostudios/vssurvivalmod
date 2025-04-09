using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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
        Dictionary<ItemSlot, GuiDialogGeneric> dialogs = new Dictionary<ItemSlot, GuiDialogGeneric>();
        IClientNetworkChannel clientChannel;

        public override bool ShouldLoad(EnumAppSide side) => true;

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
            api.Network.RegisterChannel("lootrandomizer")
               .RegisterMessageType(typeof(SaveLootRandomizerAttributes))
               .RegisterMessageType(typeof(SaveStackRandomizerAttributes))
               .SetMessageHandler<SaveLootRandomizerAttributes>(OnLootRndMsg)
               .SetMessageHandler<SaveStackRandomizerAttributes>(OnStackRndMsg);
            ;
        }


        private void OnLootRndMsg(IServerPlayer fromPlayer, SaveLootRandomizerAttributes networkMessage)
        {
            if (!fromPlayer.HasPrivilege("controlserver"))
            {
                fromPlayer.SendIngameError("noprivilege", "No privilege to set up a loot randomizer");
                return;
            }

            ItemSlot slot = fromPlayer.InventoryManager.GetInventory(networkMessage.InventoryId)?[networkMessage.SlotId];
            if (slot == null || slot.Empty) return;

            if (!(slot.Itemstack.Collectible is ItemLootRandomizer))
            {
                fromPlayer.SendIngameError("noprivilege", "Not a loot randomizer");
                return;
            }

            using (MemoryStream ms = new MemoryStream(networkMessage.attributes))
            {
                slot.Itemstack.Attributes.FromBytes(new BinaryReader(ms));
            }
        }

        
        private void OnStackRndMsg(IServerPlayer fromPlayer, SaveStackRandomizerAttributes networkMessage)
        {
            if (!fromPlayer.HasPrivilege("controlserver"))
            {
                fromPlayer.SendIngameError("noprivilege", "No privilege to set up a loot randomizer");
                return;
            }

            ItemSlot slot = fromPlayer.InventoryManager.GetInventory(networkMessage.InventoryId)?[networkMessage.SlotId];
            if (slot == null || slot.Empty) return;

            if (!(slot.Itemstack.Collectible is ItemLootRandomizer))
            {
                fromPlayer.SendIngameError("noprivilege", "Not a loot randomizer");
                return;
            }


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
                if (!val.Key.StartsWithOrdinal("stack") || !(val.Value is TreeAttribute)) continue;

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

            ITreeAttribute dialogAttributes = dialog.Attributes;
            if (dialogAttributes.GetInt("save") == 0) return;

            ITreeAttribute stree;
            for (int i = 0; i < 10; i++)
            {
                stree = dialogAttributes["stack" + i] as ITreeAttribute;
                if (stree == null)
                {
                    slot.Itemstack.Attributes.RemoveAttribute("stack" + i);
                    continue;
                }
                slot.Itemstack.Attributes["stack" + i] = stree;
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
