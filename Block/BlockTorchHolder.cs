using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockTorchHolder : Block
    {
        public bool Empty
        {
            get { return Variant["state"] == "empty"; }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Empty)
            {
                ItemStack heldStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
                if (heldStack != null && heldStack.Collectible.Code.Path.Equals("torch-up"))
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

                    Block filledBlock = world.GetBlock(CodeWithVariant("state", "filled"));
                    world.BlockAccessor.SetBlock(filledBlock.BlockId, blockSel.Position);

                    if (Sounds?.Place != null)
                    {
                        world.PlaySoundAt(Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    }

                    return true;
                }
            } else
            {
                ItemStack stack = new ItemStack(world.GetBlock(new AssetLocation("torch-up")));
                if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                {
                    Block filledBlock = world.GetBlock(CodeWithVariant("state", "empty"));
                    world.BlockAccessor.SetBlock(filledBlock.BlockId, blockSel.Position);

                    if (Sounds?.Place != null)
                    {
                        world.PlaySoundAt(Sounds.Place, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    }

                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-torchholder-addtorch",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = new ItemStack[] { new ItemStack(world.GetBlock(new AssetLocation("torch-up"))) }
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
