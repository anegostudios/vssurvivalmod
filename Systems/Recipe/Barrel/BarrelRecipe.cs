using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent;

/// <summary>
/// Creates a recipe for use inside a barrel. Primarily used to craft with liquids. 
/// </summary>
/// <example>
/// <code language="json">
///{
///  "code": "compost",
///  "sealHours": 480,
///  "ingredients": [
///    {
///      "type": "item",
///      "code": "rot",
///      "litres": 64
///    }
///  ],
///  "output": {
///    "type": "item",
///    "code": "compost",
///    "stackSize": 16
///  }
///}</code></example>
[DocumentAsJson]
public class BarrelRecipe : RecipeBase, IByteSerializable, IConcreteCloneable<BarrelRecipe>
{
    #region From JSON
    /// <summary>
    /// The final output of this recipe.
    /// </summary>
    [DocumentAsJson("Required")]
    public BarrelOutputStack? Output { get; set; }

    /// <summary>
    /// How many in-game hours this recipe takes after sealing.
    /// </summary>
    [DocumentAsJson("Required")]
    public double SealHours { get; set; }

    /// <summary>
    /// Is used only to group recipes together. Recipes with same code will be displayed as one, but cycled through.
    /// </summary>
    [DocumentAsJson("Required")]
    public string? Code { get; set; }

    /// <summary>
    /// Defines the set of ingredients used inside the barrel. Barrels can have a maximum of one item and one liquid ingredient.
    /// </summary>
    [DocumentAsJson("Required")]
    public BarrelRecipeIngredient[]? Ingredients { get; set; }
    #endregion

    public override IEnumerable<IRecipeIngredient> RecipeIngredients => Ingredients ?? throw new InvalidOperationException($"Barrel recipe '{Name}' does not have ingredients specified");

    public override IRecipeOutput RecipeOutput => Output ?? throw new InvalidOperationException($"Barrel recipe '{Name}' does not have output specified");



    public override void OnParsed(IWorldAccessor world)
    {
        if (Ingredients == null) return;

        int ingredientIndex = 1;
        foreach (CraftingRecipeIngredient ingredient in Ingredients)
        {
            ingredient.Id ??= ingredientIndex++.ToString();
        }
    }

    public bool Matches(ItemSlot[] inputSlots, out int outputStackSize)
    {
        outputStackSize = 0;

        List<(ItemSlot slot, BarrelRecipeIngredient ingredient)> matched = PairInput(inputSlots);
        if (matched.Count == 0)
        {
            return false;
        }

        outputStackSize = GetOutputSize(matched);

        return outputStackSize >= 0;
    }

    public bool Matches(IPlayer forPlayer, ItemSlot[] inputSlots, out int outputStackSize)
    {
        outputStackSize = 0;

        if (!forPlayer.Entity.Api.Event.TriggerMatchesRecipe(forPlayer, this, inputSlots))
        {
            return false;
        }

        return Matches(inputSlots, out outputStackSize);
    }

    public bool TryCraftNow(ICoreAPI api, double nowSealedHours, ItemSlot[] inputSlots)
    {
        if (SealHours > 0 && nowSealedHours < SealHours)
        {
            return false;
        }

        List<(ItemSlot slot, BarrelRecipeIngredient ingredient)> matched = PairInput(inputSlots);

        int outputStackSize = GetOutputSize(matched);

        if (outputStackSize < 0 || Output?.ResolvedItemStack == null) return false;

        ItemStack mixedStack = Output.ResolvedItemStack.Clone();
        mixedStack.StackSize = outputStackSize;

        CarryOverFreshness(api, mixedStack, inputSlots);

        ItemStack? remainStack = null;
        foreach ((ItemSlot slot, BarrelRecipeIngredient ingredient) in matched)
        {
            if (ingredient.ConsumeQuantity == null || slot.Itemstack == null) continue;

            remainStack = slot.Itemstack;
            remainStack.StackSize -= (int)ingredient.ConsumeQuantity * (mixedStack.StackSize / Output.StackSize);
            if (remainStack.StackSize <= 0)
            {
                remainStack = null;
            }
            break;
        }

        ItemSlot inputItemSlot = inputSlots[0];
        ItemSlot liquidSlot = inputSlots[1];

        if (ShouldBeInLiquidSlot(mixedStack))
        {
            inputItemSlot.Itemstack = remainStack;
            liquidSlot.Itemstack = mixedStack;
        }
        else
        {
            liquidSlot.Itemstack = remainStack;
            inputItemSlot.Itemstack = mixedStack;
        }

        inputItemSlot.MarkDirty();
        liquidSlot.MarkDirty();

        return true;
    }

    public override void ToBytes(BinaryWriter writer)
    {
        base.ToBytes(writer);

        if (Code == null || Ingredients == null || Output == null)
        {
            throw new InvalidOperationException("Cannot serialize barrel recipes: some of the properties are null");
        }

        writer.Write(Code);
        writer.Write(Ingredients.Length);
        for (int i = 0; i < Ingredients.Length; i++)
        {
            Ingredients[i].ToBytes(writer);
        }

        Output.ToBytes(writer);

        writer.Write(SealHours);
    }

