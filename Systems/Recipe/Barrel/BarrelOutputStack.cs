using System.IO;
using Vintagestory.API;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Defines an output for use in a <see cref="BarrelRecipe"/>. This object takes most of its properties from the <see cref="JsonItemStack"/> class.
    /// </summary>
    /// <example>
    /// <code language="json">
    ///"output": {
    ///	"type": "item",
    ///	"code": "leather-normal-plain",
    ///	"stackSize": 3
    ///}</code>
    ///<code language="json">
    ///"output": {
    ///  "type": "item",
    ///  "code": "weaktanninportion",
    ///  "litres": 10
    ///}</code></example>
    [DocumentAsJson]
    public class BarrelOutputStack : JsonItemStack
    {
        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>0</jsondefault>-->
        /// If this output is a liquid, this should be used instead of <see cref="JsonItemStack.StackSize"/> to define quantity.
        /// </summary>
        [DocumentAsJson] public float Litres;

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
}