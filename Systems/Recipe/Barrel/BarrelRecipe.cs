using System.Collections.Generic;
using System.IO;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
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
    public class BarrelRecipe : IByteSerializable, IRecipeBase<BarrelRecipe>
    {
        /// <summary>
        /// <!--<jsonoptional>Obsolete</jsonoptional>-->
        /// Unused. Defines an ID for the recipe.
        /// </summary>
        [DocumentAsJson] public int RecipeId;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// Defines the set of ingredients used inside the barrel. Barrels can have a maximum of one item and one liquid ingredient.
        /// </summary>
        [DocumentAsJson] public BarrelRecipeIngredient[] Ingredients;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// The final output of this recipe.
        /// </summary>
        [DocumentAsJson] public BarrelOutputStack Output;

        /// <summary>
        /// <!--<jsonoptional>Obsolete</jsonoptional>-->
        /// Unused. Defines a name for the recipe.
        /// </summary>
        [DocumentAsJson] public AssetLocation Name { get; set; }

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>True</jsondefault>-->
        /// Should this recipe be loaded by the recipe loader?
        /// </summary>
        [DocumentAsJson] public bool Enabled { get; set; } = true;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// A code for this recipe, used to create an entry in the handbook.
        /// </summary>
        [DocumentAsJson] public string Code;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// How many in-game hours this recipe takes after sealing.
        /// </summary>
        [DocumentAsJson] public double SealHours;

        IRecipeIngredient[] IRecipeBase<BarrelRecipe>.Ingredients => Ingredients;
        IRecipeOutput IRecipeBase<BarrelRecipe>.Output => Output;

        public bool Matches(ItemSlot[] inputSlots, out int outputStackSize)
        {
            outputStackSize = 0;

            List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> matched = pairInput(inputSlots);
            if (matched == null) return false;

            outputStackSize = getOutputSize(matched);

            return outputStackSize >= 0;
        }



        private List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> pairInput(ItemSlot[] inputStacks)
        {
            int stackCount = 0;
            foreach (var val in inputStacks) if (!val.Empty) stackCount++;
            var Ingredients = this.Ingredients;
            if (stackCount != Ingredients.Length) return null;

            int ingredientDone = -1;   // BarrelReceipe only has 2 ingredients, solid and liquid, so we only need 1 ingredientDone variable
            List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> matched = null;

            foreach (ItemSlot inputSlot in inputStacks)
            {
                if (inputSlot.Empty) continue;
                bool slotMatched = false;

                int i = 0;
                for (; i < Ingredients.Length; i++)
                {
                    if (i == ingredientDone) continue;
                    if (Ingredients[i].SatisfiesAsIngredient(inputSlot.Itemstack))
                    {
                        matched ??= new ();
                        matched.Add(new KeyValuePair<ItemSlot, BarrelRecipeIngredient>(inputSlot, Ingredients[i]));
                        ingredientDone = i;
                        slotMatched = true;
                        break;
                    }
                }

                if (!slotMatched) return null;
            }

            // We're missing ingredients
            if (matched.Count < Ingredients.Length)
            {
                return null;
            }

            return matched;
        }

        int getOutputSize(List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> matched)
        {
            int outQuantityMul = -1;

            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                BarrelRecipeIngredient ingred = val.Value;

                if (ingred.ConsumeQuantity == null)
                {
                    outQuantityMul = inputSlot.StackSize / ingred.Quantity;
                }
            }

            if (outQuantityMul == -1)
            {
                return -1;
            }


            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                BarrelRecipeIngredient ingred = val.Value;

                if (ingred.ConsumeQuantity == null)
                {
                    // Input stack size must be equal or a multiple of the ingredient stack size
                    if ((inputSlot.StackSize % ingred.Quantity) != 0) return -1;
                    
                    // Ingredients must be at the same ratio
                    if (outQuantityMul != inputSlot.StackSize / ingred.Quantity) return -1;

                }
                else
                {
                    // Must have same or more than the total crafted amount
                    if (inputSlot.StackSize < ingred.Quantity * outQuantityMul) return -1;
                }
            }

            return Output.StackSize * outQuantityMul;
        }


        public bool TryCraftNow(ICoreAPI api, double nowSealedHours, ItemSlot[] inputslots)
        {
            if (SealHours > 0 && nowSealedHours < SealHours) return false;

            var matched = pairInput(inputslots);

            ItemStack mixedStack = Output.ResolvedItemstack.Clone();
            mixedStack.StackSize = getOutputSize(matched);

            if (mixedStack.StackSize < 0) return false;

            // Carry over freshness
            TransitionableProperties[] props = mixedStack.Collectible.GetTransitionableProperties(api.World, mixedStack, null);
            TransitionableProperties perishProps = props != null && props.Length > 0 ? props[0] : null;

            if (perishProps != null)
            {
                CollectibleObject.CarryOverFreshness(api, inputslots, new ItemStack[] { mixedStack }, perishProps);
            }

            ItemStack remainStack = null;
            foreach (var val in matched)
            {
                if (val.Value.ConsumeQuantity != null)
                {
                    remainStack = val.Key.Itemstack;
                    remainStack.StackSize -= (int)val.Value.ConsumeQuantity * (mixedStack.StackSize / Output.StackSize);
                    if (remainStack.StackSize <= 0)
                    {
                        remainStack = null;
                    }
                    break;
                }
            }

            // Slot 0: Input/Item slot
            // Slot 1: Liquid slot
            if (shouldBeInLiquidSlot(mixedStack))
            {
                inputslots[0].Itemstack = remainStack;
                inputslots[1].Itemstack = mixedStack;
            }
            else
            {
                inputslots[1].Itemstack = remainStack;
                inputslots[0].Itemstack = mixedStack;
            }

            inputslots[0].MarkDirty();
            inputslots[1].MarkDirty();

            return true;
        }




        // Minor Fugly hack - copied from LiquidContainer.cs
        public bool shouldBeInLiquidSlot(ItemStack stack)
        {
            return stack?.ItemAttributes?["waterTightContainerProps"].Exists == true;
        }



        /// <summary>
        /// Serialized the alloy
        /// </summary>
        /// <param name="writer"></param>
        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Code);
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }

            Output.ToBytes(writer);

            writer.Write(SealHours);
        }

        /// <summary>
        /// Deserializes the alloy
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="resolver"></param>
        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
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



        /// <summary>
        /// Resolves Wildcards in the ingredients
        /// </summary>
        /// <param name="world"></param>
        /// <returns></returns>
        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();

            if (Ingredients == null || Ingredients.Length == 0) return mappings;

            foreach (var ingred in Ingredients)
            {
                if (!ingred.Code.Path.Contains('*')) continue;

                int wildcardStartLen = ingred.Code.Path.IndexOf('*');
                int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;

                List<string> codes = new List<string>();

                if (ingred.Type == EnumItemClass.Block)
                {
                    foreach (Block block in world.Blocks)
                    {
                        if (block.IsMissing) continue;   // BlockList already performs the null check for us, in its enumerator

                        if (WildcardUtil.Match(ingred.Code, block.Code))
                        {
                            string code = block.Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);

                        }
                    }
                }
                else
                {
                    foreach (Item item in world.Items)
                    {
                        if (item.Code == null || item.IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, item.Code))
                        {
                            string code = item.Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);
                        }
                    }
                }

                mappings[ingred.Name ?? "wildcard"+mappings.Count] = codes.ToArray();
            }

            return mappings;
        }



        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            bool ok = true;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                var ingred = Ingredients[i];
                bool iOk = ingred.Resolve(world, sourceForErrorLogging);
                ok &= iOk;

                if (iOk)
                {
                    var lprops = BlockLiquidContainerBase.GetContainableProps(ingred.ResolvedItemstack);
                    if (lprops != null)
                    {
                        if (ingred.Litres < 0)
                        {
                            if (ingred.Quantity > 0)
                            {
                                world.Logger.Warning("Barrel recipe {0}, ingredient {1} does not define a litres attribute but a quantity, will assume quantity=litres for backwards compatibility.", sourceForErrorLogging, ingred.Code);
                                ingred.Litres = ingred.Quantity;
                                ingred.ConsumeLitres = ingred.ConsumeQuantity;
                            } else ingred.Litres = 1;
                            
                        }

                        ingred.Quantity = (int)(lprops.ItemsPerLitre * ingred.Litres);
                        if (ingred.ConsumeLitres != null)
                        {
                            ingred.ConsumeQuantity = (int)(lprops.ItemsPerLitre * ingred.ConsumeLitres);
                        }
                    }
                }
            }

            ok &= Output.Resolve(world, sourceForErrorLogging);

            if (ok)
            {
                var lprops = BlockLiquidContainerBase.GetContainableProps(Output.ResolvedItemstack);
                if (lprops != null)
                {
                    if (Output.Litres < 0)
                    {
                        if (Output.Quantity > 0)
                        {
                            world.Logger.Warning("Barrel recipe {0}, output {1} does not define a litres attribute but a stacksize, will assume stacksize=litres for backwards compatibility.", sourceForErrorLogging, Output.Code);
                            Output.Litres = Output.Quantity;
                        }
                        else Output.Litres = 1;

                    }

                    Output.Quantity = (int)(lprops.ItemsPerLitre * Output.Litres);
                }
            }

            return ok;
        }

        public BarrelRecipe Clone()
        {
            BarrelRecipeIngredient[] ingredients = new BarrelRecipeIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                ingredients[i] = Ingredients[i].Clone();
            }

            return new BarrelRecipe()
            {
                SealHours = SealHours,
                Output = Output.Clone(),
                Code = Code,
                Enabled = Enabled,
                Name = Name,
                RecipeId = RecipeId,
                Ingredients = ingredients
            };
        }
    }
    
}
