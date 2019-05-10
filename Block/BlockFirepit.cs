using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

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


        public virtual bool Ignite(IWorldAccessor world, BlockPos pos)
        {
            if (LastCodePart() == "lit") return false;
            Block litblock = world.GetBlock(CodeWithParts("lit"));
            if (litblock == null) return false;

            world.BlockAccessor.ExchangeBlock(litblock.BlockId, pos);
            // world.Logger.Notification("light");
            return true;
        }


        public virtual bool Extinguish(IWorldAccessor world, BlockPos pos)
        {
            if (LastCodePart() == "extinct") return false;
            Block litblock = world.GetBlock(CodeWithParts("extinct"));
            if (litblock == null) return false;

            world.BlockAccessor.ExchangeBlock(litblock.BlockId, pos);
            //world.Logger.Notification("exti");

            return true;
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

                if (stack?.Collectible is BlockBowl && (stack.Collectible as BlockBowl)?.BowlContentItemCode() == null && stack.Collectible.Attributes?["mealContainer"].AsBool() == true)
                {
                    ItemSlot potSlot = null;
                    if (bef?.inputStack?.Collectible is BlockCookedContainer)
                    {
                        potSlot = bef.inputSlot;
                    }
                    if (bef?.outputStack?.Collectible is BlockCookedContainer)
                    {
                        potSlot = bef.outputSlot;
                    }

                    if (potSlot != null)
                    {
                        BlockCookedContainer blockPot = potSlot.Itemstack.Collectible as BlockCookedContainer;
                        ItemSlot targetSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                        if (byPlayer.InventoryManager.ActiveHotbarSlot.StackSize > 1)
                        {
                            targetSlot = new DummySlot(targetSlot.TakeOut(1));
                            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                            blockPot.ServeIntoBowlStack(targetSlot, potSlot, world);
                            if (!byPlayer.InventoryManager.TryGiveItemstack(targetSlot.Itemstack, true))
                            {
                                world.SpawnItemEntity(targetSlot.Itemstack, byPlayer.Entity.ServerPos.XYZ);
                            }
                        } else
                        {
                            blockPot.ServeIntoBowlStack(targetSlot, potSlot, world);
                        }
                        
                    }

                    return true;
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


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-firepit-open",
                    MouseButton = EnumMouseButton.Right
                }, 
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-firepit-refuel",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak"
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
