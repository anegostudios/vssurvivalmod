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
        protected string type;
        protected bool open;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.type = string.Intern(Code.Path.Substring(0, Code.Path.IndexOf('-')));
            this.open = Variant["state"] == "opened";
        }

        public bool IsSameDoor(Block block)
        {
            return (block is BlockBaseDoor otherDoor) && otherDoor.type == this.type;
        }

        public virtual bool IsOpened()
        {
            return open;
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

            world.PlaySoundAt(AssetLocation.Create(Attributes["triggerSound"].AsString("sounds/block/door"), Code.Domain), pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f, byPlayer);

            bool isRoughFence = this.FirstCodePart() == "roughhewnfencegate";
            if (!isRoughFence) TryOpenConnectedDoor(world, byPlayer, pos);

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }

        protected void TryOpenConnectedDoor(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        {
            BlockPos door2Pos = TryGetConnectedDoorPos(pos);
            if (door2Pos != null)
            {
                if (world.BlockAccessor.GetBlock(door2Pos) is BlockBaseDoor door2 && IsSameDoor(door2) && pos == door2.TryGetConnectedDoorPos(door2Pos))
                {
                    door2.Open(world, byPlayer, door2Pos);
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
