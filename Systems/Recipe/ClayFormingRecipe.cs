using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class ClayFormingRecipe : LayeredVoxelRecipe<ClayFormingRecipe>, IByteSerializable
    {
        public override int QuantityLayers => 16;
        public override string RecipeCategoryCode => "clay forming";


        /// <summary>
        /// Creates a deep copy
        /// </summary>
        /// <returns></returns>
        public override ClayFormingRecipe Clone()
        {
            ClayFormingRecipe recipe = new ClayFormingRecipe();

            recipe.Pattern = new string[Pattern.Length][];
            for (int i = 0; i < recipe.Pattern.Length; i++)
            {
                recipe.Pattern[i] = (string[])Pattern[i].Clone();
            }
            
            recipe.Ingredient = Ingredient.Clone();
            recipe.Output = Output.Clone();
            recipe.Name = Name;

            return recipe;
        }

    }
}
