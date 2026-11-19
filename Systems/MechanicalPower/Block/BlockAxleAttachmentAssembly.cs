using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockAxleAttachmentAssembly : BlockAxle
    {
        public BlockAxleAttachment FrameBlock { get; private set; } = null!;

        public static string GetAxleRotation(BlockFacing side) => side.GetCW().Axis == EnumAxis.X ? "we" : "ns";

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            Block frame = api.World.GetBlock(AssetLocation.Create(Attributes["frameBlockCode"].AsString(), Code.Domain))
                          ?? throw new InvalidOperationException($"Axle attachment assembly {Code} has an unknown frameBlockCode");
            FrameBlock = api.World.GetBlock(frame.CodeWithVariant("side", Variant["side"])) as BlockAxleAttachment
                         ?? throw new InvalidOperationException($"Axle attachment assembly {Code} has no frame block for side {Variant["side"]}");
        }

        // Keeps the previous block entity state, such as hosted gears or the frame setting.
        public bool TryPlaceAt(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, ItemStack axleStack, ItemStack frameStack, ref string failureCode)
        {
            if (!world.Claims.TryAccess(byPlayer, pos, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                failureCode = "claimed";
                return false;
            }

            IBlockAccessor blockAccessor = world.BlockAccessor;
            BlockEntity? previousEntity = blockAccessor.GetBlockEntity(pos);
            if (previousEntity?.GetBehavior<BlockEntityBehaviorCoverable>()?.WallStack != null)
            {
                failureCode = "notenoughspace";
                return false;
            }

            BEBehaviorMPAxle? axle = previousEntity?.GetBehavior<BEBehaviorMPAxle>();
            if (axle != null)
            {
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    if (axle.HasSpurGear(face) && !CanHostSpurGear(face, axle.GetHostedSpurRadius(face)))
                    {
                        failureCode = "notenoughspace";
                        return false;
                    }
                }
            }

            if (world.GetIntersectingEntities(pos, GetCollisionBoxes(blockAccessor, pos), e => e.IsInteractable).Length != 0)
            {
                failureCode = "entityintersecting";
                return false;
            }

            TreeAttribute state = new();
            previousEntity?.ToTreeAttributes(state);

            blockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            // Restore the state before placing so the new axle stack replaces any copied one.
            be.FromTreeAttributes(state, world);
            be.OnBlockPlaced(axleStack);
            be.GetBehavior<BEBehaviorAxleAttachmentFrame>().SetFrameStack(frameStack);
            WasPlaced(world, pos, null);
            return true;
        }

        public virtual bool CanHostSpurGear(BlockFacing face, float radius)
        {
            Cuboidf gearBox = BlockSpurGear.GetCollisionBox(face, radius);
            foreach (Cuboidf frameBox in FrameBlock.CollisionBoxes)
            {
                if (gearBox.Intersects(frameBox, 0, 0, 0)) return false;
            }
            return true;
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing side = BlockFacing.FromCode(Variant["side"]);
            BlockFacing rotated = BlockFacing.HORIZONTALS_ANGLEORDER[GameMath.Mod(side.HorizontalAngleIndex - angle / 90, 4)];
            return CodeWithVariants(["side", "rotation"], [rotated.Code, GetAxleRotation(rotated)]);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing side = BlockFacing.FromCode(Variant["side"]);
            return side.Axis == axis ? CodeWithVariant("side", side.Opposite.Code) : Code;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return [..base.GetSelectionBoxes(blockAccessor, pos), ..FrameBlock.SelectionBoxes];
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return [..base.GetCollisionBoxes(blockAccessor, pos), ..FrameBlock.CollisionBoxes];
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetBEBehavior<BEBehaviorAxleAttachmentFrame>(pos)?.GetFrameStack() ?? base.OnPickBlock(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            ItemStack[] drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
            ItemStack? frameStack = GetBEBehavior<BEBehaviorAxleAttachmentFrame>(pos)?.GetFrameStack();
            return frameStack == null ? drops : [..drops, frameStack];
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            Block? heldBlock = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Block;
            if (heldBlock is BlockSpurGear or BlockAngledGears) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            return FrameBlock.OnFrameInteract(world, byPlayer, blockSel.Position);
        }
    }
}
