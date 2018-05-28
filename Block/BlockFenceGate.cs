using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFenceGate : Block
    {
        public string GetKnobOrientation()
        {
            return GetKnobOrientation(this);
        }
        public string GetKnobOrientation(Block block)
        {
            string[] parts = block.Code.Path.Split('-');
            return parts[parts.Length - 1];
        }
        public bool IsFenceGate(Block block)
        {
            string[] parts = Code.Path.Split('-');
            string[] otherParts = block.Code.Path.Split('-');
            return parts[0] == otherParts[0];
        }

        public bool IsOpened()
        {
            return LastCodePart(1) == "opened";
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (IsSuitablePosition(world, blockSel.Position))
            {
                BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);

                string face = (horVer[0] == BlockFacing.NORTH || horVer[0] == BlockFacing.SOUTH) ? "n" : "w";

                string knobOrientation = GetSuggestedKnobOrientation(world.BlockAccessor, blockSel.Position, horVer[0]);
                AssetLocation newCode = CodeWithParts(face, "closed", knobOrientation);

                world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(newCode).BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        private string GetSuggestedKnobOrientation(IBlockAccessor ba, BlockPos pos, BlockFacing facing)
        {
            string leftOrRight = "left";

            Block nBlock1 = ba.GetBlock(pos.AddCopy(facing.GetCCW()));
            Block nBlock2 = ba.GetBlock(pos.AddCopy(facing.GetCW()));

            bool isDoor1 = IsFenceGate(nBlock1);
            bool isDoor2 = IsFenceGate(nBlock2);
            if (isDoor1 && isDoor2)
            {
                leftOrRight = "left";
            }
            else
            {
                if (isDoor1)
                {
                    leftOrRight = GetKnobOrientation(nBlock1) == "right" ? "left" : "right";
                }
                else if (isDoor2)
                {
                    leftOrRight = GetKnobOrientation(nBlock2) == "right" ? "left" : "right";
                }
            }
            return leftOrRight;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            AssetLocation newCode = CodeWithParts(IsOpened() ? "closed" : "opened", GetKnobOrientation());

            world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(newCode).BlockId, blockSel.Position);

            world.PlaySoundAt(new AssetLocation("sounds/block/door"), blockSel.Position.X + 0.5f, blockSel.Position.Y + 0.5f, blockSel.Position.Z + 0.5f, byPlayer);

            return true;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithPath(CodeWithoutParts(3) + "-n-closed-left"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithPath(CodeWithoutParts(3) + "-n-closed-left"));
            return new ItemStack(block);
        }



        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            string[] angles = { "w", "n" };
            int index = angle / 90;
            if (LastCodePart() == "n") index++;

            return CodeWithParts(angles[index % 2], IsOpened() ? "opened" : "closed", GetKnobOrientation());
        }
    }
}
