using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class MetalAlloyIngredient : JsonItemStack
    {
        public float MinRatio;
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
