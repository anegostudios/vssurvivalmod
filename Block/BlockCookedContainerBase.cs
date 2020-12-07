using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCookedContainerBase : BlockContainer, IBlockMealContainer
    {
        public void SetContents(string recipeCode, float servings, ItemStack containerStack, ItemStack[] stacks)
        {
            base.SetContents(containerStack, stacks);

            containerStack.Attributes.SetFloat("quantityServings", servings);
            containerStack.Attributes.SetString("recipeCode", recipeCode);
        }


        public void SetContents(string recipeCode, ItemStack containerStack, ItemStack[] stacks, float quantityServings = 1)
        {
            base.SetContents(containerStack, stacks);

            if (recipeCode == null)
            {
                containerStack.Attributes.RemoveAttribute("recipeCode");
            }
            else
            {
                containerStack.Attributes.SetString("recipeCode", recipeCode);
            }

            containerStack.Attributes.SetFloat("quantityServings", quantityServings);
        }



        public float GetQuantityServings(IWorldAccessor world, ItemStack byItemStack)
        {
            return (float)byItemStack.Attributes.GetDecimal("quantityServings");
        }

        public void SetQuantityServings(IWorldAccessor world, ItemStack byItemStack, float value)
        {
            byItemStack.Attributes.SetFloat("quantityServings", value);
        }


        public CookingRecipe GetCookingRecipe(IWorldAccessor world, ItemStack containerStack)
        {
            return world.CookingRecipes.FirstOrDefault(rec => rec.Code == GetRecipeCode(world, containerStack));
        }

        public string GetRecipeCode(IWorldAccessor world, ItemStack containerStack)
        {
            return containerStack.Attributes.GetString("recipeCode");
        }

        /// <summary>
        /// When code is null, will delete the attribtues recipeCode, quantityServings and contents
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <param name="code"></param>
        public void SetRecipeCode(IWorldAccessor world, ItemStack containerStack, string code)
        {
            if (code == null)
            {
                containerStack.Attributes.RemoveAttribute("recipeCode");
                containerStack.Attributes.RemoveAttribute("quantityServings");
                containerStack.Attributes.RemoveAttribute("contents");
                return;
            } 
            containerStack.Attributes.SetString("recipeCode", code);
        }

        internal float GetServings(IWorldAccessor world, ItemStack byItemStack)
        {
            return (float)byItemStack.Attributes.GetDecimal("quantityServings");
        }

        internal void SetServings(IWorldAccessor world, ItemStack byItemStack, float value)
        {
            byItemStack.Attributes.SetFloat("quantityServings", value);
        }

        public CookingRecipe GetMealRecipe(IWorldAccessor world, ItemStack containerStack)
        {
            string recipecode = GetRecipeCode(world, containerStack);
            return world.CookingRecipes.FirstOrDefault((rec) => recipecode == rec.Code);
        }


        public void ServeIntoBowl(Block selectedBlock, BlockPos pos, ItemSlot potslot, IWorldAccessor world)
        {
            if (world.Side == EnumAppSide.Client) return;

            string code = selectedBlock.Attributes["mealBlockCode"].AsString();
            Block mealblock = api.World.GetBlock(new AssetLocation(code));

            world.BlockAccessor.SetBlock(mealblock.BlockId, pos);

            IBlockEntityMealContainer bemeal = api.World.BlockAccessor.GetBlockEntity(pos) as IBlockEntityMealContainer;
            if (bemeal == null) return;

            if (tryMergeServingsIntoBE(bemeal, potslot)) return;

            bemeal.RecipeCode = GetRecipeCode(world, potslot.Itemstack);

            ItemStack[] myStacks = GetNonEmptyContents(api.World, potslot.Itemstack);
            for (int i = 0; i < myStacks.Length; i++)
            {
                bemeal.inventory[i].Itemstack = myStacks[i].Clone();
            }

            float quantityServings = GetServings(world, potslot.Itemstack);
            float servingsToTransfer = Math.Min(quantityServings, selectedBlock.Attributes["servingCapacity"].AsFloat(1));

            bemeal.QuantityServings = servingsToTransfer;


            SetServings(world, potslot.Itemstack, quantityServings - servingsToTransfer);

            if (quantityServings - servingsToTransfer <= 0)
            {
                string loc = Attributes["emptiedBlockCode"].AsString();
                if (loc != null)
                {
                    Block emptyPotBlock = world.GetBlock(new AssetLocation(loc));
                    potslot.Itemstack = new ItemStack(emptyPotBlock);
                } else
                {
                    SetRecipeCode(api.World, potslot.Itemstack, null);
                }
            }

            potslot.MarkDirty();
            bemeal.MarkDirty(true);
        }




        private bool tryMergeServingsIntoBE(IBlockEntityMealContainer bemeal, ItemSlot potslot)
        {
            ItemStack[] myStacks = GetNonEmptyContents(api.World, potslot.Itemstack);

            string hisRecipeCode = bemeal.RecipeCode;
            ItemStack[] hisStacks = bemeal.GetNonEmptyContentStacks();
            float hisServings = bemeal.QuantityServings;

            string ownRecipeCode = GetRecipeCode(api.World, potslot.Itemstack);
            float servingCapacity = (bemeal as BlockEntity).Block.Attributes["servingCapacity"].AsFloat(1);

            // Empty
            if (hisStacks == null || hisServings == 0) return false;
            // Different ingredient quantity
            if (myStacks.Length != hisStacks.Length) return true;
            // Different recipe
            if (ownRecipeCode != hisRecipeCode) return true;
            // No more empty space
            float remainingPlaceableServings = servingCapacity - hisServings;
            if (remainingPlaceableServings <= 0) return true;
            // Different ingredients
            for (int i = 0; i < myStacks.Length; i++)
            {
                if (!myStacks[i].Equals(api.World, hisStacks[i], GlobalConstants.IgnoredStackAttributes))
                {
                    return true;
                }
            }


            // Ok merge transition states
            for (int i = 0; i < hisStacks.Length; i++)
            {
                ItemStackMergeOperation op = new ItemStackMergeOperation(api.World, EnumMouseButton.Left, 0, EnumMergePriority.ConfirmedMerge, myStacks[i].StackSize);
                op.SourceSlot = new DummySlot(myStacks[i]);
                op.SinkSlot = new DummySlot(hisStacks[i]);
                hisStacks[i].Collectible.TryMergeStacks(op);
            }

            // Now increase serving siize
            float quantityServings = GetServings(api.World, potslot.Itemstack);
            float movedservings = Math.Min(remainingPlaceableServings, quantityServings);
            bemeal.QuantityServings = hisServings + movedservings;

            SetServings(api.World, potslot.Itemstack, quantityServings - movedservings);
            if (quantityServings - movedservings <= 0)
            {
                string loc = Attributes["emptiedBlockCode"].AsString();
                if (loc != null)
                {
                    Block emptyPotBlock = api.World.GetBlock(new AssetLocation(loc));
                    potslot.Itemstack = new ItemStack(emptyPotBlock);
                } else
                {
                    SetRecipeCode(api.World, potslot.Itemstack, null);
                }
            }

            potslot.MarkDirty();
            bemeal.MarkDirty(true);

            return true;
        }



        public bool ServeIntoBowlStack(ItemSlot bowlSlot, ItemSlot potslot, IWorldAccessor world)
        {
            if (world.Side == EnumAppSide.Client) return true;

            string code = bowlSlot.Itemstack.Block.Attributes["mealBlockCode"].AsString();
            Block mealblock = api.World.GetBlock(new AssetLocation(code)) as Block;

            ItemStack[] stacks = GetContents(api.World, potslot.Itemstack);
            float quantityServings = GetServings(world, potslot.Itemstack);
            string ownRecipeCode = GetRecipeCode(world, potslot.Itemstack);

            float servingCapacity = bowlSlot.Itemstack.Block.Attributes["servingCapacity"].AsFloat(1);

            // Merge existing servings
            if (bowlSlot.Itemstack.Block is IBlockMealContainer)
            {
                var mealcont = (bowlSlot.Itemstack.Block as IBlockMealContainer);

                ItemStack[] myStacks = GetNonEmptyContents(api.World, potslot.Itemstack);

                string hisRecipeCode = mealcont.GetRecipeCode(world, bowlSlot.Itemstack);
                ItemStack[] hisStacks = mealcont.GetNonEmptyContents(world, bowlSlot.Itemstack);
                float hisServings = mealcont.GetQuantityServings(world, bowlSlot.Itemstack);

                if (hisStacks != null && hisServings > 0)
                {
                    if (myStacks.Length != hisStacks.Length) return false;

                    if (ownRecipeCode != hisRecipeCode) return false;

                    float remainingPlaceableServings = servingCapacity - hisServings;
                    if (remainingPlaceableServings <= 0) return false;

                    for (int i = 0; i < myStacks.Length; i++)
                    {
                        if (!myStacks[i].Equals(world, hisStacks[i], GlobalConstants.IgnoredStackAttributes))
                        {
                            return false;
                        }
                    }

                    // Ok merge transition states
                    for (int i = 0; i < hisStacks.Length; i++)
                    {
                        ItemStackMergeOperation op = new ItemStackMergeOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.ConfirmedMerge, myStacks[i].StackSize);
                        op.SourceSlot = new DummySlot(myStacks[i]);
                        op.SinkSlot = new DummySlot(hisStacks[i]);
                        hisStacks[i].Collectible.TryMergeStacks(op);

                    }

                    // Now increase serving siize
                    float movedservings = Math.Min(remainingPlaceableServings, quantityServings);
                    mealcont.SetQuantityServings(world, bowlSlot.Itemstack, hisServings + movedservings);

                    SetServings(world, potslot.Itemstack, quantityServings - movedservings);
                    if (quantityServings - movedservings <= 0)
                    {
                        Block emptyPotBlock = world.GetBlock(new AssetLocation(Attributes["emptiedBlockCode"].AsString()));
                        potslot.Itemstack = new ItemStack(emptyPotBlock);
                    }


                    potslot.MarkDirty();
                    bowlSlot.MarkDirty();

                    return true;
                }
            }


            float servingsToTransfer = Math.Min(quantityServings, servingCapacity);

            ItemStack stack = new ItemStack(mealblock);
            (mealblock as IBlockMealContainer).SetContents(ownRecipeCode, stack, stacks, servingsToTransfer);

            SetServings(world, potslot.Itemstack, quantityServings - servingsToTransfer);

            if (quantityServings - servingsToTransfer <= 0)
            {
                Block emptyPotBlock = world.GetBlock(new AssetLocation(Attributes["emptiedBlockCode"].AsString()));
                potslot.Itemstack = new ItemStack(emptyPotBlock);
            }

            potslot.MarkDirty();

            bowlSlot.Itemstack = stack;
            bowlSlot.MarkDirty();
            return true;
        }



    }

}
