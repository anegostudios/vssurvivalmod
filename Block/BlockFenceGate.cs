using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFenceGate : BaseDoor
    {
        public override string GetKnobOrientation()
        {
            return GetKnobOrientation(this);
        }

        public override BlockFacing GetDirection()
        {
            string[] parts = Code.Path.Split('-');
            return BlockFacing.FromFirstLetter(parts[2]);
        }

        public string GetKnobOrientation(Block block)
        {
            string[] parts = block.Code.Path.Split('-');
            return parts[parts.Length - 1];
        }

        public bool IsOpened()
        {
            return LastCodePart(1) == "opened";
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (!world.TryAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

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

            bool isDoor1 = IsSameDoor(nBlock1);
            bool isDoor2 = IsSameDoor(nBlock2);
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

        protected override void Open(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        {
            AssetLocation newCode = CodeWithParts(IsOpened() ? "closed" : "opened", GetKnobOrientation());

            world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(newCode).BlockId, pos);
        }

        protected override BlockPos TryGetConnectedDoorPos(BlockPos pos)
        {
            string knob = GetKnobOrientation();
            BlockFacing dir = GetDirection();
            return knob == "right" ? pos.AddCopy(dir.GetCW()) : pos.AddCopy(dir.GetCCW());
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
