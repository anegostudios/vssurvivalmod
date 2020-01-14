using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockHelveHammer : Block
    {

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEHelveHammer beh = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEHelveHammer;

            if (beh != null && !beh.HasHammer && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.Code.Path.Equals("helvehammer"))
                {
                    beh.HasHammer = true;
                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                    }
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    api.World.PlaySoundAt(new AssetLocation("sounds/player/build"), blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5, null, 0.88f + (float)api.World.Rand.NextDouble() * 0.24f, 16);
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

    }
}
