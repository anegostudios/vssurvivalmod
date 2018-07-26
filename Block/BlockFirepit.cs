using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFirepit : Block
    {
        public int Stage { get {
            switch (LastCodePart())
                {
                    case "construct1":
                        return 1;
                    case "construct2":
                        return 2;
                    case "construct3":
                        return 3;
                    case "construct4":
                        return 4;
                }
                return 5;
        } }

        public string NextStageCodePart
        {
            get
            {
                switch (LastCodePart())
                {
                    case "construct1":
                        return "construct2";
                    case "construct2":
                        return "construct3";
                    case "construct3":
                        return "construct4";
                    case "construct4":
                        return "lit";
                }
                return "lit";
            }
        }


        public virtual void Ignite(IWorldAccessor world, BlockPos pos)
        {
            if (LastCodePart() == "lit") return;
            Block litblock = world.GetBlock(CodeWithParts("lit"));
            if (litblock == null) return;

            world.BlockAccessor.ExchangeBlock(litblock.BlockId, pos);
            // world.Logger.Notification("light");
        }


        public void Extinguish(IWorldAccessor world, BlockPos pos)
        {
            if (LastCodePart() == "extinct") return;
            Block litblock = world.GetBlock(CodeWithParts("extinct"));
            if (litblock == null) return;

            world.BlockAccessor.ExchangeBlock(litblock.BlockId, pos);
            //world.Logger.Notification("exti");
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            int stage = Stage;
            ItemStack stack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;

            if (stage == 5)
            {
                BlockEntityFirepit bef = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFirepit;
                
                if (bef != null && stack != null && byPlayer.Entity.Controls.Sneak)
                {
                    if (stack.Collectible.CombustibleProps != null && stack.Collectible.CombustibleProps.MeltingPoint > 0)
                    {
                        ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Button1, 0, EnumMergePriority.DirectMerge, 1);
                        byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(bef.inputSlot, ref op);
                        if (op.MovedQuantity > 0) return true;
                    }

                    if (stack.Collectible.CombustibleProps != null && stack.Collectible.CombustibleProps.BurnTemperature > 0)
                    {
                        ItemStackMoveOperation op = new ItemStackMoveOperation(world, EnumMouseButton.Button1, 0, EnumMergePriority.DirectMerge, 1);
                        byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(bef.fuelSlot, ref op);
                        if (op.MovedQuantity > 0) return true;
                    }
                }

                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }


            if (stack != null && TryConstruct(world, blockSel.Position, stack.Collectible))
            {
                if (byPlayer != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                }
                return true;
            }


            return false;
        }

        public bool TryConstruct(IWorldAccessor world, BlockPos pos, CollectibleObject obj) {
            int stage = Stage;

            if (obj.Attributes?["firepitConstructable"]?.AsBool(false) != true) return false;

            CombustibleProperties combprops = obj.CombustibleProps;

            if (stage == 5) return false;

            if (stage == 4 && world.BlockAccessor.GetBlock(pos.DownCopy()).Code.Path.Equals("firewoodpile"))
            {
                Block charcoalPitBlock = world.GetBlock(new AssetLocation("charcoalpit"));
                if (charcoalPitBlock != null)
                {
                    world.BlockAccessor.SetBlock(charcoalPitBlock.BlockId, pos);
                    return true;
                }
            }

            Block block = world.GetBlock(CodeWithParts(NextStageCodePart));
            world.BlockAccessor.ExchangeBlock(block.BlockId, pos);
            world.BlockAccessor.MarkBlockDirty(pos);
            if (block.Sounds != null) world.PlaySoundAt(block.Sounds.Place, pos.X, pos.Y, pos.Z);

            if (stage == 4)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                if (be is BlockEntityFirepit)
                {
                    ((BlockEntityFirepit)be).igniteWithFuel(combprops, 4);
                }
            }

            return true;
        }
    }
}
