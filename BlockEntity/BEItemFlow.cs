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
        public string ItemFlowObjectLangCode = "hopper-contents";
        public int QuantitySlots = 4;
        public int FlowAmount = 1;


        public override AssetLocation OpenSound => new AssetLocation("sounds/block/hopperopen");
        public override AssetLocation CloseSound => null;


        public BlockEntityItemFlow() : base()
        {

        }

        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        private void InitInventory()
        {
            if(Block?.Attributes != null)
            {
                if (Block.Attributes["input-face"].Exists)
                {
                    InputFace = BlockFacing.FromCode(Block.Attributes["input-face"].AsString(null));
                }
                OutputFace = BlockFacing.FromCode(Block.Attributes["output-face"].AsString(OutputFace.Code));
                FlowAmount = Block.Attributes["item-flowrate"].AsInt(FlowAmount);
                inventoryClassName = Block.Attributes["inventoryClassName"].AsString(inventoryClassName);
                ItemFlowObjectLangCode = Block.Attributes["itemFlowObjectLangCode"].AsString(ItemFlowObjectLangCode);
                QuantitySlots = Block.Attributes["quantitySlots"].AsInt(QuantitySlots);
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
            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        }

        protected virtual void OnInvOpened(IPlayer player)
        {
            inventory.PutLocked = false;// player.WorldData.CurrentGameMode != EnumGameMode.Creative;
        }

        protected virtual void OnInvClosed(IPlayer player)
        {
            invDialog?.Dispose();
            invDialog = null;
        }

        public override void Initialize(ICoreAPI api)
        {
            InitInventory();

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
            BlockPos OutputPosition = Pos.AddCopy(OutputFace);

            // If inventory below, attempt to move item in me to below
            if (!inventory.IsEmpty)
            {
                if (Api.World.BlockAccessor.GetBlockEntity(OutputPosition) is BlockEntityContainer)
                {
                    BlockEntityContainer outputBox = (BlockEntityContainer)Api.World.BlockAccessor.GetBlockEntity(OutputPosition);
                    ItemSlot transferSlot = null;
                    foreach (ItemSlot slot in inventory)
                    {
                        if (!slot.Empty)
                        {
                            transferSlot = slot;
                            break;
                        }
                    }

                    if (transferSlot != null)
                    {
                        WeightedSlot ws = outputBox.Inventory.GetBestSuitedSlot(transferSlot);
                        if (ws.slot != null)
                        {
                            ItemStackMoveOperation op = new ItemStackMoveOperation(Api.World, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, FlowAmount);
                            if (transferSlot.TryPutInto(ws.slot, ref op) > 0)
                            {
                                if (Api.World.Rand.NextDouble() < 0.2)
                                {
                                    Api.World.PlaySoundAt(new AssetLocation("sounds/block/hoppertumble"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, true, 8, 0.5f);
                                }
                            }
                        }


                    }//transfer slot

                } else
                {



                }
            }


            // If inventory above, attempt to move item from above into me.  (LATER ON: CHECK FILTER)
            if (InputFace != null)
            {
                BlockPos InputPosition = Pos.AddCopy(InputFace);
                if (Api.World.BlockAccessor.GetBlockEntity(InputPosition) is BlockEntityContainer)
                {
                    BlockEntityContainer inputBox = (BlockEntityContainer)Api.World.BlockAccessor.GetBlockEntity(InputPosition);
                    if (inputBox.Inventory is InventoryGeneric)
                    {
                        InventoryGeneric inputInventory = (InventoryGeneric)inputBox.Inventory;
                        if (!inputInventory.IsEmpty)
                        {
                            ItemSlot transferSlot = null;
                            foreach (ItemSlot slot in inputInventory)
                            {
                                if (!slot.Empty)
                                {
                                    transferSlot = slot;
                                }
                            }

                            if (transferSlot != null)
                            {
                                WeightedSlot ws = inventory.GetBestSuitedSlot(transferSlot);
                                if (ws.slot != null)
                                {
                                    ItemStackMoveOperation op = new ItemStackMoveOperation(Api.World, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, FlowAmount);

                                    if (transferSlot.TryPutInto(ws.slot, ref op) > 0)
                                    {
                                        if (Api.World.Rand.NextDouble() < 0.2)
                                        {
                                            Api.World.PlaySoundAt(new AssetLocation("sounds/block/hoppertumble"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, true, 8, 0.5f);
                                        }
                                    }
                                }
                            } //transfer slot

                        }//Inventory empty check

                    }//Inventory Generic check.
                }//Check for Block entity container.       
            }
        }



        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if(Api.World is IServerWorldAccessor)
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

                    ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    Pos.X, Pos.Y, Pos.Z,
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
            InitInventory();

            base.FromTreeAtributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }


    }
}
