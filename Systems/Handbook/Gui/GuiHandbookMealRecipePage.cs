using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    class IngredientMinMax
    {
        required public string Code;
        public int ExtraSlots;
        public float MinSat;
        public float MaxSat;
        public float MinHP;
        public float MaxHP;

        public IngredientMinMax Clone()
        {
            IngredientMinMax ingredient = new()
            {
                Code = Code,
                ExtraSlots = ExtraSlots,
                MinSat = MinSat,
                MaxSat = MaxSat,
                MinHP = MinHP,
                MaxHP = MaxHP
            };

            return ingredient;
        }
    }

    class HandbookMealNutritionFacts
    {
        required public HashSet<EnumFoodCategory> Categories;
        public float MinSatiety;
        public float MaxSatiety;
        public float MinHealth;
        public float MaxHealth;
    }

    public class GuiHandbookMealRecipePage : GuiHandbookPage
    {
        public CookingRecipe Recipe;
        public string pageCode;
        public string Title;
        readonly string titleCached;
        protected float secondsVisible = 0;

        protected const int TinyPadding = 2;   // Used to add tiny amounts of vertical padding after headings, so that things look less cramped
        protected const int TinyIndent = 2;    // Used to indent the page contents following headings - this subtly helps headings to stand out more
        protected const int MarginBottom = 3;  // Used following some (but not all) itemstack graphics
        protected const int SmallPadding = 7;  // Used to separate bullets in the Created By list
        protected const int MediumPadding = 14;  // Used before all headings

        public LoadedTexture? Texture;
        public override string PageCode => pageCode;

        public InventoryBase unspoilableInventory;
        public DummySlot dummySlot;
        HandbookMealNutritionFacts? cachedNutritionFacts;
        Dictionary<string, List<ItemStack>> cachedIngredientStacks;
        Dictionary<CookingRecipeIngredient, HashSet<ItemStack?>>? cachedValidStacks;
        bool isPie;
        int slots;
        ItemStack[] allStacks;

        ElementBounds? scissorBounds;

        public override string CategoryCode => "stack";
        public override bool IsDuplicate => false;

        public GuiHandbookMealRecipePage(ICoreClientAPI capi, CookingRecipe recipe, ItemStack[] allstacks, int slots = 4, bool isPie = false)
        {
            this.Recipe = recipe;
            this.pageCode = "handbook-mealrecipe-" + recipe.Code + (isPie ? "-pie" : "");
            this.isPie = isPie;
            this.slots = slots;
            this.allStacks = allstacks;

            unspoilableInventory = new CreativeInventoryTab(1, "not-used", null);
            if (isPie)
            {
                dummySlot = new(new(capi.World.BlockAccessor.GetBlock("pie-perfect")), unspoilableInventory);
                dummySlot.Itemstack!.Attributes.SetInt("pieSize", 4);
                dummySlot.Itemstack.Attributes.SetString("topCrustType", BlockPie.TopCrustTypes[capi.World.Rand.Next(BlockPie.TopCrustTypes.Length)].Code);
                dummySlot.Itemstack.Attributes.SetInt("bakeLevel", 2);
            }
            else
            {
                dummySlot = new(new(BlockMeal.RandomMealBowl(capi)), unspoilableInventory);
            }

            Title = Lang.Get(isPie ? ("pie-" + recipe.Code + "-perfect") : ("mealrecipe-name-" + recipe.Code));

            titleCached = Lang.Get(Title).ToSearchFriendly();
            cachedIngredientStacks = new Dictionary<string, List<ItemStack>>();
            cachedValidStacks = null;
        }

        [MemberNotNull(nameof(Texture), nameof(scissorBounds))]
        public void Recompose(ICoreClientAPI capi)
        {
            Texture?.Dispose();
            Texture = new TextTextureUtil(capi).GenTextTexture(Title, CairoFont.WhiteSmallText());

            scissorBounds = ElementBounds.FixedSize(50, 50);
            scissorBounds.ParentBounds = capi.Gui.WindowBounds;
        }

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            float size = (float)GuiElement.scaled(25);
            float pad = (float)GuiElement.scaled(10);

            IBlockMealContainer? mealBlock = dummySlot.Itemstack?.Collectible as IBlockMealContainer;

            if ((secondsVisible -= dt) <= 0)
            {
                secondsVisible = 1;
                if (isPie) dummySlot.Itemstack?.Attributes.SetString("topCrustType", BlockPie.TopCrustTypes[capi.World.Rand.Next(BlockPie.TopCrustTypes.Length)].Code);
                else dummySlot.Itemstack = new (BlockMeal.RandomMealBowl(capi));
                mealBlock?.SetContents(Recipe.Code!, dummySlot.Itemstack!, isPie ? BlockPie.GenerateRandomPie(capi, ref cachedValidStacks, Recipe) : Recipe.GenerateRandomMeal(capi, ref cachedValidStacks, allStacks, slots), 1);
            }

            if (Texture == null || scissorBounds == null)
            {
                Recompose(capi);
            }

            scissorBounds.fixedX = (pad + x - size / 2) / RuntimeEnv.GUIScale;
            scissorBounds.fixedY = (y - size / 2) / RuntimeEnv.GUIScale;
            scissorBounds.CalcWorldBounds();

            if (scissorBounds.InnerWidth <= 0 || scissorBounds.InnerHeight <= 0) return;

            capi.Render.PushScissor(scissorBounds, true);
            capi.Render.RenderItemstackToGui(dummySlot, x + pad + size / 2, y + size / 2, 100, size, ColorUtil.WhiteArgb, true, false, false);
            capi.Render.PopScissor();

            capi.Render.Render2DTexturePremultipliedAlpha(
                Texture.TextureId,
                (x + size + GuiElement.scaled(25)),
                y + size / 4 - GuiElement.scaled(3),
                Texture.Width,
                Texture.Height,
                50
            );
        }

        public override void Dispose()
        {
            Texture?.Dispose();
            Texture = null;
        }

        public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
        {
            RichTextComponentBase[] cmps = GetPageText(detailViewGui.Api, allstacks, openDetailPageFor);
            detailViewGui.AddRichtext(cmps, textBounds, "richtext");
        }

        protected virtual RichTextComponentBase[] GetPageText(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<RichTextComponentBase> components = new List<RichTextComponentBase>();

            addGeneralInfo(capi, allStacks, components);
            addIngredientLists(capi, allStacks, components, openDetailPageFor);
            addCookingDirections(capi, components);
            addRecipeNotes(capi, components);

            return components.ToArray();
        }

        protected void addGeneralInfo(ICoreClientAPI capi, ItemStack[] allStacks, List<RichTextComponentBase> components)
        {
            ItemStack mealBlock = dummySlot.Itemstack!.Clone();

            components.Add(new MealstackTextComponent(capi, ref cachedValidStacks, mealBlock, Recipe, 100, EnumFloat.Left, allStacks, null, slots, isPie) { PaddingRight = GuiElement.scaled(10), offX = -8});
            components.AddRange(VtmlUtil.Richtextify(capi, Title + "\n", CairoFont.WhiteSmallishText()));
            if (capi.Settings.Bool["extendedDebugInfo"] == true)
            {
                var font = CairoFont.WhiteDetailText();
                font.Color[3] = 0.5;
                components.AddRange(VtmlUtil.Richtextify(capi, "Page code:" + pageCode + "\n", font));
            }
            var nutritionFacts = getNutritionFacts(capi, allStacks, slots);

            float minSat = (float)Math.Round(nutritionFacts.MinSatiety);
            float maxSat = (float)Math.Round(nutritionFacts.MaxSatiety);
            float minHP = nutritionFacts.MinHealth;
            float maxHP = nutritionFacts.MaxHealth;

            string satCategories = "";
            if (nutritionFacts.Categories.Count > 0)
            {
                satCategories = Lang.Get("handbook-mealrecipe-nutritionfacts-satietycategories", string.Join(", ", nutritionFacts.Categories.Select(category => Lang.Get("foodcategory-" + category.ToString().ToLowerInvariant()))));
            }

            bool isSat = minSat != 0 || maxSat != 0;
            bool isHP = minHP > 0 || maxHP > 0;

            if (isSat || isHP) components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-mealrecipe-nutritionfacts") + "\n", CairoFont.WhiteSmallText()));
            if (isSat) components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-mealrecipe-nutritionfactsline-satiety", minSat, maxSat, satCategories) + "\n", CairoFont.WhiteDetailText()));
            if (isHP) components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-mealrecipe-nutritionfactsline-health", (minHP >= 0 ? "+" : "") + minHP, (maxHP >= 0 ? "+" : "") + maxHP) + "\n", CairoFont.WhiteDetailText()));
        }

        void addIngredientLists(ICoreClientAPI capi, ItemStack[] allStacks, List<RichTextComponentBase> components, ActionConsumable<string> openDetailPageFor)
        {
            List<CookingRecipeIngredient> ingredients = isPie ? Recipe.Ingredients!.ToList() : CombinedIngredientsList();

            bool haveText = components.Count > 0;
            CollectibleBehaviorHandbookTextAndExtraInfo.AddHeading(components, capi, "handbook-mealrecipe-requiredingredients", ref haveText);

            foreach (var ingredient in ingredients.Where(ingredient => ingredient.MinQuantity > 0))
            {
                List<ItemStack> ingredientStacks = getIngredientStacks(capi, ingredient, allStacks);

                if (ingredientStacks.Count > 0)
                {
                    addIngredientHeading(components, capi, ingredient, "handbook-mealrecipe" + (isPie ? "-pie" : "-") + "ingredients", ingredientStacks, openDetailPageFor);
                }
            }

            var optionalIngredients = ingredients.Where(ingredient => ingredient.MinQuantity == 0);
            if (optionalIngredients.Count() > 0)
            {
                haveText = components.Count > 0;
                CollectibleBehaviorHandbookTextAndExtraInfo.AddHeading(components, capi, "handbook-mealrecipe-optionalingredients", ref haveText);
            }

            foreach (var ingredient in optionalIngredients)
            {
                List<ItemStack> ingredientStacks = getIngredientStacks(capi, ingredient, allStacks);

                if (ingredientStacks.Count > 0)
                {
                    addIngredientHeading(components, capi, ingredient, "handbook-mealrecipe" + (isPie ? "-pie" : "-") + "ingredients", ingredientStacks, openDetailPageFor);
                }
            }
        }

        void addIngredientHeading(List<RichTextComponentBase> components, ICoreClientAPI capi, CookingRecipeIngredient ingredient, string ingredientHeader, List<ItemStack> ingredientStacks, ActionConsumable<string> openDetailPageFor)
        {
            var verticalSpace = new ClearFloatTextComponent(capi, TinyPadding + 1);
            int minQuantity = ingredient.MinQuantity;
            int maxQuantity = ingredient.MaxQuantity;
            if (maxQuantity == minQuantity) minQuantity = 0;
            ingredientHeader = Lang.Get(ingredientHeader, minQuantity, maxQuantity, Lang.Get("handbook-mealingredient-" + ingredient.TypeName));

            components.Add(verticalSpace);
            CollectibleBehaviorHandbookTextAndExtraInfo.AddSubHeading(components, capi, null, ingredientHeader, null);

            int firstPadding = TinyPadding;
            while (ingredientStacks.Count > 0)
            {
                ItemStack istack = ingredientStacks[0];
                ingredientStacks.RemoveAt(0);
                if (istack == null) continue;

                SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, istack, ingredientStacks, 30, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                comp.PaddingLeft = firstPadding;
                comp.ShowStackSize = true;
                firstPadding = 0;
                components.Add(comp);
            }

            components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
        }

        protected void addCookingDirections(ICoreClientAPI capi, List<RichTextComponentBase> components)
        {
            string directionsText = Lang.GetMatchingIfExists("handbook-mealrecipe-directionstext-" + Recipe.Code);

            if (isPie)
            {
                directionsText ??= Lang.GetMatchingIfExists("handbook-mealrecipe-directionstext-" + Recipe.Code + "-pie");
                directionsText ??= Lang.GetMatchingIfExists("handbook-mealrecipe-directionstext-pie");
            }

            directionsText ??= Lang.Get("handbook-mealrecipe-directionstext");

            components.Add(new ClearFloatTextComponent(capi, MediumPadding));
            components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-mealrecipe" + (isPie ? "-pie" : "-") + "directions", directionsText) + "\n", CairoFont.WhiteSmallText()));
        }

        protected void addRecipeNotes(ICoreClientAPI capi, List<RichTextComponentBase> components)
        {
            string notesText = Lang.GetMatchingIfExists("handbook-mealrecipe-notestext-" + Recipe.Code);
            if (isPie)
            {
                notesText ??= Lang.GetMatchingIfExists("handbook-mealrecipe-notestext-" + Recipe.Code + "-pie");
                notesText ??= Lang.GetMatchingIfExists("handbook-mealrecipe-notestext-pie");
            }

            if (notesText == null) return;

            components.Add(new ClearFloatTextComponent(capi, MediumPadding));

            components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get("handbook-mealrecipe-notes", notesText), CairoFont.WhiteSmallText()));
        }

        HandbookMealNutritionFacts getNutritionFacts(ICoreClientAPI capi, ItemStack[] allStacks, int slots = 4)
        {
            if (cachedNutritionFacts == null)
            {
                float lowestSat, highestSat, lowestHP, highestHP;
                lowestSat = highestSat = lowestHP = highestHP = 0;

                List<IngredientMinMax> ingredientMinMax = new();
                HashSet<EnumFoodCategory> categories = new();
                List<CookingRecipeIngredient> ingredientsToSkip = new();

                foreach (var rIngredient in isPie ? Recipe.Ingredients!.ToList() : CombinedIngredientsList())
                {
                    var ingredient = rIngredient.Clone();
                    List<ItemStack> ingredientStacks = getIngredientStacks(capi, ingredient, allStacks);

                    float ingredientMinSat, ingredientMaxSat, ingredientMinHP, ingredientMaxHP;

                    ingredientMinSat = ingredientMinHP = float.MaxValue;
                    ingredientMaxSat = ingredientMaxHP = float.MinValue;

                    bool skip = true;
                    while (ingredientStacks.Count > 0)
                    {
                        ItemStack istack = ingredientStacks[0];
                        ingredientStacks.RemoveAt(0);

                        if (istack == null) continue;

                        float statModifier = istack.StackSize;
                        CookingRecipeIngredient? ingred = Recipe.GetIngrendientFor(istack, ingredientsToSkip.ToArray());
                        istack = ingred?.GetMatchingStack(istack)?.CookedStack?.ResolvedItemstack.Clone() ?? istack;
                        istack.StackSize = (int)statModifier;

                        FoodNutritionProperties? stackProps = BlockMeal.GetIngredientStackNutritionProperties(capi.World, istack, capi.World.Player.Entity);

                        if (BlockLiquidContainerBase.GetContainableProps(istack) is WaterTightContainableProps props)
                        {
                            statModifier = statModifier / props.ItemsPerLitre / ingredient.PortionSizeLitres;
                        }

                        if (stackProps != null)
                        {
                            ingredientMinSat = GameMath.Min(ingredientMinSat, stackProps.Satiety * statModifier);
                            ingredientMaxSat = GameMath.Max(ingredientMaxSat, stackProps.Satiety * statModifier);

                            ingredientMinHP = GameMath.Min(ingredientMinHP, stackProps.Health * statModifier);
                            ingredientMaxHP = GameMath.Max(ingredientMaxHP, stackProps.Health * statModifier);
                        }
                        skip &= stackProps == null;

                        if (stackProps?.FoodCategory is not EnumFoodCategory.NoNutrition and not EnumFoodCategory.Unknown and not null) categories.Add(stackProps.FoodCategory);
                    }

                    slots -= ingredient.MinQuantity;
                    if (skip) continue;

                    int minQuantity = ingredient.MinQuantity;
                    int possibleSlots = ingredient.MaxQuantity - minQuantity;

                    while (minQuantity > 0)
                    {
                        lowestSat += ingredientMinSat;
                        highestSat += ingredientMaxSat;
                        lowestHP += ingredientMinHP;
                        highestHP += ingredientMaxHP;
                        minQuantity--;
                    }

                    if (possibleSlots > 0)
                    {
                        ingredientMinMax.Add(new IngredientMinMax()
                        {
                            Code = ingredient.Code,
                            ExtraSlots = possibleSlots,
                            MinSat = ingredientMinSat,
                            MaxSat = ingredientMaxSat,
                            MinHP = ingredientMinHP,
                            MaxHP = ingredientMaxHP,
                        });
                    }

                    ingredientsToSkip.Add(rIngredient);
                }

                if (ingredientMinMax.Count > 0 && slots > 0)
                {
                    List<IngredientMinMax> satMin = [.. ingredientMinMax.OrderBy(ingredient => ingredient.MinSat).Select(ingredient => ingredient.Clone())];
                    List<IngredientMinMax> satMax = [.. ingredientMinMax.OrderBy(ingredient => ingredient.MaxSat).Select(ingredient => ingredient.Clone())];
                    List<IngredientMinMax> hpMin = [.. ingredientMinMax.OrderBy(ingredient => ingredient.MinHP).Select(ingredient => ingredient.Clone())];
                    List<IngredientMinMax> hpMax = [.. ingredientMinMax.OrderBy(ingredient => ingredient.MaxHP).Select(ingredient => ingredient.Clone())];

                    while (slots > 0)
                    {
                        float slotMinSat, slotMaxSat, slotMinHP, slotMaxHP;
                        slotMinSat = slotMinHP = slotMaxSat = slotMaxHP = 0;

                        if (satMin.First().MinSat < 0)
                        {
                            var ingredient = satMin.First();
                            slotMinSat = ingredient.MinSat;
                            ingredient.ExtraSlots -= 1;
                            if (ingredient.ExtraSlots == 0) satMin.Remove(ingredient);
                        }

                        if (satMax.Last().MaxSat > 0)
                        {
                            var ingredient = satMax.Last();
                            slotMaxSat = ingredient.MaxSat;
                            ingredient.ExtraSlots -= 1;
                            if (ingredient.ExtraSlots == 0) satMax.Remove(ingredient);
                        }

                        if (hpMin.First().MinHP < 0)
                        {
                            var ingredient = hpMin.First();
                            slotMinHP = ingredient.MinHP;
                            ingredient.ExtraSlots -= 1;
                            if (ingredient.ExtraSlots == 0) hpMin.Remove(ingredient);
                        }

                        if (hpMax.Last().MaxHP > 0)
                        {
                            var ingredient = hpMax.Last();
                            slotMaxHP = ingredient.MaxHP;
                            ingredient.ExtraSlots -= 1;
                            if (ingredient.ExtraSlots == 0) hpMax.Remove(ingredient);
                        }

                        if (slotMinSat < 0) lowestSat += slotMinSat;
                        if (slotMaxSat > 0) highestSat += slotMaxSat;
                        if (slotMinHP < 0) lowestHP += slotMinHP;
                        if (slotMaxHP > 0) highestHP += slotMaxHP;
                        slots--;
                    }
                }

                cachedNutritionFacts = new HandbookMealNutritionFacts() { Categories = categories, MinSatiety = lowestSat, MaxSatiety = highestSat, MinHealth = lowestHP, MaxHealth = highestHP };
            }

            return cachedNutritionFacts;
        }

        List<ItemStack> getIngredientStacks(ICoreClientAPI capi, CookingRecipeIngredient ingredient, ItemStack[] allStacks)
        {
            if (!cachedIngredientStacks.TryGetValue(ingredient.Code, out List<ItemStack>? cachedStacks))
            {
                HashSet<ItemStack> ingredientStacks = new HashSet<ItemStack>();

                foreach (var aStack in allStacks)
                {
                    if (ingredient.GetMatchingStack(aStack) is CookingRecipeStack jstack)
                    {
                        ItemStack istack = aStack.Clone();
                        istack.StackSize = jstack.StackSize;

                        if (BlockLiquidContainerBase.GetContainableProps(istack) is WaterTightContainableProps props)
                        {
                            istack.StackSize = (int)(props.ItemsPerLitre * ingredient.PortionSizeLitres) * jstack.StackSize;
                        }

                        ingredientStacks.Add(istack);
                    }
                }

                cachedIngredientStacks.Add(ingredient.Code, ingredientStacks.ToList());
                return [.. ingredientStacks.Select(stack => stack.Clone())];
            }

            return [.. cachedStacks.Select(stack => stack.Clone())];
        }

        protected List<CookingRecipeIngredient> CombinedIngredientsList()
        {
            List<CookingRecipeIngredient> combinedIngredients = new List<CookingRecipeIngredient>();

            foreach (var ingredient in Recipe.Ingredients!)
            {
                if (combinedIngredients.Count > 0)
                {
                    List<AssetLocation> valCodes = new List<AssetLocation>();

                    foreach (var vstack in ingredient.ValidStacks) valCodes.Add(vstack.Code);

                    bool add = true;
                    foreach (var comIngred in combinedIngredients)
                    {
                        List<AssetLocation> combinedCodes = new List<AssetLocation>();

                        foreach (var vstack in comIngred.ValidStacks) combinedCodes.Add(vstack.Code);

                        valCodes.Sort();
                        combinedCodes.Sort();

                        bool matches = true;
                        if (valCodes.Count != combinedCodes.Count) matches = false;

                        for (int i = 0;  i < combinedCodes.Count; i++)
                        {
                            if (i < valCodes.Count && valCodes[i] != combinedCodes[i]) matches = false;
                        }

                        if (matches)
                        {
                            comIngred.MinQuantity += ingredient.MinQuantity;
                            comIngred.MaxQuantity += ingredient.MaxQuantity;

                            add = false;
                        }
                    }
                    if (add) combinedIngredients.Add(ingredient.Clone());

                }
                else combinedIngredients.Add(ingredient.Clone());
            }

            return combinedIngredients;
        }

        public override float GetTextMatchWeight(string searchText)
        {
            string specialKeywords = Lang.Get("handbook-mealrecipe-" + (isPie ? "pie" : "meal") + "searchkeywords");
            if (titleCached.Equals(searchText, StringComparison.InvariantCultureIgnoreCase)) return 4;
            if (titleCached.StartsWith(searchText + " ", StringComparison.InvariantCultureIgnoreCase)) return 3.5f;
            if (titleCached.StartsWith(searchText, StringComparison.InvariantCultureIgnoreCase)) return 3f;
            if (titleCached.CaseInsensitiveContains(searchText)) return 2.75f;
            if (specialKeywords.CaseInsensitiveContains(searchText)) return 2.5f;
            return 0;
        }
    }
}
