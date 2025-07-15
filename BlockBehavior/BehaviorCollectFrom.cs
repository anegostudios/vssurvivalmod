using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// When this block is right clicked, harvest its drops and replace it with an empty version.
    /// Uses the code "CollectFrom". This behavior has no properties.
    /// 
    /// Note that this behavior requires a variant which ends in "-empty", and also requires at least two drops. The first drop should be the item to collect, the second drop is the block itself. See 'henbox.json' for an example.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "CollectFrom"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BehaviorCollectFrom : BlockBehavior
    {
        

        public BehaviorCollectFrom(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }
            if (!block.Code.Path.Contains("empty"))
            {
                handling = EnumHandling.PreventDefault;

                //handles the collection of items, and the transformation of the block.
                world.Logger.VerboseDebug("Collecting item(s) from target block at {0}.", blockSel.Position);

                if (block.Drops != null && block.Drops.Length > 1)
                {
                    BlockDropItemStack drop = block.Drops[0];
                    ItemStack stack = drop.GetNextItemStack();

                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                    {
                        world.SpawnItemEntity(drop.GetNextItemStack(), blockSel.Position);
                    }

                    AssetLocation loc = block.Code.CopyWithPath(block.Code.Path.Replace(block.Code.Path.Split('-').Last(), "empty"));

                    world.BlockAccessor.SetBlock(world.GetBlock(loc).BlockId, blockSel.Position);

                    world.PlaySoundAt(new AssetLocation("sounds/player/collect"), blockSel.Position, 0, byPlayer);
                }

                return true;
            }

            return false;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (blockSel == null) return false;

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
            return true;
        }

    }
}
