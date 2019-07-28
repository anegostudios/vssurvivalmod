using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    class BlockEntityItemFlow : BlockEntityOpenableContainer
    {

        
        internal InventoryGeneric inventory;

        public BlockFacing InputFace = BlockFacing.UP;
        public BlockFacing OutputFace = BlockFacing.DOWN;
        public string inventoryClassName = "hopper";
        public string ItemFlowObjectLangCode = "hoppin-contents";
        public int QuantitySlots = 4;
        public int FlowAmount = 1;

        Block myBlock;

        public override AssetLocation OpenSound => new AssetLocation("sounds/block/hopperopen");
        public override AssetLocation CloseSound => new AssetLocation("sounds/block/nosound");


        public BlockEntityItemFlow() : base()
        {

        }

        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        private void InitInventory(Block block)
        {
            myBlock = block;

            if(block?.Attributes != null)
            {
                InputFace = BlockFacing.FromCode(block.Attributes["input-face"].AsString(InputFace.Code));
                OutputFace = BlockFacing.FromCode(block.Attributes["output-face"].AsString(OutputFace.Code));
                FlowAmount = block.Attributes["item-flowrate"].AsInt(FlowAmount);
                inventoryClassName = block.Attributes["inventoryClassName"].AsString(inventoryClassName);
                ItemFlowObjectLangCode = block.Attributes["itemFlowObjectLangCode"].AsString(ItemFlowObjectLangCode);
                QuantitySlots = block.Attributes["quantitySlots"].AsInt(QuantitySlots);
            }

            if (inventory == null)
            {
                inventory = new InventoryGeneric(QuantitySlots, null, null, null);

                inventory.OnInventoryClosed += OnInvClosed;
                inventory.OnInventoryOpened += OnInvOpened;
                inventory.SlotModified += OnSlotModifid;
            }
        }

        public override string InventoryClassName
        {
            get { return inventoryClassName; }
        }

        private void OnSlotModifid(int slot)
        {
            api.World.BlockAccessor.GetChunkAtBlockPos(pos)?.MarkModified();
        }

        protected virtual void OnInvOpened(IPlayer player)
        {
            inventory.PutLocked = player.WorldData.CurrentGameMode != EnumGameMode.Creative;
        }

        protected virtual void OnInvClosed(IPlayer player)
        {
            invDialog?.Dispose();
            invDialog = null;
        }

        public override void Initialize(ICoreAPI api)
        {
            myBlock = api.World.BlockAccessor.GetBlock(pos);
            InitInventory(myBlock);

            base.Initialize(api);

            if (api is ICoreServerAPI)
            {
                RegisterGameTickListener(MoveItem, 200);
            }
        }

        /// <summary>
        /// Attempts to move the item from slot to slot.
        /// </summary>
        /// <param name="dt"></param>
        public void MoveItem(float dt)
        {
            //check above.  Then check below.  
            BlockPos InputPosition = pos.AddCopy(InputFace);
            BlockPos OutputPosition = pos.AddCopy(OutputFace);

            //if inventory below, attempt to move item in me to below
            if(api.World.BlockAccessor.GetBlockEntity(OutputPosition) is BlockEntityContainer && !inventory.IsEmpty)
            {
                BlockEntityContainer outputBox = (BlockEntityContainer)api.World.BlockAccessor.GetBlockEntity(OutputPosition);
                ItemSlot transferSlot = null;
                foreach(ItemSlot slot in inventory)
                {
                    if(!slot.Empty)
                    {
                        transferSlot = slot;
                        break;
                    }
                }

                if(transferSlot != null)
                {
                    WeightedSlot ws = outputBox.Inventory.GetBestSuitedSlot(transferSlot);
                    if (ws.slot != null)
                    {
                        ItemStackMoveOperation op = new ItemStackMoveOperation(api.World, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, FlowAmount);
                        if (transferSlot.TryPutInto(ws.slot, ref op) > 0)
                        {
                            if (api.World.Rand.NextDouble() < 0.2)
                            {
                                api.World.PlaySoundAt(new AssetLocation("sounds/block/hoppertumble"), pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, null, true, 8, 0.5f);
                            }
                        }
                    }


                }//transfer slot

            }//Output move.

            //if inventory above, attempt to move item from above into me.  (LATER ON: CHECK FILTER)
            if(api.World.BlockAccessor.GetBlockEntity(InputPosition) is BlockEntityContainer)
            {
                BlockEntityContainer inputBox = (BlockEntityContainer)api.World.BlockAccessor.GetBlockEntity(InputPosition);
                if(inputBox.Inventory is InventoryGeneric)
                {
                    InventoryGeneric inputInventory = (InventoryGeneric)inputBox.Inventory;
                    if(!inputInventory.IsEmpty)
                    {
                        ItemSlot transferSlot = null;
                        foreach (ItemSlot slot in inputInventory)
                        {
                            if(!slot.Empty)
                            {
                                transferSlot = slot;
                            }
                        }

                        if(transferSlot != null)
                        {
                            WeightedSlot ws = inventory.GetBestSuitedSlot(transferSlot);
                            if (ws.slot != null)
                            {
                                ItemStackMoveOperation op = new ItemStackMoveOperation(api.World, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, FlowAmount);

                                if (transferSlot.TryPutInto(ws.slot, ref op) > 0)
                                {
                                    if (api.World.Rand.NextDouble() < 0.2)
                                    {
                                        api.World.PlaySoundAt(new AssetLocation("sounds/block/hoppertumble"), pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, null, true, 8, 0.5f);
                                    }
                                }
                            }
                        } //transfer slot

                    }//Inventory empty check

                }//Inventory Generic check.
            }//Check for Block entity container.       
        }



        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if(api.World is IServerWorldAccessor)
            {
                byte[] data;

                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    writer.Write("BlockEntityItemFlow");
                    writer.Write(Lang.Get(ItemFlowObjectLangCode));
                    writer.Write((byte)4); //No idea what this is for but it's on the generic container so...
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


        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            InitInventory(null);

            base.FromTreeAtributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }


    }
}
