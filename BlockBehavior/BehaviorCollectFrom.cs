using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
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
                        world.SpawnItemEntity(drop.GetNextItemStack(), blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                    }

                    AssetLocation loc = block.Code.CopyWithPath(block.Code.Path.Replace(block.Code.Path.Split('-').Last(), "empty"));

                    world.BlockAccessor.SetBlock(world.GetBlock(loc).BlockId, blockSel.Position);

                    world.PlaySoundAt(new AssetLocation("sounds/player/collect"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
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
