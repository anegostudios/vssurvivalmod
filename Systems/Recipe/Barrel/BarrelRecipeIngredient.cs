using System.IO;
using Vintagestory.API;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Defines an ingredient for use in a <see cref="BarrelRecipe"/>.
    /// </summary>
    /// <example>
    /// <code language="json">
    ///{
    ///	"type": "item",
    ///	"code": "strongtanninportion",
    ///	"litres": 2,
    ///	"consumeLitres": 2
    ///}</code>
    ///<code language="json">
    ///{
    ///	"type": "item",
    ///	"code": "hide-prepared-small",
    ///	"quantity": 1
    ///}
    ///</code></example>
    [DocumentAsJson]
    public class BarrelRecipeIngredient : CraftingRecipeIngredient
    {
        #region From JSON
        /// <summary>
        /// How many of this itemstack should be consumed in the recipe? This should only be used for recipes with a liquid output.
        /// </summary>
        [DocumentAsJson("Optional", "Consume All")]
        public int? ConsumeQuantity = null;

        /// <summary>
        /// If the ingredient is a liquid, will use this value instead of <see cref="CraftingRecipeIngredient.Quantity"/>.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        public float Litres = -1;

        /// <summary>
        /// How much of the liquid should be consumed in the recipe? This should only be used by recipes with a non-liquid output.
        /// </summary>
        [DocumentAsJson("Optional", "Consume All")]
        public float? ConsumeLitres;
        #endregion

        public override BarrelRecipeIngredient Clone()
        {
            BarrelRecipeIngredient result = new();

            CloneTo(result);

            return result;
        }

        public override void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            base.FromBytes(reader, resolver);

            bool isset = reader.ReadBoolean();
            if (isset)
            {
                ConsumeQuantity = reader.ReadInt32();
            }
            else
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
            }
            else
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

        public override bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            if (!base.Resolve(world, sourceForErrorLogging))
            {
                return false;
            }

            ResolveLiquidProperties(world, sourceForErrorLogging);

            return true;
        }

        protected virtual void ResolveLiquidProperties(IWorldAccessor world, string sourceForErrorLogging)
        {
            WaterTightContainableProps? liquidProperties = BlockLiquidContainerBase.GetContainableProps(ResolvedItemStack);

            if (liquidProperties == null) return;

            if (Litres < 0)
            {
                if (Quantity > 0)
                {
                    world.Logger.Warning($"({sourceForErrorLogging}) Barrel recipe ingredient '{Code}' does not define a litres attribute but a quantity, will assume quantity=litres for backwards compatibility.");
                    Litres = Quantity;
                    ConsumeLitres = ConsumeQuantity;
                }
                else
                {
                    Litres = 1;
                }
            }

            Quantity = (int)(liquidProperties.ItemsPerLitre * Litres);
            if (ConsumeLitres != null)
            {
                ConsumeQuantity = (int)(liquidProperties.ItemsPerLitre * ConsumeLitres);
            }
        }

        protected override void CloneTo(object cloneTo)
        {
            base.CloneTo(cloneTo);

            if (cloneTo is BarrelRecipeIngredient ingredient)
            {
                ingredient.ConsumeQuantity = ConsumeQuantity;
                ingredient.Litres = Litres;
                ingredient.ConsumeLitres = ConsumeLitres;
            }
        }
    }
}
