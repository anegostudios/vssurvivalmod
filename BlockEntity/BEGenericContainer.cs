using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityGenericContainer : BlockEntityOpenableContainer
    {
        internal InventoryGeneric inventory;

        public int quantitySlots = 16;
        public string inventoryClassName = "chest";
        public string dialogTitleLangCode = "chestcontents";
        public bool retrieveOnly = false;

        Block ownBlock;

        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        public override string InventoryClassName
        {
            get { return inventoryClassName; }
        }

        public BlockEntityGenericContainer() : base()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            ownBlock = api.World.BlockAccessor.GetBlock(pos);

            // Newly placed 
            if (inventory == null)
            {
                InitInventory(ownBlock);
            }

            base.Initialize(api);
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            if (inventory == null)
            {
                if (tree.HasAttribute("forBlockId"))
                {
                    InitInventory(worldForResolving.GetBlock((ushort)tree.GetInt("forBlockId")));
                }
                else
                {
                    ITreeAttribute inventroytree = tree.GetTreeAttribute("inventory");
                    int qslots = inventroytree.GetInt("qslots");
                    // Must be a basket
                    if (qslots == 8)
                    {
                        quantitySlots = 8;
                        inventoryClassName = "basket";
                        dialogTitleLangCode = "basketcontents";
                    }

                    InitInventory(null);
                }
            }
            

            base.FromTreeAtributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (ownBlock != null) tree.SetInt("forBlockId", ownBlock.BlockId);
        }

        private void InitInventory(Block block)
        {
            this.ownBlock = block;

            if (block?.Attributes != null)
            {
                inventoryClassName = block.Attributes["inventoryClassName"].AsString(inventoryClassName);
                dialogTitleLangCode = block.Attributes["dialogTitleLangCode"].AsString(dialogTitleLangCode);
                quantitySlots = block.Attributes["quantitySlots"].AsInt(quantitySlots);
                retrieveOnly = block.Attributes["retrieveOnly"].AsBool(false);
            }

            inventory = new InventoryGeneric(quantitySlots, null, null, null);

            if (block.Attributes?["spoilSpeedMulByFoodCat"].Exists == true)
            {
                inventory.PerishableFactorByFoodCategory = block.Attributes["spoilSpeedMulByFoodCat"].AsObject<Dictionary<EnumFoodCategory, float>>();
            }

            if (block.Attributes?["transitionSpeedMulByType"].Exists == true)
            {
                inventory.TransitionableSpeedMulByType = block.Attributes["transitionSpeedMulByType"].AsObject<Dictionary<EnumTransitionType, float>>();
            }
            
            inventory.OnInventoryClosed += OnInvClosed;
            inventory.OnInventoryOpened += OnInvOpened;
            inventory.SlotModified += OnSlotModifid;
        }

        private void OnSlotModifid(int slot)
        {
            api.World.BlockAccessor.GetChunkAtBlockPos(pos)?.MarkModified();
        }

        protected virtual void OnInvOpened(IPlayer player)
        {
            inventory.PutLocked = retrieveOnly && player.WorldData.CurrentGameMode != EnumGameMode.Creative;
        }

        protected virtual void OnInvClosed(IPlayer player)
        {
            inventory.PutLocked = retrieveOnly;
            invDialog?.Dispose();
            invDialog = null;
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api.World is IServerWorldAccessor)
            {
                byte[] data;

                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    writer.Write("BlockEntityInventory");
                    writer.Write(Lang.Get(dialogTitleLangCode));
                    writer.Write((byte)4);
                    TreeAttribute tree = new TreeAttribute();
                    inventory.ToTreeAttributes(tree);
                    tree.ToBytes(writer);
                    data = ms.ToArray();
                }

                ((ICoreServerAPI)api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    pos.X, pos.Y, pos.Z,
                    (int)EnumBlockContainerPacketId.OpenInventory,
                    data
                );

                byPlayer.InventoryManager.OpenInventory(inventory);
            }

            return true;
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            if (retrieveOnly)
            {
                return base.GetBlockInfo(forPlayer);
            }

            return null;
        }
    }
}
