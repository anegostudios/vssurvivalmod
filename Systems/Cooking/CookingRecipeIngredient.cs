using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class CookingRecipeStack : JsonItemStack
    {
        public string ShapeElement;
        public string[] TextureMapping;

        public JsonItemStack CookedStack;

        public override void FromBytes(BinaryReader reader, IClassRegistryAPI instancer)
        {
            base.FromBytes(reader, instancer);

            if (!reader.ReadBoolean())
            {
                ShapeElement = reader.ReadString();
            }

            if (!reader.ReadBoolean())
            {
                TextureMapping = new string[] { reader.ReadString(), reader.ReadString() };
            }

            if (!reader.ReadBoolean())
            {
                CookedStack = new JsonItemStack();
                CookedStack.FromBytes(reader, instancer);
            }
        }

        public override void ToBytes(BinaryWriter writer)
        {
            base.ToBytes(writer);

            writer.Write(ShapeElement == null);
            if (ShapeElement != null) writer.Write(ShapeElement);

            writer.Write(TextureMapping == null);
            if (TextureMapping != null) { writer.Write(TextureMapping[0]); writer.Write(TextureMapping[1]); }

            writer.Write(CookedStack == null);
            if (CookedStack != null) { CookedStack.ToBytes(writer); }
        }

        public new CookingRecipeStack Clone()
        {
            CookingRecipeStack stack = new CookingRecipeStack()
            {
                Code = Code.Clone(),
                ResolvedItemstack = ResolvedItemstack?.Clone(),
                StackSize = StackSize,
                Type = Type,
                TextureMapping = (string[])TextureMapping?.Clone(),
                CookedStack = CookedStack?.Clone()
            };

            if (Attributes != null) stack.Attributes = Attributes.Clone();

            stack.ShapeElement = ShapeElement;

            return stack;
        }
        
    }

    public class CookingRecipeIngredient
    {
        /// <summary>
        /// The code for the recipe ingredient.
        /// </summary>
        public string Code;

        /// <summary>
        /// The minimum quantity required for the given ingredient.
        /// </summary>
        public int MinQuantity;

        /// <summary>
        /// The maximum quantity required for the given ingredient.
        /// </summary>
        public int MaxQuantity;


        public float PortionSizeLitres;

        /// <summary>
        /// The world accessor for the ingredient.
        /// </summary>
        public IWorldAccessor world;
        
        public CookingRecipeStack[] ValidStacks;

        public virtual void FromBytes(BinaryReader reader, IClassRegistryAPI instancer)
        {
            Code = reader.ReadString();
            MinQuantity = reader.ReadInt32();
            MaxQuantity = reader.ReadInt32();
            PortionSizeLitres = reader.ReadSingle();

            int q = reader.ReadInt32();
            ValidStacks = new CookingRecipeStack[q];
            for (int i = 0; i < q; i++)
            {
                ValidStacks[i] = new CookingRecipeStack();
                ValidStacks[i].FromBytes(reader, instancer);
            }

        }

        public virtual void ToBytes(BinaryWriter writer)
        {
            writer.Write(Code);
            writer.Write(MinQuantity);
            writer.Write(MaxQuantity);
            writer.Write(PortionSizeLitres);
            
            writer.Write(ValidStacks.Length);
            for (int i = 0; i < ValidStacks.Length; i++)
            {
                ValidStacks[i].ToBytes(writer);
            }
        }


        /// <summary>
        /// Creates a deep copy of this object
        /// </summary>
        /// <returns></returns>
        public CookingRecipeIngredient Clone()
        {
            CookingRecipeIngredient ingredient = new CookingRecipeIngredient()
            {
                Code = Code,
                MinQuantity = MinQuantity,
                MaxQuantity = MaxQuantity,
                PortionSizeLitres = PortionSizeLitres
            };

            ingredient.ValidStacks = new CookingRecipeStack[ValidStacks.Length];
            for (int i = 0; i < ValidStacks.Length; i++)
            {
                ingredient.ValidStacks[i] = ValidStacks[i].Clone();
            }

            return ingredient;
        }

        /// <summary>
        /// Checks to see whether or not the itemstack matches the ingredient.
        /// </summary>
        /// <param name="inputStack"></param>
        /// <returns></returns>
        public bool Matches(ItemStack inputStack)
        {
            return GetMatchingStack(inputStack) != null;
        }

        /// <summary>
        /// Attempts to get a matching ingredient stack for the given input.
        /// </summary>
        /// <param name="inputStack"></param>
        /// <returns></returns>
        public CookingRecipeStack GetMatchingStack(ItemStack inputStack)
        {
            if (inputStack == null) return null;

            for (int i = 0; i < ValidStacks.Length; i++)
            {
                bool isWildCard = ValidStacks[i].Code.Path.Contains("*");
                bool found =
                    (isWildCard && inputStack.Collectible.WildCardMatch(ValidStacks[i].Code))
                    || (!isWildCard && inputStack.Equals(world, ValidStacks[i].ResolvedItemstack, GlobalConstants.IgnoredStackAttributes))
                    || (ValidStacks[i].CookedStack?.ResolvedItemstack != null && inputStack.Equals(world, ValidStacks[i].CookedStack.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes))
                ;

                if (found) return ValidStacks[i];
            }


            return null;
        }

        internal void Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            this.world = world;

            List<CookingRecipeStack> resolvedStacks = new List<CookingRecipeStack>();

            for (int i = 0; i < ValidStacks.Length; i++)
            {
                var cstack = ValidStacks[i];

                if (cstack.Code.Path.Contains("*"))
                {
                    resolvedStacks.Add(cstack);
                    continue;
                }

                if (cstack.Resolve(world, sourceForErrorLogging))
                {
                    resolvedStacks.Add(cstack);
                }

                cstack.CookedStack?.Resolve(world, sourceForErrorLogging);
            }

            ValidStacks = resolvedStacks.ToArray();
        }
    }
}
