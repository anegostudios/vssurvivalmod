using Vintagestory.API;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Creates a recipe that can be created using clay. This class takes all of its properties from the <see cref="LayeredVoxelRecipe{T}"/> class.
    /// </summary>
    /// <example>
    /// <code language="json">
    ///{
    ///  "ingredient": {
    ///    "type": "item",
    ///    "code": "clay-*",
    ///    "name": "type",
    ///    "allowedVariants": [ "blue", "fire", "red" ]
    ///  },
    ///  "pattern": [
    ///    [
    ///      "#####",
    ///      "#####",
    ///      "#####",
    ///      "#####",
    ///      "#####"
    ///    ],
    ///    [
    ///      "#####",
    ///      "#___#",
    ///      "#___#",
    ///      "#___#",
    ///      "#####"
    ///    ]
    ///  ],
    ///  "name": "Bowl",
    ///  "output": {
    ///    "type": "block",
    ///    "code": "bowl-raw"
    ///  }
    ///}</code></example>
    [DocumentAsJson]
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
