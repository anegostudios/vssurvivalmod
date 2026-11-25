using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockAxleArm : BlockMPConsumer, IWrenchOrientable
    {
        // Linkage pins use the visible peg centers, not the shape element's unused rotation origins.
        public const int CrankHoleCount = 3;
        public const float CrankHoleSpacing = 2f / 16f;
        public const float CrankHorizontalOffset = 0;
        public const float CrankFirstVerticalOffset = 2f / 16f;
        public const float CrankAxialOffset = 0.5f / 16f;

        BlockBehaviorLinkageEndpoint? linkageEndpoint;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            linkageEndpoint = GetBehavior<BlockBehaviorLinkageEndpoint>();
        }

        public static float GetCrankRadius(int hole)
        {
            float verticalOffset = CrankFirstVerticalOffset + hole * CrankHoleSpacing;
            return (float)Math.Sqrt(CrankHorizontalOffset * CrankHorizontalOffset + verticalOffset * verticalOffset);
        }

        public static float GetCrankPhase(int hole, BlockFacing facing)
        {
            float verticalOffset = CrankFirstVerticalOffset + hole * CrankHoleSpacing;
            int facingSign = facing.Normali.X + facing.Normali.Z;
            return facingSign * (float)Math.Atan2(CrankHorizontalOffset, verticalOffset);
        }

        public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            var arm = GetBEBehavior<BEBehaviorMPAxleArm>(blockSel.Position);
            if (arm == null) return;

            if (arm.TrySetCrankHole(GameMath.Mod(arm.CrankHole + dir, CrankHoleCount))) return;

            (api as ICoreClientAPI)?.TriggerIngameError(this, "linkage-remove-before-adjusting", Lang.Get("linkage-remove-before-adjusting"));
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Cuboidf[] boxes = base.GetSelectionBoxes(blockAccessor, pos);
            return linkageEndpoint?.GetSelectionBoxes(pos, boxes) ?? boxes;
        }

        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!BlockAxle.TryGetHorizontalEndPlacement(world, blockSel, out _, out _, out BlockFacing supportFacing, out BlockSelection placement)
                && (supportFacing = blockSel.Face)?.IsHorizontal != true)
            {
                failureCode = "requirehorizontalattachable";
                return false;
            }
            supportFacing = supportFacing.Opposite;

            Block? orientedBlock = world.GetBlock(CodeWithVariant("side", supportFacing.Code));
            if (orientedBlock is not BlockAxleArm axleArm)
            {
                failureCode = "notreplaceable";
                return false;
            }
            if (!axleArm.CanPlaceBlock(world, byPlayer, placement, ref failureCode)) return false;
            if (!axleArm.isSupported(world, placement.Position, supportFacing))
            {
                failureCode = "requirehorizontalattachable";
                return false;
            }
            if (!axleArm.DoPlaceBlock(world, byPlayer, placement, itemstack)) return false;

            axleArm.tryConnect(world, byPlayer, placement.Position, supportFacing);
            return true;
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase? forBlock)
        {
            return HasCompatibleConnector(forBlock) && base.HasMechPowerConnectorAt(world, pos, face, forBlock);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!isSupported(world, pos, Facing))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        bool isSupported(IWorldAccessor world, BlockPos pos, BlockFacing supportFacing)
        {
            IBlockAccessor blockAccessor = world.BlockAccessor;
            BlockPos supportPos = pos.AddCopy(supportFacing);
            if (blockAccessor.GetChunkAtBlockPos(supportPos) == null) return true;

            Block supportBlock = blockAccessor.GetBlock(supportPos);
            if (supportBlock.CanAttachBlockAt(blockAccessor, this, supportPos, supportFacing.Opposite)) return true;
            if (supportBlock is not IMechanicalPowerBlock mechanicalSupport) return false;

            return HasMechPowerConnectorAt(world, pos, supportFacing, supportBlock as BlockMPBase)
                && mechanicalSupport.HasMechPowerConnectorAt(world, supportPos, supportFacing.Opposite, this);
        }
    }
}
