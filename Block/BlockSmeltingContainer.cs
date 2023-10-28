using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSmeltingContainer : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                failureCode = "__ignore__";
                return false;
            }

            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            if (world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).CanAttachBlockAt(world.BlockAccessor, this, blockSel.Position.DownCopy(), BlockFacing.UP))
            {
                DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            failureCode = "requiresolidground";

            return false;
        }

        public override float GetMeltingDuration(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float duration = 0;

            ItemStack[] stacks = GetIngredients(world, cookingSlotsProvider);
            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i]?.Collectible?.CombustibleProps == null) continue;

                float singleDuration = stacks[i].Collectible.GetMeltingDuration(world, cookingSlotsProvider, inputSlot);

                duration += singleDuration * stacks[i].StackSize / stacks[i].Collectible.CombustibleProps.SmeltedRatio;
            }

            return duration;
        }


        public override float GetMeltingPoint(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float meltpoint = 0;

            ItemStack[] stacks = GetIngredients(world, cookingSlotsProvider);
            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i] == null) continue;

                meltpoint = Math.Max(meltpoint, stacks[i].Collectible.GetMeltingPoint(world, cookingSlotsProvider, inputSlot));
            }

            return meltpoint;
        }


        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            ItemStack[] stacks = GetIngredients(world, cookingSlotsProvider);

            // Got alloy?
            if (GetMatchingAlloy(world, stacks) != null)
            {
                return true;
            }

            for (int i = 0; i < stacks.Length; i++)
            {
                CombustibleProperties props = stacks[i]?.Collectible.CombustibleProps;
                if (props != null && !props.RequiresContainer) return false;
            }

            MatchedSmeltableStack match = GetSingleSmeltableStack(stacks);

            return match != null;
        }


        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            ItemStack[] stacks = GetIngredients(world, cookingSlotsProvider);

            AlloyRecipe alloy = GetMatchingAlloy(world, stacks);

            Block block = world.GetBlock(CodeWithPath(FirstCodePart() + "-smelted"));
            ItemStack outputStack = new ItemStack(block);

            if (alloy != null)
            {
                ItemStack smeltedStack = alloy.Output.ResolvedItemstack.Clone();
                int units = (int)Math.Round(alloy.GetTotalOutputQuantity(stacks) * 100, 0);

                ((BlockSmeltedContainer)block).SetContents(outputStack, smeltedStack, units);
                outputStack.Collectible.SetTemperature(world, outputStack, GetIngredientsTemperature(world, stacks));
                outputSlot.Itemstack = outputStack;
                inputSlot.Itemstack = null;

                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }


                return;
            }


            MatchedSmeltableStack match = GetSingleSmeltableStack(stacks);

            if (match != null)
            {
                ((BlockSmeltedContainer)block).SetContents(outputStack, match.output, (int)(match.stackSize * 100));
                outputStack.Collectible.SetTemperature(world, outputStack, GetIngredientsTemperature(world, stacks));
                outputSlot.Itemstack = outputStack;
                inputSlot.Itemstack = null;

                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }
            }

        }


        public string GetOutputText(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            if (inputSlot.Itemstack == null) return null;

            if (inputSlot.Itemstack.Collectible is BlockSmeltingContainer)
            {
                BlockSmeltingContainer bsc = (BlockSmeltingContainer)inputSlot.Itemstack.Collectible;

                ItemStack[] stacks = bsc.GetIngredients(world, cookingSlotsProvider);

                for (int i = 0; i < stacks.Length; i++)
                {
                    CombustibleProperties props = stacks[i]?.Collectible.CombustibleProps;
                    if (props != null && !props.RequiresContainer) return null;
                }

                AlloyRecipe alloy = bsc.GetMatchingAlloy(world, stacks);

                if (alloy != null)
                {
                    double quantity = alloy.GetTotalOutputQuantity(stacks);
                    return Lang.Get("Will create {0} units of {1}", (int)Math.Round(quantity * 100, 4), CheapMetalNameHax(alloy.Output.ResolvedItemstack));
                }

                MatchedSmeltableStack match = GetSingleSmeltableStack(stacks);
                if (match != null)
                {
                    return Lang.Get("Will create {0} units of {1}", (int)Math.Round(match.stackSize * 100, 4), CheapMetalNameHax(match.output));
                }

                return null;
            }

            return null;
        }


        public string CheapMetalNameHax(ItemStack ingot)
        {
            // cheap hax to get metal name
            string name = ingot.GetName();
            return
                ingot.Collectible.Code.Path.Contains("ingot") ? name.Substring(name.IndexOf("(") + 1, name.Length - 1 - name.IndexOf("(")) :  name;
        }


        


        public AlloyRecipe GetMatchingAlloy(IWorldAccessor world, ItemStack[] stacks)
        {
            List<AlloyRecipe> alloys = api.GetMetalAlloys();
            if (alloys == null) return null;

            for (int j = 0; j < alloys.Count; j++)
            {
                if (alloys[j].Matches(stacks))
                {
                    return alloys[j];
                }
            }
            
            return null;
        }


        public float GetIngredientsTemperature(IWorldAccessor world, ItemStack[] ingredients)
        {
            bool haveStack = false;
            float lowestTemp = 0;
            for (int i = 0; i < ingredients.Length; i++)
            {
                if (ingredients[i] != null)
                {
                    float stackTemp = ingredients[i].Collectible.GetTemperature(world, ingredients[i]);
                    lowestTemp = haveStack ? Math.Min(lowestTemp, stackTemp) : stackTemp;
                    haveStack = true;
                }

            }

            return lowestTemp;
        }



        public static MatchedSmeltableStack GetSingleSmeltableStack(ItemStack[] stacks)
        {
            // Got only same stacks to smelt directly?
            ItemStack outputStack = null;
            double quantity = 0;

            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i] == null) continue;

                ItemStack stack = stacks[i];
                double stackSize = stack.StackSize;

                if (stack.Collectible.CombustibleProps?.SmeltedStack != null && stack.Collectible.CombustibleProps.MeltingPoint > 0)
                {
                    stackSize *= stack.Collectible.CombustibleProps.SmeltedStack.StackSize;
                    stackSize /= stack.Collectible.CombustibleProps.SmeltedRatio;
                    stack = stack.Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack;
                } else
                {
                    return null;
                }

                if (outputStack == null)
                {
                    outputStack = stack.Clone();
                    quantity += stackSize;
                    continue;
                }

                if (outputStack.Class != stack.Class || outputStack.Id != stack.Id) return null;

                quantity += stackSize;
            }

            if (outputStack == null) return null;
            if (outputStack.Collectible is BlockSmeltingContainer) return null;

            return new MatchedSmeltableStack()
            {
                output = outputStack,
                stackSize = quantity
            };
        }



        public ItemStack[] GetIngredients(IWorldAccessor world, ISlotProvider cookingSlotsProvider)
        {
            ItemStack[] stacks = new ItemStack[cookingSlotsProvider.Slots.Length];

            for (int i = 0; i < stacks.Length; i++)
            {
                stacks[i] = cookingSlotsProvider.Slots[i].Itemstack;
            }

            return stacks;
        }
    }
}
