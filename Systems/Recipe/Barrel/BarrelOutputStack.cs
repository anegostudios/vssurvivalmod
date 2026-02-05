using System.IO;
using Vintagestory.API;
using Vintagestory.API.Common;

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
    public class BarrelOutputStack : JsonItemStack, IConcreteCloneable<BarrelOutputStack>
    {
        #region From JSON
        /// <summary>
        /// If this output is a liquid, this should be used instead of <see cref="JsonItemStack.StackSize"/> to define quantity.
        /// </summary>
        [DocumentAsJson("Optional", "0")]
        public float Litres;
        #endregion

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

        public override bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            if (!base.Resolve(world, sourceForErrorLogging))
            {
                return false;
            }

            ResolveLiquidProperties(world, sourceForErrorLogging);

            return true;
        }

        public override BarrelOutputStack Clone()
        {
            BarrelOutputStack result = new();

            CloneTo(result);

            return result;
        }



        protected override void CloneTo(object stack)
        {
            base.CloneTo(stack);

            if (stack is BarrelOutputStack barrelOutput)
            {
                barrelOutput.Litres = Litres;
            }
        }

        protected virtual void ResolveLiquidProperties(IWorldAccessor world, string sourceForErrorLogging)
        {
            WaterTightContainableProps? liquidProperties = BlockLiquidContainerBase.GetContainableProps(ResolvedItemStack);
            if (liquidProperties != null)
            {
                if (Litres < 0)
                {
                    if (Quantity > 0)
                    {
                        world.Logger.Warning($"({sourceForErrorLogging}) Barrel recipe output {Code} does not define a litres attribute but a stacksize, will assume stacksize=litres for backwards compatibility.");
                        Litres = Quantity;
                    }
                    else
                    {
                        Litres = 1;
                    }

                }

                Quantity = (int)(liquidProperties.ItemsPerLitre * Litres);
            }
        }
    }
}
