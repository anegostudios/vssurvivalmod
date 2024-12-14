using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorContainer : BlockBehavior
    {
        public BlockBehaviorContainer(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventSubsequent;

            BlockEntity entity = world.BlockAccessor.GetBlockEntity(blockSel.Position);

            if (entity is BlockEntityOpenableContainer)
            {
                BlockEntityOpenableContainer beContainer = (BlockEntityOpenableContainer)entity;
                return beContainer.OnPlayerRightClick(byPlayer, blockSel);
            }

            return false;
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;
        
            BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);

            if (entity is BlockEntityOpenableContainer)
            {
                BlockEntityOpenableContainer container = (BlockEntityOpenableContainer)entity;

                IPlayer[] players = world.AllOnlinePlayers;
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i].InventoryManager.HasInventory(container.Inventory))
                    {
                        players[i].InventoryManager.CloseInventory(container.Inventory);
                    }
                }
            }
        }
    }
}
