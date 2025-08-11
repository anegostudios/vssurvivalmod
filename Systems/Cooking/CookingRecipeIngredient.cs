using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// An itemstack specific for a <see cref="CookingRecipeIngredient"/>.
    /// Most properties are extended from <see cref="JsonItemStack"/>.
    /// </summary>
    [DocumentAsJson]
    public class CookingRecipeStack : JsonItemStack
    {
        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>None</jsondefault>-->
        /// The hierachy/path of the shape element inside the recipe's shape file. Will be enabled/disabled in the final meal if this itemstack is used.
        /// </summary>
        [DocumentAsJson] public string? ShapeElement;

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>None</jsondefault>-->
        /// Overrides a texture mapping for the shape element. Uses two strings, the first being the original texture code, and the second being a new texture code.
        /// </summary>
        [DocumentAsJson] public string[]? TextureMapping;

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>None</jsondefault>-->
        /// A cooked version of the ingredient stack that also satisfies this recipe.
        /// </summary>
        [DocumentAsJson] public JsonItemStack? CookedStack;

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
                TextureMapping = (string[]?)TextureMapping?.Clone(),
                CookedStack = CookedStack?.Clone()
            };

            if (Attributes != null) stack.Attributes = Attributes.Clone();

            stack.ShapeElement = ShapeElement;

            return stack;
        }
        
    }

    /// <summary>
    /// An ingredient for a <see cref="CookingRecipe"/>.
    /// Note that each ingredient can have multiple valid itemstacks that satisfy the ingredient.
    /// </summary>
    /// <example> <code language="json">
    ///{
    ///	"code": "water",
    ///	"validStacks": [
    ///		{
    ///			"type": "item",
    ///			"code": "waterportion",
    ///			"shapeElement": "bowl/water"
    ///		}
    ///	],
    ///	"minQuantity": 1,
    ///	"maxQuantity": 1,
    ///	"portionSizeLitres": 1
    ///}
    /// </code></example>
    [DocumentAsJson]
    public class CookingRecipeIngredient
    {
        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// The code for the recipe ingredient. Should be unique in the recipe, but isn't specifically used for anything.
        /// </summary>
        [DocumentAsJson] required public string Code;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// The minimum quantity required for the given ingredient.
        /// </summary>
        [DocumentAsJson] public int MinQuantity;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// The maximum quantity required for the given ingredient.
        /// </summary>
        [DocumentAsJson] public int MaxQuantity;

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>0</jsondefault>-->
        /// If this ingredient is a liquid, how many litres of it do we need for it to be a valid ingredient?
        /// </summary>
        [DocumentAsJson] public float PortionSizeLitres;

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// The string to use when displaying the ingredient name in the recipe book.
        /// </summary>
        [DocumentAsJson] public string TypeName = "unknown";

        /// <summary>
        /// <!--<jsonoptional>Required</jsonoptional>-->
        /// A list of item stacks that satisfy this ingredient.
        /// </summary>
        [DocumentAsJson] required public CookingRecipeStack[] ValidStacks;

        /// <summary>
        /// The world accessor for the ingredient.
        /// </summary>
        public IWorldAccessor? world;

        [MemberNotNull(nameof(Code), nameof(ValidStacks))]
        public virtual void FromBytes(BinaryReader reader, IClassRegistryAPI instancer)
        {
            Code = reader.ReadString();
            MinQuantity = reader.ReadInt32();
            MaxQuantity = reader.ReadInt32();
            PortionSizeLitres = reader.ReadSingle();
            TypeName = reader.ReadString();

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
            writer.Write(TypeName);

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
                PortionSizeLitres = PortionSizeLitres,
                TypeName = TypeName,
                ValidStacks = new CookingRecipeStack[ValidStacks.Length]
            };

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
        public CookingRecipeStack? GetMatchingStack(ItemStack? inputStack)
        {
            if (inputStack == null) return null;

            for (int i = 0; i < ValidStacks.Length; i++)
            {
                bool isWildCard = ValidStacks[i].Code.Path.Contains('*');
                bool found =
                    (isWildCard && inputStack.Collectible.WildCardMatch(ValidStacks[i].Code))
                    || (!isWildCard && inputStack.Equals(world, ValidStacks[i].ResolvedItemstack, [.. GlobalConstants.IgnoredStackAttributes, "timeFrozen"]))
                    || (ValidStacks[i].CookedStack?.ResolvedItemstack is ItemStack cookedStack && inputStack.Equals(world, cookedStack, [.. GlobalConstants.IgnoredStackAttributes, "timeFrozen"]))
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

                if (cstack.Code.Path.Contains('*'))
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