    public override void FromBytes(BinaryReader reader, IWorldAccessor resolver)
    {
        base.FromBytes(reader, resolver);

        Code = reader.ReadString();
        Ingredients = new BarrelRecipeIngredient[reader.ReadInt32()];

        for (int i = 0; i < Ingredients.Length; i++)
        {
            Ingredients[i] = new BarrelRecipeIngredient();
            Ingredients[i].FromBytes(reader, resolver);
            Ingredients[i].Resolve(resolver, "Barrel Recipe (FromBytes)");
        }

        Output = new BarrelOutputStack();
        Output.FromBytes(reader, resolver.ClassRegistry);
        Output.Resolve(resolver, "Barrel Recipe (FromBytes)");

        SealHours = reader.ReadDouble();
    }

    public override bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
    {
        bool resolved = true;

        if (Ingredients == null || Output == null)
        {
            world.Logger.Error($"Cannot resolve barrel recipe '{Name}', either Ingredients or Output are not specified");
            return false;
        }

        foreach (BarrelRecipeIngredient ingredient in Ingredients)
        {
            resolved &= ingredient.Resolve(world, sourceForErrorLogging);
        }

        resolved &= Output.Resolve(world, sourceForErrorLogging);

        return resolved;
    }

    public override BarrelRecipe Clone()
    {
        BarrelRecipe recipe = new();

        CloneTo(recipe);

        return recipe;
    }



    protected override void CloneTo(object recipe)
    {
        base.CloneTo(recipe);

        if (recipe is not BarrelRecipe barrelRecipe)
        {
            throw new ArgumentException("CloneTo should take object of same class or it subclass");
        }

        barrelRecipe.Output = Output?.Clone();
        barrelRecipe.SealHours = SealHours;
        barrelRecipe.Code = Code;
        if (Ingredients != null)
        {
            barrelRecipe.Ingredients = new BarrelRecipeIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                barrelRecipe.Ingredients[i] = Ingredients[i].Clone();
            }
        }
    }

    protected virtual bool ShouldBeInLiquidSlot(ItemStack? stack)
    {
        // Minor Fugly hack - copied from LiquidContainer.cs
        return stack?.ItemAttributes?["waterTightContainerProps"].Exists == true;
    }

    protected virtual List<(ItemSlot slot, BarrelRecipeIngredient ingredient)> PairInput(ItemSlot[] inputSlots)
    {
        int stackCount = 0;
        foreach (ItemSlot inputSlot in inputSlots)
        {
            if (!inputSlot.Empty)
            {
                stackCount++;
            }
        }

        if (Ingredients == null || stackCount != Ingredients.Length)
        {
            return [];
        }

        List<(ItemSlot slot, BarrelRecipeIngredient ingredient)> matched = [];
        List<BarrelRecipeIngredient> ingredientsToProcess = Ingredients.ToList();

        foreach (ItemSlot inputSlot in inputSlots)
        {
            if (inputSlot.Itemstack == null) continue;

            BarrelRecipeIngredient? ingredient = ingredientsToProcess.Find(ingredient => MatchStackToIngredient(inputSlot.Itemstack, ingredient));
            if (ingredient != null)
            {
                matched.Add((inputSlot, ingredient));
                ingredientsToProcess.Remove(ingredient);
            }
            else
            {
                return [];
            }
        }

        // We're missing ingredients
        if (matched.Count < Ingredients.Length)
        {
            return [];
        }

        return matched;
    }

    protected virtual int GetOutputSize(List<(ItemSlot slot, BarrelRecipeIngredient ingredient)> matched)
    {
        int outQuantityMul = -1;

        foreach ((ItemSlot slot, BarrelRecipeIngredient ingredient) in matched)
        {
            if (ingredient.ConsumeQuantity == null)
            {
                outQuantityMul = slot.StackSize / ingredient.Quantity;
            }
        }

        if (outQuantityMul == -1)
        {
            return -1;
        }

        foreach ((ItemSlot slot, BarrelRecipeIngredient ingredient) in matched)
        {
            if (ingredient.ConsumeQuantity == null)
            {
                // Input stack size must be equal or a multiple of the ingredient stack size
                if ((slot.StackSize % ingredient.Quantity) != 0) return -1;

                // Ingredients must be at the same ratio
                if (outQuantityMul != slot.StackSize / ingredient.Quantity) return -1;

            }
            else
            {
                // Must have same or more than the total crafted amount
                if (slot.StackSize < ingredient.Quantity * outQuantityMul) return -1;
            }
        }

        return Output.StackSize * outQuantityMul;
    }

    protected virtual void CarryOverFreshness(ICoreAPI api, ItemStack mixedStack, ItemSlot[] inputSlots)
    {
        TransitionableProperties[] props = mixedStack.Collectible.GetTransitionableProperties(api.World, mixedStack, null);
        TransitionableProperties? perishProps = props?.FirstOrDefault(p => p.Type == EnumTransitionType.Perish);

        if (perishProps != null)
        {
            CollectibleObject.CarryOverFreshness(api, inputSlots, [mixedStack], perishProps);
        }
    }
}
