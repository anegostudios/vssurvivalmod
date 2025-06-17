using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockFenceGateRoughHewn : BlockBaseDoor
    {
        public override string GetKnobOrientation()
        {
            return "left";
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            CanStep = false;
        }

        public override BlockFacing GetDirection()
        {
            return BlockFacing.FromFirstLetter(Variant["type"]);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);

                string face = (horVer[0] == BlockFacing.NORTH || horVer[0] == BlockFacing.SOUTH) ? "n" : "w";

                AssetLocation newCode = CodeWithVariants(new string[] { "type", "state" }, new string[] { face, "closed" });

                world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(newCode).BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        protected override void Open(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        {
            AssetLocation newCode = CodeWithVariant("state", IsOpened() ? "closed" : "opened");

            world.BlockAccessor.ExchangeBlock(world.BlockAccessor.GetBlock(newCode).BlockId, pos);
        }

        protected override BlockPos TryGetConnectedDoorPos(BlockPos pos)
        {
            BlockFacing dir = GetDirection();
            return pos.AddCopy(dir.GetCW());
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariants(new string[] { "type", "state", "cover" }, new string[] { "n", "closed", "free" }));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariants(new string[] { "type", "state", "cover" }, new string[] { "n", "closed", "free" }));
            return new ItemStack(block);
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing nowFacing = BlockFacing.FromFirstLetter(Variant["type"]);
            BlockFacing rotatedFacing = BlockFacing.HORIZONTALS_ANGLEORDER[(nowFacing.HorizontalAngleIndex + angle / 90) % 4];

            string type = Variant["type"];

            if (nowFacing.Axis != rotatedFacing.Axis)
            {
                type = (type == "n" ? "w" : "n");
            }

            return CodeWithVariant("type", type);
        }

        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs)
        {
            if (activationArgs != null && activationArgs.HasAttribute("opened"))
            {
                if (activationArgs.GetBool("opened") == IsOpened()) return;   // do nothing if already in the required state
            }
            Open(world, caller.Player, blockSel.Position);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }
    }
}
