using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BarrelRecipeIngredient : CraftingRecipeIngredient
    {
        /// <summary>
        /// If set, the barrel may contain more, but it gets consumed by this amount
        /// </summary>
        public int? ConsumeQuantity = null;

        /// <summary>
        /// If the ingredient is a liquid, will use this value instead of Quantity
        /// </summary>
        public float Litres = -1;

        /// <summary>
        /// If the ingredient is a liquid and if set, the barrel may contain more, but it gets consumed by this amount
        /// </summary>
        public float? ConsumeLitres;

        public new BarrelRecipeIngredient Clone()
        {
            BarrelRecipeIngredient stack = new BarrelRecipeIngredient()
            {
                Code = Code.Clone(),
                Type = Type,
                Name = Name,
                Quantity = Quantity,
                ConsumeQuantity = ConsumeQuantity,
                ConsumeLitres = ConsumeLitres,
                IsWildCard = IsWildCard,
                IsTool = IsTool,
                Litres = Litres,
                AllowedVariants = AllowedVariants == null ? null : (string[])AllowedVariants.Clone(),
                ResolvedItemstack = ResolvedItemstack?.Clone(),
                ReturnedStack = ReturnedStack?.Clone()
            };

            if (Attributes != null) stack.Attributes = Attributes.Clone();

            return stack;
        }

        public override void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            base.FromBytes(reader, resolver);

            bool isset = reader.ReadBoolean();
            if (isset)
            {
                ConsumeQuantity = reader.ReadInt32();
            } else
            {
                ConsumeQuantity = null;
            }

            isset = reader.ReadBoolean();
            if (isset)
            {
                ConsumeLitres = reader.ReadSingle();
            }
            else
            {
                ConsumeLitres = null;
            }

            Litres = reader.ReadSingle();
        }

        public override void ToBytes(BinaryWriter writer)
        {
            base.ToBytes(writer);

            if (ConsumeQuantity != null)
            {
                writer.Write(true);
                writer.Write((int)ConsumeQuantity);
            } else
            {
                writer.Write(false);
            }

            if (ConsumeLitres != null)
            {
                writer.Write(true);
                writer.Write((float)ConsumeLitres);
            }
            else
            {
                writer.Write(false);
            }

            writer.Write(Litres);
        }
    }

    public class BarrelOutputStack : JsonItemStack
    {
        public float Litres;
        public override void FromBytes(BinaryReader reader, IClassRegistryAPI instancer)
        {
            base.FromBytes(reader, instancer);

            Litres = reader.ReadSingle();
        }
        public override void ToBytes(BinaryWriter writer)
        {
            base.ToBytes(writer);

            writer.Write(Litres);
        }

        public new BarrelOutputStack Clone()
        {
            BarrelOutputStack stack = new BarrelOutputStack()
            {
                Code = Code.Clone(),
                ResolvedItemstack = ResolvedItemstack?.Clone(),
                StackSize = StackSize,
                Type = Type,
                Litres = Litres
            };

            if (Attributes != null) stack.Attributes = Attributes.Clone();

            return stack;
        }
    }

    public class BarrelRecipe : IByteSerializable, IRecipeBase<BarrelRecipe>
    {
        public int RecipeId;

        /// <summary>
        /// ...or alternatively for recipes with multiple ingredients
        /// </summary>
        public BarrelRecipeIngredient[] Ingredients;

        public BarrelOutputStack Output;

        public AssetLocation Name { get; set; }
        public bool Enabled { get; set; } = true;


        public string Code;
        public double SealHours;


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



        List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> pairInput(ItemSlot[] inputStacks)
        {
            List<BarrelRecipeIngredient> ingredientList = new List<BarrelRecipeIngredient>(Ingredients);

            Queue<ItemSlot> inputSlotsList = new Queue<ItemSlot>();
            foreach (var val in inputStacks) if (!val.Empty) inputSlotsList.Enqueue(val);

            if (inputSlotsList.Count != Ingredients.Length) return null;

            List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>>();

            while (inputSlotsList.Count > 0)
            {
                ItemSlot inputSlot = inputSlotsList.Dequeue();
                bool found = false;

                for (int i = 0; i < ingredientList.Count; i++)
                {
                    BarrelRecipeIngredient ingred = ingredientList[i];

                    if (ingred.SatisfiesAsIngredient(inputSlot.Itemstack))
                    {
                        matched.Add(new KeyValuePair<ItemSlot, BarrelRecipeIngredient>(inputSlot, ingred));
                        found = true;
                        ingredientList.RemoveAt(i);
                        break;
                    }
                }

                if (!found) return null;
            }

            // We're missing ingredients
            if (ingredientList.Count > 0)
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
                if (!ingred.Code.Path.Contains("*")) continue;

                int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;

                List<string> codes = new List<string>();

                if (ingred.Type == EnumItemClass.Block)
                {
                    for (int i = 0; i < world.Blocks.Count; i++)
                    {
                        if (world.Blocks[i].Code == null || world.Blocks[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                        {
                            string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);

                        }
                    }
                }
                else
                {
                    for (int i = 0; i < world.Items.Count; i++)
                    {
                        if (world.Items[i].Code == null || world.Items[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Items[i].Code))
                        {
                            string code = world.Items[i].Code.Path.Substring(wildcardStartLen);
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
