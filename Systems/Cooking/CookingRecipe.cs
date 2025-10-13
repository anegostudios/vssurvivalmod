using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class VanillaCookingRecipeNames : ICookingRecipeNamingHelper
    {
        protected enum EnumIngredientNameType
        {
            None,
            InsturmentalCase,
            Topping
        }

        /// <summary>
        /// Gets the name for ingredients in regards to food.
        /// </summary>
        /// <param name="worldForResolve">The world to resolve in.</param>
        /// <param name="recipeCode">The recipe code.</param>
        /// <param name="stacks">The stacks of items to add.</param>
        /// <returns>The name of the food type.</returns>
        public string GetNameForIngredients(IWorldAccessor worldForResolve, string recipeCode, ItemStack[] stacks)
        {
            API.Datastructures.OrderedDictionary<ItemStack, int> quantitiesByStack = new ();
            quantitiesByStack = mergeStacks(worldForResolve, stacks);

            CookingRecipe recipe = worldForResolve.Api.GetCookingRecipe(recipeCode);

            if (recipeCode == null || recipe == null || quantitiesByStack.Count == 0) return Lang.Get("unknown");

            return GetNameForMergedIngredients(worldForResolve, recipe, quantitiesByStack);
        }

        protected virtual string GetNameForMergedIngredients(IWorldAccessor worldForResolve, CookingRecipe recipe, API.Datastructures.OrderedDictionary<ItemStack, int> quantitiesByStack)
        {
            string recipeCode = recipe.Code!;

            switch (recipeCode)
            {
                case "soup":
                    {
                        List<string> BoiledIngredientNames = [];
                        List<string> StewedIngredientNames = [];
                        CookingRecipeIngredient? ingred = null;
                        ItemStack? stockStack = null;
                        ItemStack? creamStack = null;
                        ItemStack? mainStack = null;
                        string itemName = string.Empty;
                        int max = 0;

                        foreach (var val in quantitiesByStack)
                        {
                            if (val.Key.Collectible.Code.Path.Contains("waterportion")) continue;

                            ItemStack? stack = val.Key;
                            ingred = recipe.GetIngrendientFor(stack);
                            if (ingred?.Code == "cream")
                            {
                                creamStack = stack;
                                continue;
                            }
                            else if (ingred?.Code == "stock")
                            {
                                stockStack = stack;
                                continue;
                            }
                            else if (max < val.Value)
                            {
                                max = val.Value;
                                stack = mainStack;
                                mainStack = val.Key;
                            }

                            if (stack == null) continue;

                            itemName = ingredientName(stack, EnumIngredientNameType.InsturmentalCase);
                            if (getFoodCat(worldForResolve, stack, ingred) == EnumFoodCategory.Vegetable ||
                                stack.Collectible.FirstCodePart().Contains("egg"))
                            {
                                if (!BoiledIngredientNames.Contains(itemName)) BoiledIngredientNames.Add(itemName);
                            }
                            else
                            {
                                if (!StewedIngredientNames.Contains(itemName)) StewedIngredientNames.Add(itemName);
                            }
                        }

                        List<string> MainIngredientNames = [];
                        string MainIngredientFormat = "{0}";

                        if (creamStack != null)
                        {
                            if (stockStack != null) itemName = getMainIngredientName(stockStack, "soup");
                            else if (mainStack != null)
                            {
                                itemName = getMainIngredientName(mainStack, "soup");
                            }
                            MainIngredientNames.Add(itemName);
                            MainIngredientNames.Add(getMainIngredientName(creamStack, "soup", true));
                            MainIngredientFormat = "meal-soup-in-cream-format";
                        }
                        else if (stockStack != null)
                        {
                            if (mainStack != null)
                            {
                                itemName = getMainIngredientName(mainStack, "soup");
                            }
                            MainIngredientNames.Add(itemName);
                            MainIngredientNames.Add(getMainIngredientName(stockStack, "soup", true));
                            MainIngredientFormat = "meal-soup-in-stock-format";
                        }
                        else if (mainStack != null)
                        {
                            MainIngredientNames.Add(getMainIngredientName(mainStack, "soup"));
                        }

                        string ExtraIngredientsFormat = "meal-adds-soup-boiled";
                        if (StewedIngredientNames.Count > 0)
                        {
                            if (BoiledIngredientNames.Count > 0) ExtraIngredientsFormat = "meal-adds-soup-boiled-and-stewed";
                            else ExtraIngredientsFormat = "meal-adds-soup-stewed";
                        }

                        string MealFormat = getMaxMealFormat("meal", "soup", max);
                        MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, MainIngredientFormat), getMealAddsString(ExtraIngredientsFormat, BoiledIngredientNames, StewedIngredientNames));
                        return MealFormat.Trim().UcFirst();
                    }

                case "porridge":
                    {
                        string MealFormat = "meal";
                        List<string> MainIngredientNames = [];
                        List<string> MashedIngredientNames = [];
                        List<string> FreshIngredientNames = [];
                        string ToppingName = string.Empty;
                        string itemName = string.Empty;
                        int typesOfGrain = quantitiesByStack.Where(val => recipe.GetIngrendientFor(val.Key)?.Code == "grain-base").Count();
                        int max = 0;

                        foreach (var val in quantitiesByStack)
                        {
                            CookingRecipeIngredient? ingred = recipe.GetIngrendientFor(val.Key);
                            if (ingred?.Code == "topping")
                            {
                                ToppingName = ingredientName(val.Key, EnumIngredientNameType.Topping);
                                continue;
                            }

                            if (ingred?.Code == "grain-base")
                            {
                                if (typesOfGrain < 3)
                                {
                                    if (MainIngredientNames.Count < 2)
                                    {
                                        itemName = getMainIngredientName(val.Key, recipeCode, MainIngredientNames.Count > 0);
                                        if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                    }
                                }
                                else
                                {
                                    itemName = ingredientName(val.Key);
                                    if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                }

                                max += val.Value;
                                continue;
                            }

                            itemName = ingredientName(val.Key, EnumIngredientNameType.InsturmentalCase);
                            if (getFoodCat(worldForResolve, val.Key, ingred) == EnumFoodCategory.Vegetable)
                            {
                                if (!MashedIngredientNames.Contains(itemName)) MashedIngredientNames.Add(itemName);
                            }
                            else
                            {
                                if (!FreshIngredientNames.Contains(itemName)) FreshIngredientNames.Add(itemName);
                            }
                        }

                        string ExtraIngredientsFormat = "meal-adds-porridge-mashed";
                        if (FreshIngredientNames.Count > 0)
                        {
                            if (MashedIngredientNames.Count > 0) ExtraIngredientsFormat = "meal-adds-porridge-mashed-and-fresh";
                            else ExtraIngredientsFormat = "meal-adds-porridge-fresh";
                        }

                        string MainIngredientFormat = "{0}";
                        if (MainIngredientNames.Count == 2) MainIngredientFormat = "multi-main-ingredients-format";
                        MealFormat = getMaxMealFormat(MealFormat, recipeCode, max);
                        MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, MainIngredientFormat), getMealAddsString(ExtraIngredientsFormat, MashedIngredientNames, FreshIngredientNames));
                        if (ToppingName != string.Empty) MealFormat = Lang.Get("meal-topping-ingredient-format", ToppingName, MealFormat);
                        return MealFormat.Trim().UcFirst();
                    }

                case "meatystew":
                case "vegetablestew":
                    {
                        ItemStack[] requiredStacks = new ItemStack[quantitiesByStack.Count];
                        int vegetableCount = 0;
                        int proteinCount = 0;

                        foreach (var ingred in recipe.Ingredients!)
                        {
                            if (ingred.Code.Contains("base"))
                            {
                                for (int i = 0; i < quantitiesByStack.Count; i++)
                                {
                                    var stack = quantitiesByStack.GetKeyAtIndex(i);
                                    if (!ingred.Matches(stack)) continue;
                                    if (requiredStacks.Contains(stack)) continue;

                                    requiredStacks[i] = stack;
                                    if (getFoodCat(worldForResolve, stack, ingred) == EnumFoodCategory.Vegetable) vegetableCount++;
                                    if (getFoodCat(worldForResolve, stack, ingred) == EnumFoodCategory.Protein) proteinCount++;
                                }
                            }
                        }

                        List<string> MainIngredientNames = [];
                        List<string> BoiledIngredientNames = [];
                        List<string> StewedIngredientNames = [];
                        string ToppingName = string.Empty;
                        string itemName = string.Empty;
                        EnumFoodCategory primaryCategory = EnumFoodCategory.Protein;
                        int max = 0;

                        if (vegetableCount > proteinCount) primaryCategory = EnumFoodCategory.Vegetable;
                        for (int i = 0; i < quantitiesByStack.Count; i++)
                        {
                            var stack = quantitiesByStack.GetKeyAtIndex(i);
                            int quantity = quantitiesByStack.GetValueAtIndex(i);

                            CookingRecipeIngredient? ingred = recipe.GetIngrendientFor(stack);
                            if (ingred?.Code == "topping")
                            {
                                ToppingName = ingredientName(stack, EnumIngredientNameType.Topping);
                                continue;
                            }

                            var cat = getFoodCat(worldForResolve, requiredStacks[i], ingred);
                            if ((cat is EnumFoodCategory.Vegetable or EnumFoodCategory.Protein && quantitiesByStack.Count <= 2) || cat == primaryCategory)
                            {
                                max += quantity;

                                if (MainIngredientNames.Count < 2)
                                {
                                    itemName = getMainIngredientName(stack, "stew", MainIngredientNames.Count > 0);
                                    if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                    continue;
                                }
                            }

                            itemName = ingredientName(stack, EnumIngredientNameType.InsturmentalCase);
                            if (getFoodCat(worldForResolve, stack, ingred) == EnumFoodCategory.Vegetable ||
                                stack.Collectible.FirstCodePart().Contains("egg"))
                            {
                                if (!BoiledIngredientNames.Contains(itemName)) BoiledIngredientNames.Add(itemName);
                            }
                            else
                            {
                                if (!StewedIngredientNames.Contains(itemName)) StewedIngredientNames.Add(itemName);
                            }
                        }

                        string ExtraIngredientsFormat = "meal-adds-stew-boiled";
                        if (StewedIngredientNames.Count > 0)
                        {
                            if (BoiledIngredientNames.Count > 0) ExtraIngredientsFormat = "meal-adds-stew-boiled-and-stewed";
                            else ExtraIngredientsFormat = "meal-adds-stew-stewed";
                        }

                        string MainIngredientFormat = "{0}";
                        if (MainIngredientNames.Count == 2) MainIngredientFormat = "multi-main-ingredients-format";
                        string MealFormat = getMaxMealFormat("meal", "stew", max);
                        MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, MainIngredientFormat), getMealAddsString(ExtraIngredientsFormat, BoiledIngredientNames, StewedIngredientNames));
                        if (ToppingName != string.Empty) MealFormat = Lang.Get("meal-topping-ingredient-format", ToppingName, MealFormat);
                        return MealFormat.Trim().UcFirst();
                    }

                case "scrambledeggs":
                    {
                        List<string> MainIngredientNames = [];
                        List<string> FreshIngredientNames = [];
                        List<string> MeltedIngredientNames = [];
                        string itemName = string.Empty;
                        int max = 0;

                        foreach (var val in quantitiesByStack)
                        {
                            if (recipe.GetIngrendientFor(val.Key)?.Code == "egg-base")
                            {
                                itemName = getMainIngredientName(val.Key, recipeCode);
                                if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                max += val.Value;
                                continue;
                            }

                            itemName = ingredientName(val.Key, EnumIngredientNameType.InsturmentalCase);

                            if (val.Key.Collectible.FirstCodePart() == "cheese")
                            {
                                if (!MeltedIngredientNames.Contains(itemName)) MeltedIngredientNames.Add(itemName);
                                continue;
                            }

                            if (!FreshIngredientNames.Contains(itemName)) FreshIngredientNames.Add(itemName);
                        }

                        string ExtraIngredientsFormat = "meal-adds-scrambledeggs-fresh";
                        if (MeltedIngredientNames.Count > 0)
                        {
                            if (FreshIngredientNames.Count > 0) ExtraIngredientsFormat = "meal-adds-scrambledeggs-melted-and-fresh";
                            else ExtraIngredientsFormat = "meal-adds-scrambledeggs-melted";
                        }

                        string MealFormat = getMaxMealFormat("meal", recipeCode, max);
                        MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, "{0}"), getMealAddsString(ExtraIngredientsFormat, MeltedIngredientNames, FreshIngredientNames));
                        return MealFormat.Trim().UcFirst();
                    }

                case "jam":
                    {
                        ItemStack[] fruits = new ItemStack[2];
                        int i = 0;
                        foreach (var val in quantitiesByStack)
                        {
                            if (recipe.GetIngrendientFor(val.Key)?.Code != "sweetener")
                            {
                                fruits[i++] = val.Key;
                                if (i == 2) break;
                            }
                        }

                        if (fruits[0] != null)
                        {
                            string jamName = fruits[0].Collectible.LastCodePart() + (fruits[1] != null ? ("-" + fruits[1].Collectible.LastCodePart() + "-") : "-") + "jam";
                            if (Lang.HasTranslation(jamName)) return Lang.Get(jamName);

                            return Lang.Get(fruits[1] != null ? "mealname-mixedjam" : "mealname-singlejam", getInJamName(fruits[0]), getInJamName(fruits[1]));
                        }
                        else return Lang.Get("unknown");
                    }

                default:
                    {
                        if (Lang.HasTranslation("meal-" + recipeCode)) return Lang.Get("meal-" + recipeCode);

                        ItemStack[] requiredStacks = new ItemStack[quantitiesByStack.Count];
                        int requiredCount = 0;
                        bool hasBase = false;

                        foreach (var ingred in recipe.Ingredients!)
                        {
                            bool isBase = ingred.Code.Contains("base");
                            if (isBase && !hasBase)
                            {
                                hasBase = true;
                                requiredStacks = new ItemStack[quantitiesByStack.Count];
                                requiredCount = 0;
                            }

                            if ((isBase && hasBase) || (ingred.MinQuantity > 0 && !hasBase))
                            {
                                for (int i = 0; i < quantitiesByStack.Count; i++)
                                {
                                    var stack = quantitiesByStack.GetKeyAtIndex(i);
                                    if (!ingred.Matches(quantitiesByStack.GetKeyAtIndex(i))) continue;
                                    if (requiredStacks.Contains(stack)) continue;
                                        
                                    requiredStacks[i] = stack;
                                    requiredCount++;
                                }
                            }
                        }

                        List<string> MainIngredientNames = [];
                        List<string> ExtraIngredientNames = [];
                        string ToppingName = string.Empty;
                        string itemName = string.Empty;
                        int max = 0;

                        for (int i = 0; i < quantitiesByStack.Count; i++)
                        {
                            var stack = quantitiesByStack.GetKeyAtIndex(i);
                            int quantity = quantitiesByStack.GetValueAtIndex(i);

                            CookingRecipeIngredient? ingred = recipe.GetIngrendientFor(stack);
                            if (ingred?.Code == "topping")
                            {
                                ToppingName = ingredientName(stack, EnumIngredientNameType.Topping);
                                continue;
                            }

                            if (requiredStacks[i] != null)
                            {
                                if (requiredCount < 3)
                                {
                                    if (MainIngredientNames.Count < 2)
                                    {
                                        itemName = getMainIngredientName(stack, recipeCode, MainIngredientNames.Count > 0);
                                        if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                    }
                                }
                                else
                                {
                                    itemName = ingredientName(stack);
                                    if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                }

                                max += quantity;
                                continue;
                            }

                            itemName = ingredientName(stack, EnumIngredientNameType.InsturmentalCase);
                            if (!ExtraIngredientNames.Contains(itemName)) ExtraIngredientNames.Add(itemName);
                        }

                        string MainIngredientFormat = "{0}";
                        if (MainIngredientNames.Count == 2) MainIngredientFormat = "multi-main-ingredients-format";
                        string MealFormat = getMaxMealFormat("meal", recipeCode, max);
                        MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, MainIngredientFormat), getMealAddsString("meal-adds-generic", ExtraIngredientNames));
                        if (ToppingName != string.Empty) MealFormat = Lang.Get("meal-topping-ingredient-format", ToppingName, MealFormat);
                        return MealFormat.Trim().UcFirst();
                    }
            }
        }

        protected string getMaxMealFormat(string format, string recipeCode, int max)
        {
            switch (max)
            {
                case 3:
                    format += "-hearty-" + recipeCode;
                    break;
                case 4:
                    format += "-hefty-" + recipeCode;
                    break;
                default:
                    format += "-normal-" + recipeCode;
                    break;
            }
            return format;
        }

        protected EnumFoodCategory getFoodCat(IWorldAccessor worldForResolve, ItemStack stack, CookingRecipeIngredient? ingred)
        {
            var cookedStack = ingred?.GetMatchingStack(stack)?.CookedStack?.ResolvedItemstack;
            FoodNutritionProperties? props = BlockMeal.GetIngredientStackNutritionProperties(worldForResolve, cookedStack, null) ??
                                             BlockMeal.GetIngredientStackNutritionProperties(worldForResolve, stack, null);

            return props?.FoodCategory ?? EnumFoodCategory.Unknown;
        }

        protected string ingredientName(ItemStack stack, EnumIngredientNameType NameType = EnumIngredientNameType.None)
        {
            string code;

            code = stack.Collectible.Code?.Domain + AssetLocation.LocationSeparator + "recipeingredient-" + stack.Class.ToString().ToLowerInvariant() + "-" + stack.Collectible.Code?.Path;

            if (NameType == EnumIngredientNameType.InsturmentalCase)
                code += "-insturmentalcase";

            if (NameType == EnumIngredientNameType.Topping)
                code += "-topping";

            if (Lang.HasTranslation(code))
            {
                return Lang.GetMatching(code);
            }

            code = stack.Collectible.Code?.Domain + AssetLocation.LocationSeparator + "recipeingredient-" + stack.Class.ToString().ToLowerInvariant() + "-" + stack.Collectible.FirstCodePart();

            if (NameType == EnumIngredientNameType.InsturmentalCase)
                code += "-insturmentalcase";

            if (NameType == EnumIngredientNameType.Topping)
                code += "-topping";

            return Lang.GetMatching(code);
        }

        protected string getMainIngredientName(ItemStack itemstack, string code, bool secondary = false)
        {
            string t = secondary ? "secondary" : "primary";
            string langcode = $"meal-ingredient-{code}-{t}-{itemstack.Collectible.Code.Path}";

            if (Lang.HasTranslation(langcode, true))
            {
                return Lang.GetMatching(langcode);
            }

            langcode = $"meal-ingredient-{code}-{t}-{itemstack.Collectible.FirstCodePart()}";
            return Lang.GetMatching(langcode);
        }

        protected string getInJamName(ItemStack fruit)
        {
            if (fruit == null) return "";
            string fruitInJam = (fruit.Collectible.Code.Domain + ":" + fruit.Collectible.LastCodePart() + "-in-jam-name").Replace("game:", "");
            return Lang.HasTranslation(fruitInJam) ? Lang.Get(fruitInJam) : fruit.GetName();
        }

        protected string getMainIngredientsString(List<string> ingredients, string format, bool list = true)
        {
            if (ingredients.Count == 0) return "";

            if (ingredients.Count < 3 || list == false) return Lang.Get(format, ingredients.ToArray());
            return getMealAddsString(format, ingredients);
        }

        protected string getMealAddsString(string code, List<string> ingredients1, List<string>? ingredients2 = null)
        {
            if (ingredients1.Count == 0)
            {
                if ((ingredients2?.Count ?? 0) == 0) return "";
                ingredients1 = ingredients2!.ToList();
                ingredients2 = null;
            }

            return Lang.Get(code, Lang.Get($"meal-ingredientlist-{ingredients1?.Count ?? 0}", ingredients1?.ToArray() ?? [""]), Lang.Get($"meal-ingredientlist-{ingredients2?.Count ?? 0}", ingredients2?.ToArray() ?? [""]));
        }

        protected static API.Datastructures.OrderedDictionary<ItemStack, int> mergeStacks(IWorldAccessor worldForResolve, ItemStack[] stacks)
        {
            API.Datastructures.OrderedDictionary<ItemStack, int> dict = new ();

            List<ItemStack> stackslist = [.. stacks];
            while (stackslist.Count > 0)
            {
                ItemStack stack = stackslist[0];
                stackslist.RemoveAt(0);
                if (stack == null) continue;

                int cnt = 1;

                while (true)
                {
                    if (stackslist.FirstOrDefault((ostack) => ostack != null && ostack.Equals(worldForResolve, stack, GlobalConstants.IgnoredStackAttributes)) is ItemStack fstack)
                    {
                        stackslist.Remove(fstack);
                        cnt++;
                        continue;
                    }

                    break;
                }

                dict[stack] = cnt;
            }

            return dict;
        }

    }

    /// <summary>
    /// Interface for a helper for cooking various food in game.
    /// </summary>
    public interface ICookingRecipeNamingHelper
    {
        /// <summary>
        /// Gets the name for ingredients in regards to food.
        /// </summary>
        /// <param name="worldForResolve">The world to resolve in.</param>
        /// <param name="recipeCode">The recipe code.</param>
        /// <param name="stacks">The stacks of items to add.</param>
        /// <returns>The name of the food type.</returns>
        string GetNameForIngredients(IWorldAccessor worldForResolve, string recipeCode, ItemStack[] stacks);
    }

    /// <summary>
    /// Defines a recipe to be made using a cooking pot.
    /// Creating a new recipe for a cooking pot will automatically register the new meal item, unless using <see cref="CooksInto"/>.
    /// </summary>
    /// <example> 
    /// <code langauge="json">
    ///{
    ///	"code": "jam",
    ///	"perishableProps": {
    ///		"freshHours": { "avg": 1080 },
    ///		"transitionHours": { "avg": 180 },
    ///		"transitionRatio": 1,
    ///		"transitionedStack": {
    ///			"type": "item",
    ///			"code": "rot"
    ///		}
    ///	},
    ///	"shape": { "base": "block/food/meal/jam" },
    ///	"ingredients": [
    ///		{
    ///			"code": "honey",
    ///			"validStacks": [
    ///				{
    ///					"type": "item",
    ///					"code": "honeyportion",
    ///					"shapeElement": "bowl/honey",
    ///					"cookedStack": {
    ///						"type": "item",
    ///						"code": "jamhoneyportion"
    ///					}
    ///				}
    ///			],
    ///			"minQuantity": 2,
    ///			"maxQuantity": 2,
    ///			"portionSizeLitres": 0.2
    ///		},
    ///		{
    ///			"code": "fruit",
    ///			"validStacks": [
    ///				{
    ///					"type": "item",
    ///					"code": "fruit-*",
    ///					"shapeElement": "bowl/fruit"
    ///				}
    ///			],
    ///			"minQuantity": 2,
    ///			"maxQuantity": 2
    ///		}
    ///	]
    ///}
    /// </code>
    /// </example>
    [DocumentAsJson]
    public class CookingRecipe : IByteSerializable
    {
        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// A unique code for the recipe and meal created.
        /// </summary>
        [DocumentAsJson] public string? Code;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// A list of ingredients for the recipe. Although cooking pots have a maximum of 4 unique entries, there is no limit on the number of potential ingredients.
        /// </summary>
        [DocumentAsJson] public CookingRecipeIngredient[]? Ingredients;

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>True</jsondefault>-->
        /// Should this recipe be loaded by the game?
        /// </summary>
        [DocumentAsJson] public bool Enabled = true;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// A path to the shape file for this meal when inside a cooking pot. Specific ingredient-based elements can be enabled using the <see cref="CookingRecipeStack.ShapeElement"/> in the ingredient stacks.
        /// </summary>
        [DocumentAsJson] public CompositeShape? Shape;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// The transitionable properties for the meal item. Usually controls meal expiry.
        /// </summary>
        [DocumentAsJson] public TransitionableProperties? PerishableProps;

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>None</jsondefault>-->
        /// If set, will treat the recipe not as a meal with its ingredients retained but convert the ingredients into supplied itemstack.
        /// </summary>
        [DocumentAsJson] public JsonItemStack? CooksInto = null;

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>False</jsondefault>-->
        /// If this is true and CooksInto is set the recipe will not dirty the pot.
        /// </summary>
        [DocumentAsJson] public bool IsFood = false;

        public static Dictionary<string, ICookingRecipeNamingHelper> NamingRegistry = new Dictionary<string, ICookingRecipeNamingHelper>();

        public bool Matches(ItemStack?[] inputStacks)
        {
            int useless = 0;
            return Matches(inputStacks, ref useless);
        }

        public int GetQuantityServings(ItemStack[] stacks)
        {
            int quantity = 0;
            Matches(stacks, ref quantity);
            return quantity;
        }

        /// <summary>
        /// Gets the name of the output food if one exists.
        /// </summary>
        /// <param name="worldForResolve"></param>
        /// <param name="inputStacks"></param>
        /// <returns></returns>
        public string GetOutputName(IWorldAccessor worldForResolve, ItemStack[] inputStacks)
        {
            bool rotten = inputStacks.Any((stack) => stack?.Collectible.Code.Path == "rot");
            if (rotten)
            {
                return Lang.Get("Rotten Food");
            }


            if (NamingRegistry.TryGetValue(Code!, out ICookingRecipeNamingHelper? namer))
            {
                return namer.GetNameForIngredients(worldForResolve, Code!, inputStacks);
            }

            return new VanillaCookingRecipeNames().GetNameForIngredients(worldForResolve, Code!, inputStacks);
        }



        public bool Matches(ItemStack?[] inputStacks, ref int quantityServings)
        {
            if (Ingredients == null) return false;

            List<ItemStack> inputStacksList = [.. inputStacks];
            List<CookingRecipeIngredient> ingredientList = [.. Ingredients];

            int totalOutputQuantity = 99999;

            int[] curQuantities = new int[ingredientList.Count];
            for (int i = 0; i < curQuantities.Length; i++) curQuantities[i] = 0;

            while (inputStacksList.Count > 0)
            {
                ItemStack inputStack = inputStacksList[0];
                inputStacksList.RemoveAt(0);
                if (inputStack == null) continue;

                bool found = false;
                for (int i = 0; i < ingredientList.Count; i++)
                {
                    CookingRecipeIngredient ingred = ingredientList[i];
                    
                    if (ingred.GetMatchingStack(inputStack) is CookingRecipeStack jstack)
                    {
                        if (curQuantities[i] >= ingred.MaxQuantity) continue;
                        int stackPortion = inputStack.StackSize / jstack.StackSize;

                        if (BlockLiquidContainerBase.GetContainableProps(inputStack) is WaterTightContainableProps props)
                        {
                            stackPortion = (int)(inputStack.StackSize / jstack.StackSize / props.ItemsPerLitre / ingred.PortionSizeLitres);
                        }

                        totalOutputQuantity = Math.Min(totalOutputQuantity, stackPortion);
                        curQuantities[i]++;
                        found = true;
                        break;
                    }
                }

                // This input stack does not fit in this cooking recipe
                if (!found) return false;
            }

            // Any required ingredients left?
            for (int i = 0; i < ingredientList.Count; i++)
            {
                if (curQuantities[i] < ingredientList[i].MinQuantity) return false;
            }

            quantityServings = totalOutputQuantity;

            // Too many ingredients?
            for (int i = 0; i < inputStacks.Length; i++)
            {
                var stack = inputStacks[i];
                if (stack == null) continue;

                int jStackSize = GetIngrendientFor(stack)?.GetMatchingStack(stack)?.StackSize ?? 1;
                if (BlockLiquidContainerBase.GetContainableProps(stack) is WaterTightContainableProps props)
                {
                    if (stack.StackSize / jStackSize != (int)(quantityServings * props.ItemsPerLitre * (GetIngrendientFor(stack)?.PortionSizeLitres ?? 100))) quantityServings = -1;
                }
                else if (stack.StackSize / jStackSize != quantityServings) quantityServings = -1;

                if (quantityServings == -1) return false;
            }

            return true;
        }
       

        public CookingRecipeIngredient? GetIngrendientFor(ItemStack? stack, params CookingRecipeIngredient[] ingredsToskip)
        {
            if (stack == null) return null;

            for (int i = 0; i < Ingredients!.Length; i++)
            {
                if (Ingredients[i].Matches(stack) && !ingredsToskip.Contains(Ingredients[i])) return Ingredients[i];
            }

            return null;
        }


        public void Resolve(IServerWorldAccessor world, string sourceForErrorLogging)
        {
            if (Ingredients == null) return;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].Resolve(world, sourceForErrorLogging);
            }

            CooksInto?.Resolve(world, sourceForErrorLogging);
        }

        public ItemStack?[] GenerateRandomMeal(ICoreAPI api, ref Dictionary<CookingRecipeIngredient, HashSet<ItemStack?>>? cachedValidStacksByIngredient, ItemStack[] allstacks, int slots = 4, ItemStack? ingredientStack = null)
        {
            if (Ingredients == null) return new ItemStack?[slots];

            Dictionary<CookingRecipeIngredient, HashSet<ItemStack?>>? validStacksByIngredient = cachedValidStacksByIngredient;

            if (cachedValidStacksByIngredient == null)
            {
                validStacksByIngredient = new();

                foreach (var ingredient in Ingredients)
                {
                    HashSet<ItemStack?> ingredientStacks = [];
                    List<AssetLocation> ingredientCodes = [];

                    ingredient.Resolve(api.World, "handbook meal recipes");
                    foreach (var astack in allstacks)
                    {
                        if (ingredient.GetMatchingStack(astack) is not CookingRecipeStack vstack) continue;

                        ItemStack stack = astack.Clone();
                        stack.StackSize = vstack.StackSize;

                        if (BlockLiquidContainerBase.GetContainableProps(stack) is WaterTightContainableProps props)
                        {
                            stack.StackSize *= (int)(props.ItemsPerLitre * ingredient.PortionSizeLitres);
                        }

                        ingredientStacks.Add(stack);
                    }

                    if (ingredient.MinQuantity <= 0) ingredientStacks.Add(null);

                    validStacksByIngredient.Add(ingredient.Clone(), ingredientStacks);
                }

                cachedValidStacksByIngredient = validStacksByIngredient;
            }

            if (validStacksByIngredient == null) return new ItemStack?[slots];

            List<ItemStack?> randomMeal = new();

            while (!Matches(randomMeal.ToArray()))
            {
                var valIngStacks = new Dictionary<CookingRecipeIngredient, List<ItemStack?>>();
                foreach (var entry in validStacksByIngredient) valIngStacks.Add(entry.Key.Clone(), entry.Value.ToList());
                valIngStacks = valIngStacks.OrderBy(x => api.World.Rand.Next()).ToDictionary(item => item.Key, item => item.Value);

                CookingRecipeIngredient? requestedIngredient = null;
                if (ingredientStack != null)
                {
                    var validIngredients = Ingredients.Where(ingredient => ingredient.Matches(ingredientStack)).ToList();
                    requestedIngredient = validIngredients[api.World.Rand.Next(validIngredients.Count)].Clone();
                }

                randomMeal = new List<ItemStack?>();

                foreach (var entry in valIngStacks.Where(entry => entry.Key.MinQuantity > 0))
                {
                    var ingredient = entry.Key;
                    var validStacks = entry.Value;

                    if (ingredient.Code == requestedIngredient?.Code)
                    {
                        ItemStack? stack = validStacks.First(stack => stack?.Collectible.Code == ingredientStack?.Collectible.Code);
                        if (stack != null)
                        {
                            randomMeal.Add(stack.Clone());

                            ingredient.MinQuantity--;
                            ingredient.MaxQuantity--;
                        }
                        requestedIngredient = null;
                    }

                    while (ingredient.MinQuantity > 0)
                    {
                        randomMeal.Add(validStacks[api.World.Rand.Next(validStacks.Count)]?.Clone());

                        ingredient.MinQuantity--;
                        ingredient.MaxQuantity--;
                    }

                    if (ingredient.MaxQuantity > 0) validStacks.Add(null);
                    else valIngStacks.Remove(ingredient);
                }

                int tries = slots - randomMeal.Count;
                int requestedTry = 0;
                if (requestedIngredient != null) requestedTry = api.World.Rand.Next(tries) + 1;
                while (tries > 0)
                {
                    if (api.World.Rand.NextDouble() > 0.25 || tries == requestedTry)
                    {
                        valIngStacks = valIngStacks.OrderBy(x => api.World.Rand.Next()).ToDictionary(item => item.Key, item => item.Value);

                        foreach (var entry in valIngStacks)
                        {
                            var ingredient = entry.Key;
                            var validStacks = entry.Value;

                            if (tries == requestedTry)
                            {
                                if (requestedIngredient != null && ingredient.Code != requestedIngredient?.Code) continue;

                                if (ingredient.Code == requestedIngredient?.Code)
                                {
                                    ItemStack? stack = validStacks.First(stack => stack?.Collectible.Code == ingredientStack?.Collectible.Code);
                                    if (stack != null)
                                    {
                                        randomMeal.Add(stack.Clone());
                                        ingredient.MaxQuantity--;

                                        requestedIngredient = null;
                                        break;
                                    }
                                }
                            }

                            if (ingredient.MaxQuantity > 0 && api.World.Rand.NextDouble() < 0.5)
                            {
                                var stack = validStacks[api.World.Rand.Next(validStacks.Count)];

                                if (stack != null)
                                {
                                    randomMeal.Add(stack.Clone());
                                    ingredient.MaxQuantity--;

                                    break;
                                }
                            }
                        }
                    }

                    tries--;
                }
            }

            randomMeal.Shuffle(api.World.Rand);

            while (randomMeal.Count < slots) randomMeal.Add(null);
            return randomMeal.ToArray();
        }



        /// <summary>
        /// Serialized the alloy
        /// </summary>
        /// <param name="writer"></param>
        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Code!);
            writer.Write(Ingredients!.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }

            writer.Write(Shape == null);
            if (Shape != null) writer.Write(Shape.Base.ToString());

            PerishableProps!.ToBytes(writer);

            writer.Write(CooksInto != null);
            if (CooksInto != null) CooksInto.ToBytes(writer);

            writer.Write(IsFood);
        }

        /// <summary>
        /// Deserializes the alloy
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="resolver"></param>
        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Code = reader.ReadString();
            Ingredients = new CookingRecipeIngredient[reader.ReadInt32()];

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new CookingRecipeIngredient() { Code = null!, ValidStacks = null! };
                Ingredients[i].FromBytes(reader, resolver.ClassRegistry);
                Ingredients[i].Resolve(resolver, "[FromBytes]");
            }

            if (!reader.ReadBoolean())
            {
                Shape = new CompositeShape() { Base = new AssetLocation(reader.ReadString()) };
            }

            PerishableProps = new TransitionableProperties();
            PerishableProps.FromBytes(reader, resolver.ClassRegistry);

            if (reader.ReadBoolean())
            {
                CooksInto = new JsonItemStack();
                CooksInto.FromBytes(reader, resolver.ClassRegistry);
                CooksInto.Resolve(resolver, "[FromBytes]");
            }

            IsFood = reader.ReadBoolean();
        }

    }
    
}
