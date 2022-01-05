using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class AlloyRecipe : IByteSerializable
    {
        /// <summary>
        /// The ingredients for this alloy.
        /// </summary>
        public MetalAlloyIngredient[] Ingredients;

        /// <summary>
        /// The output for the alloy.
        /// </summary>
        public JsonItemStack Output;

        public bool Enabled = true;

        /// <summary>
        /// Makes a check to see if the input for the recipe is valid.
        /// </summary>
        /// <param name="inputStacks">The item inputs.</param>
        /// <param name="useSmeltedWhereApplicable">Whether or not items should be considered their smelted form as opposed their raw form.</param>
        /// <returns></returns>
        public bool Matches(ItemStack[] inputStacks, bool useSmeltedWhereApplicable = true)
        {
            List<MatchedSmeltableStackAlloy> mergedStacks = mergeAndCompareStacks(inputStacks, useSmeltedWhereApplicable);
            if (mergedStacks == null) return false;

            double totalOutputStacksize = 0;

            foreach (MatchedSmeltableStackAlloy matchedstack in mergedStacks)
            {
                totalOutputStacksize += matchedstack.stackSize;
            }

            foreach (MatchedSmeltableStackAlloy matchedstack in mergedStacks)
            {
                // So apparently double comparison is pretty bad. 0.2 < 0.2 can result true, so lets multiply by 10000 and compare ints
                double ratio = matchedstack.stackSize / totalOutputStacksize;
                int rationInt = (int)Math.Round(ratio * 10000);
                int min = (int)Math.Round(matchedstack.ingred.MinRatio * 10000);
                int max = (int)Math.Round(matchedstack.ingred.MaxRatio * 10000);

                if (rationInt < min || rationInt > max) return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves the ingredients that are used to their actual types in the world.
        /// </summary>
        /// <param name="world">The world accessor for data.</param>
        /// <param name="sourceForErrorLogging"></param>
        public void Resolve(IServerWorldAccessor world, string sourceForErrorLogging)
        {
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].Resolve(world, sourceForErrorLogging);
            }

            Output.Resolve(world, sourceForErrorLogging);
        }

        /// <summary>
        /// Gets the output amount of material in the resulting alloy.
        /// </summary>
        /// <param name="stacks"></param>
        /// <param name="useSmeltedWhereAppicable"></param>
        /// <returns></returns>
        public double GetTotalOutputQuantity(ItemStack[] stacks, bool useSmeltedWhereAppicable = true)
        {
            List<MatchedSmeltableStackAlloy> mergedStacks = mergeAndCompareStacks(stacks, useSmeltedWhereAppicable);
            if (mergedStacks == null) return 0;

            double totalOutputStacksize = 0;

            foreach (MatchedSmeltableStackAlloy matchedstack in mergedStacks)
            {
                totalOutputStacksize += matchedstack.stackSize;
            }

            return totalOutputStacksize;
        }


        List<MatchedSmeltableStackAlloy> mergeAndCompareStacks(ItemStack[] inputStacks, bool useSmeltedWhereApplicable)
        {
            List<MatchedSmeltableStackAlloy> mergedStacks = new List<MatchedSmeltableStackAlloy>();
            List<MetalAlloyIngredient> ingredients = new List<MetalAlloyIngredient>(this.Ingredients);

            for (int i = 0; i < inputStacks.Length; i++)
            {
                if (inputStacks[i] == null) continue;

                ItemStack stack = inputStacks[i];
                float stackSize = stack.StackSize;

                if (useSmeltedWhereApplicable && stack.Collectible.CombustibleProps?.SmeltedStack != null) 
                {
                    stackSize /= stack.Collectible.CombustibleProps.SmeltedRatio;
                    stack = stack.Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack;
                }

                bool exists = false;
                for (int j = 0; j < mergedStacks.Count; j++)
                {
                    if (stack.Class == mergedStacks[j].stack.Class && stack.Id == mergedStacks[j].stack.Id)
                    {
                        mergedStacks[j].stackSize += stackSize;
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    MetalAlloyIngredient ingred = getIgrendientFor(stack, ingredients);
                    if (ingred == null) return null;

                    mergedStacks.Add(new MatchedSmeltableStackAlloy() { stack = stack.Clone(), ingred = ingred, stackSize = stackSize });
                }
            }

            if (ingredients.Count > 0) return null;

            return mergedStacks;
        }

        MetalAlloyIngredient getIgrendientFor(ItemStack stack, List<MetalAlloyIngredient> ingredients)
        {
            if (stack == null) return null;

            for (int i = 0; i < ingredients.Count; i++)
            {
                ItemStack ingredientstack = ingredients[i].ResolvedItemstack;
                if (ingredientstack.Class == stack.Class && ingredientstack.Id == stack.Id)
                {
                    MetalAlloyIngredient ingred = ingredients[i];
                    ingredients.Remove(ingredients[i]);
                    return ingred;
                }
            }
            return null;
        }

        



        /// <summary>
        /// Serialized the alloy
        /// </summary>
        /// <param name="writer"></param>
        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }

            Output.ToBytes(writer);
        }

        /// <summary>
        /// Deserializes the alloy
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="resolver"></param>
        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Ingredients = new MetalAlloyIngredient[reader.ReadInt32()];

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new MetalAlloyIngredient();
                Ingredients[i].FromBytes(reader, resolver.ClassRegistry);
                Ingredients[i].Resolve(resolver, "[FromBytes]");
            }

            Output = new JsonItemStack();
            Output.FromBytes(reader, resolver.ClassRegistry);
            Output.Resolve(resolver, "[FromBytes]");
        }

    }


    internal class MatchedSmeltableStackAlloy
    {
        public ItemStack stack;
        public MetalAlloyIngredient ingred;

        public double stackSize;
           
    }

    public class MatchedSmeltableStack
    {
        public ItemStack output;
        
        public double stackSize;

    }
}
