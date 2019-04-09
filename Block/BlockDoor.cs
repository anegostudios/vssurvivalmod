using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    class BlockDoor : BaseDoor
    {
        public override string GetKnobOrientation()
        {
            return GetKnobOrientation(this);
        }

        public override BlockFacing GetDirection()
        {
            string[] parts = Code.Path.Split('-');
            return BlockFacing.FromCode(parts[1]);
        }

        public string GetKnobOrientation(Block block)
        {
            string[] parts = block.Code.Path.Split('-');
            return parts[parts.Length - 1];
        }

        public bool IsUpperHalf()
        {
            string[] parts = Code.Path.Split('-');
            return parts[parts.Length - 3] == "up";
        }

        public bool IsOpened()
        {
            string[] parts = Code.Path.Split('-');
            return parts[parts.Length - 2] == "opened";
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                failureCode = "claimed";
                return false;
            }

            BlockPos abovePos = blockSel.Position.AddCopy(0, 1, 0);
            IBlockAccessor ba = world.BlockAccessor;
            
            if (ba.GetBlockId(abovePos) == 0 && IsSuitablePosition(world, blockSel.Position, ref failureCode))
            {
                BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);

                string knobOrientation = GetSuggestedKnobOrientation(ba, blockSel.Position, horVer[0]);

                AssetLocation downBlockCode = CodeWithParts(horVer[0].Code, "down", "closed", knobOrientation);
                Block downBlock = ba.GetBlock(downBlockCode);

                AssetLocation upBlockCode = CodeWithParts(horVer[0].Code, "up", "closed", knobOrientation);
                Block upBlock = ba.GetBlock(upBlockCode);

                ba.SetBlock(downBlock.BlockId, blockSel.Position);
                ba.SetBlock(upBlock.BlockId, abovePos);
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

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            BlockPos otherPos = pos.AddCopy(0, IsUpperHalf() ? -1 : 1, 0);
            Block otherPart = world.BlockAccessor.GetBlock(otherPos);

            if (otherPart is BlockDoor && ((BlockDoor)otherPart).IsUpperHalf() != IsUpperHalf())
            {
                world.BlockAccessor.SetBlock(0, otherPos);
            }

            Block block = world.BlockAccessor.GetBlock(pos);
            if (block is BlockDoor)
            {
                world.BlockAccessor.SetBlock(0, pos);
            }
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("north", "down", "closed", "left"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("north", "down", "closed", "left"));
            return new ItemStack(block);
        }

        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighourBlockChange(world, pos, neibpos);
        }

        protected override void Open(IWorldAccessor world, IPlayer byPlayer, BlockPos position)
        {
            AssetLocation newCode = CodeWithParts(IsUpperHalf() ? "up" : "down", IsOpened() ? "closed" : "opened", GetKnobOrientation());
            AssetLocation otherNewCode = CodeWithParts(IsUpperHalf() ? "down" : "up", IsOpened() ? "closed" : "opened", GetKnobOrientation());

            BlockPos otherPos = position.AddCopy(0, IsUpperHalf() ? -1 : 1, 0);
            Block otherPart = world.BlockAccessor.GetBlock(otherPos);

            Block newBlock = world.BlockAccessor.GetBlock(newCode);
            world.BlockAccessor.ExchangeBlock(newBlock.BlockId, position);

            if (otherPart is BlockDoor && ((BlockDoor)otherPart).IsUpperHalf() != IsUpperHalf())
            {
                world.BlockAccessor.ExchangeBlock(world.BlockAccessor.GetBlock(otherNewCode).BlockId, otherPos);
            }
        }

        protected override BlockPos TryGetConnectedDoorPos(BlockPos pos)
        {
            string knob = GetKnobOrientation();
            BlockFacing dir = GetDirection();
            return knob == "left" ? pos.AddCopy(dir.GetCW()) : pos.AddCopy(dir.GetCCW());
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing beforeFacing = BlockFacing.FromCode(LastCodePart(3));
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];

            return CodeWithParts(nowFacing.Code, IsUpperHalf() ? "up" : "down", IsOpened() ? "opened" : "closed", GetKnobOrientation());
        }

    }
}
