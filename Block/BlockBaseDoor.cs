using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    abstract public class BlockBaseDoor : Block
    {
        public abstract string GetKnobOrientation();
        public abstract BlockFacing GetDirection();
        protected abstract BlockPos TryGetConnectedDoorPos(BlockPos pos);
        protected abstract void Open(IWorldAccessor world, IPlayer byPlayer, BlockPos position);

        public bool IsSameDoor(Block block)
        {
            string[] parts = Code.Path.Split('-');
            string[] otherParts = block.Code.Path.Split('-');
            return parts[0] == otherParts[0];
        }

        public bool DoesBehaviorAllow(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            bool preventDefault = false;

            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;
                bool behaviorResult = behavior.OnBlockInteractStart(world, byPlayer, blockSel, ref handled);
                if (handled != EnumHandling.PassThrough)
                {
                    preventDefault = true;
                }

                if (handled == EnumHandling.PreventSubsequent) return false;
            }
            if (preventDefault) return false;

            
            if (this is BlockDoor)
            {
                blockSel = blockSel.Clone();
                blockSel.Position = (this as BlockDoor).IsUpperHalf() ? blockSel.Position.DownCopy() : blockSel.Position.UpCopy();

                foreach (BlockBehavior behavior in BlockBehaviors)
                {
                    EnumHandling handled = EnumHandling.PassThrough;
                    bool behaviorResult = behavior.OnBlockInteractStart(world, byPlayer, blockSel, ref handled);
                    if (handled != EnumHandling.PassThrough)
                    {
                        preventDefault = true;
                    }

                    if (handled == EnumHandling.PreventSubsequent) return false;
                }

                if (preventDefault) return false;
            }

            return true;
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!DoesBehaviorAllow(world, byPlayer, blockSel)) return true;

            BlockPos pos = blockSel.Position;
            Open(world, byPlayer, pos);

            world.PlaySoundAt(new AssetLocation("sounds/block/door"), pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f, byPlayer);

            TryOpenConnectedDoor(world, byPlayer, pos);
            return true;
        }

        protected void TryOpenConnectedDoor(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        {
            BlockPos door2Pos = TryGetConnectedDoorPos(pos);
            if (door2Pos != null)
            {
                Block nBlock1 = world.BlockAccessor.GetBlock(pos);
                Block nBlock2 = world.BlockAccessor.GetBlock(door2Pos);

                bool isDoor1 = IsSameDoor(nBlock1);
                bool isDoor2 = IsSameDoor(nBlock2);
                if (isDoor1 && isDoor2)
                {
                    if(nBlock2 is BlockBaseDoor)
                    {
                        BlockBaseDoor door2 = (BlockBaseDoor)nBlock2;
                        door2.Open(world, byPlayer, door2Pos);
                    }
                }
            }
        }



        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-door-openclose",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
