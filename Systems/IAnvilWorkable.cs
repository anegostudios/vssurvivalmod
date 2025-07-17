﻿using System.Collections.Generic;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumHelveWorkableMode
    {
        NotWorkable,
        FullyWorkable,
        TestSufficientVoxelsWorkable
    }

    public interface IAnvilWorkable
    {

        int GetRequiredAnvilTier(ItemStack stack);

        List<SmithingRecipe> GetMatchingRecipes(ItemStack stack);

        bool CanWork(ItemStack stack);

        /// <summary>
        /// Must also set the anvil voxels!
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="beAnvil"></param>
        /// <returns>The converted work item stack. Return stack itself if the same stack is used. Return null on failure.</returns>
        ItemStack TryPlaceOn(ItemStack stack, BlockEntityAnvil beAnvil);

        /// <summary>
        /// If the itemstack is not the same as the work itemstack, this needs to return the inverse
        /// </summary>
        /// <param name="stack"></param>
        /// <returns></returns>
        ItemStack GetBaseMaterial(ItemStack stack);

        EnumHelveWorkableMode GetHelveWorkableMode(ItemStack stack, BlockEntityAnvil beAnvil);

        /// <summary>
        /// This method is used by the handbook to determine how many of an item is needed when smithing the recipe
        /// </summary>
        /// <param name="stack"></param>
        /// <returns>Total number of metal voxels added by the ItemStack in question.</returns>
        int VoxelCountForHandbook(ItemStack stack);
    }
}
