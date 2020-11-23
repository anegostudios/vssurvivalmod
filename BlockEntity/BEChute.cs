using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    class BlockEntityChute : BlockEntityOpenableContainer
    {
        InventoryGeneric inventory;

        bool[] AttachmentMask; //this is taken care of on placement of the block.
        bool[] DirectionalMask; //Only up and down is checked on placement.

        public override InventoryBase Inventory => inventory;

        public override string InventoryClassName => "chuteContents";

        public override AssetLocation OpenSound => new AssetLocation("sounds/block/hopperopen");
        public override AssetLocation CloseSound => null;

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            return false;
        }

        private void InitInventory()
        {
            if(inventory == null)
            {
                inventory = new InventoryGeneric(1, null, null, null);

                inventory.OnInventoryClosed += OnInventoryClosed;
                inventory.OnInventoryOpened += OnInvOpened;
                inventory.SlotModified += OnSlotModified;
            }
        }

        private void OnSlotModified(int slot)
        {
            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        }

        private void OnInvOpened(IPlayer player)
        {
            inventory.PutLocked = player.WorldData.CurrentGameMode != EnumGameMode.Creative;
        }

        private void OnInventoryClosed(IPlayer player)
        {
            invDialog?.Dispose();
            invDialog = null;
        }

        public override void Initialize(ICoreAPI api)
        {
            InitInventory();

            DirectionalMask = new bool[6]; //N, S, E, W, U, D - by default: input is false.
            AttachmentMask = new bool[6]; //see above, false = not connected.

            base.Initialize(api);

            if(api is ICoreServerAPI)
            {
                RegisterGameTickListener(UpdateChuteInfo, 200);
            }

        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            UpdateTube(0f);
            
        }

        private void UpdateChuteInfo(float dt)
        {
            UpdateTube(dt);

            MoveItem(dt);
        }

        private void UpdateTube(float dt)
        {
            //setup the north mask
            BlockEntityContainer check = Api.World.BlockAccessor.GetBlockEntity(Pos.NorthCopy()) as BlockEntityContainer;
            if (check != null)
            {
                AttachmentMask[0] = check.Inventory is InventoryGeneric;
            }

            //setup the attachment mask
            check = Api.World.BlockAccessor.GetBlockEntity(Pos.SouthCopy()) as BlockEntityContainer;
            if (check != null)
            {
                AttachmentMask[1] = check.Inventory is InventoryGeneric;
            }

            //setup the attachment mask
            check = Api.World.BlockAccessor.GetBlockEntity(Pos.EastCopy()) as BlockEntityContainer;
            if (check != null)
            {
                AttachmentMask[2] = check.Inventory is InventoryGeneric;
            }

            //setup the attachment mask
            check = Api.World.BlockAccessor.GetBlockEntity(Pos.WestCopy()) as BlockEntityContainer;
            if (check != null)
            {
                AttachmentMask[3] = check.Inventory is InventoryGeneric;
            }

            //setup the attachment mask
            check = Api.World.BlockAccessor.GetBlockEntity(Pos.UpCopy()) as BlockEntityContainer;
            if (check != null)
            {
                AttachmentMask[4] = check.Inventory is InventoryGeneric;
            }

            //setup the attachment mask
            check = Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy()) as BlockEntityContainer;
            if (check != null)
            {
                AttachmentMask[5] = check.Inventory is InventoryGeneric;
            }

            //Elbow default is West and Up
            //3way default is West, South and Up
            //4way default is west, east, south and Up
            //5 way default is NOT down.
            //6 way is yes.
            //cross default is up east down west
            //straight default is east-west
            //T default is East, west, up

            //orient from west as the origin (-x)


        }

        private void MoveItem(float dt)
        {
            for (int d = 0; d < 6; d++)
            {
                if(inventory.IsEmpty)
                {
                    break; //don't need to check for output if we don't have anything to output.
                }
                if (DirectionalMask[d] && d != 4) //means we're dealing with an output face if this is true.  Also no going back up.
                {

                } //Checking for Up
            } //

            if(DirectionalMask[4])
            {
                //pull.
                BlockEntity target = Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(BlockFacing.UP));

            } //checking for UP

        }
    }
}
