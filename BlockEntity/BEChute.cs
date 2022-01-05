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

            base.Initialize(api);
        }
    }
}
