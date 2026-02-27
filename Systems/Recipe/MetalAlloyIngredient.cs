using System.IO;
using Vintagestory.API;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Defines an ingredient for a <see cref="AlloyRecipe"/> recipe.
    /// </summary>
    /// <example>
    /// <code language="json">
    ///{
    ///	"type": "item",
    ///	"code": "ingot-copper",
    ///	"minratio": 0.5,
    ///	"maxratio": 0.7
    ///}
    /// </code>
    /// </example>
    [DocumentAsJson]
    public class MetalAlloyIngredient : JsonItemStack
    {

        /// <summary>
        /// The minimum ratio of this metal to be used in the alloy, between 0 and 1.
        /// </summary>
        [DocumentAsJson("Required")]
        public float MinRatio;

        /// <summary>
        /// The maximum ratio of this metal to be used in the alloy, between 0 and 1.
        /// </summary>
        [DocumentAsJson("Required")]
        public float MaxRatio;

        public override void FromBytes(BinaryReader reader, IClassRegistryAPI instancer)
        {
            base.FromBytes(reader, instancer);
            MinRatio = reader.ReadSingle();
            MaxRatio = reader.ReadSingle();
        }

        public override void ToBytes(BinaryWriter writer)
        {
            base.ToBytes(writer);
            writer.Write(MinRatio);
            writer.Write(MaxRatio);
        }


        /// <summary>
        /// Creates a deep copy of this object
        /// </summary>
        /// <returns></returns>
        public new MetalAlloyIngredient Clone()
        {
            MetalAlloyIngredient stack = new MetalAlloyIngredient()
            {
                Code = Code,
                StackSize = StackSize,
                Type = Type,
                MinRatio = MinRatio,
                MaxRatio = MaxRatio
            };

            if (Attributes != null) stack.Attributes = Attributes.Clone();

            return stack;
        }
    }
}
