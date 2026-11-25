using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockAxleAttachment : BlockMPBase
    {
        public BlockAxleAttachmentAssembly AssemblyBlock { get; private set; } = null!;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            BlockFacing side = BlockFacing.FromCode(Variant["side"]);
            Block assembly = api.World.GetBlock(AssetLocation.Create(Attributes["assemblyBlockCode"].AsString(), Code.Domain))
                             ?? throw new InvalidOperationException($"Axle attachment {Code} has an unknown assemblyBlockCode");
            AssemblyBlock = api.World.GetBlock(assembly.CodeWithVariants(["side", "rotation"], [side.Code, BlockAxleAttachmentAssembly.GetAxleRotation(side)])) as BlockAxleAttachmentAssembly
                            ?? throw new InvalidOperationException($"Axle attachment {Code} has no assembly block for side {side.Code}");
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock) => false;

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack?.Block is not BlockAxle axle) return OnFrameInteract(world, byPlayer, blockSel.Position);

            string failureCode = "";
            if (!TryInsertAxle(world, byPlayer, slot.Itemstack, axle, blockSel.Position, ref failureCode))
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, failureCode, Lang.Get($"placefailure-{failureCode}"));
            }
            else if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }
            return true;
        }

        // Interaction with the frame itself used by both the frame and its assembly.
        public virtual bool OnFrameInteract(IWorldAccessor world, IPlayer byPlayer, BlockPos pos) => false;

        public virtual bool CanInsertAxle(IWorldAccessor world, BlockPos pos, ref string failureCode) => true;

        public bool TryInsertAxle(IWorldAccessor world, IPlayer byPlayer, ItemStack axleStack, BlockAxle axle, BlockPos pos, ref string failureCode)
        {
            if (!AssemblyBlock.HasCompatibleConnector(axle))
            {
                failureCode = "incompatibleconnector";
                return false;
            }
            if (!CanInsertAxle(world, pos, ref failureCode)) return false;

            return AssemblyBlock.TryPlaceAt(world, byPlayer, pos, axleStack, OnPickBlock(world, pos), ref failureCode);
        }

        public bool TryAttachToAxle(IWorldAccessor world, IPlayer byPlayer, ItemStack frameStack, BlockAxle axle, BlockSelection blockSel, ref string failureCode)
        {
            if (axle.IsOrientedTo(BlockFacing.UP))
            {
                failureCode = "requireshorizontalaxle";
                return false;
            }

            // The frame stands across the axle so a facing along it turns a quarter.
            BlockFacing side = SuggestedHVOrientation(byPlayer, blockSel)[0];
            if (axle.IsOrientedTo(side)) side = side.GetCW();

            BlockAxleAttachment frame = world.GetBlock(CodeWithVariant("side", side.Code)) as BlockAxleAttachment
                                       ?? throw new InvalidOperationException($"Axle attachment {Code} has no frame block for side {side.Code}");
            BlockAxleAttachmentAssembly assembly = frame.AssemblyBlock;
            if (!assembly.HasCompatibleConnector(axle))
            {
                failureCode = "incompatibleconnector";
                return false;
            }

            return assembly.TryPlaceAt(world, byPlayer, blockSel.Position, axle.OnPickBlock(world, blockSel.Position), frameStack, ref failureCode);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetBEBehavior<BEBehaviorAxleAttachmentFrame>(pos)?.GetFrameStack() ?? base.OnPickBlock(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            ItemStack? frameStack = GetBEBehavior<BEBehaviorAxleAttachmentFrame>(pos)?.GetFrameStack();
            return frameStack == null ? base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier) : [frameStack];
        }
    }
}
