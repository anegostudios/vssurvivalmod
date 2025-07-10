using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Vintagestory.API.Common;
using Vintagestory.API;

#nullable disable

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
        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>Consume All</jsondefault>-->
        /// How many of this itemstack should be consumed in the recipe? This should only be used for recipes with a liquid output.
        /// </summary>
        [DocumentAsJson] public int? ConsumeQuantity = null;

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>None</jsondefault>-->
        /// If the ingredient is a liquid, will use this value instead of <see cref="CraftingRecipeIngredient.Quantity"/>.
        /// </summary>
        [DocumentAsJson] public float Litres = -1;

        /// <summary>
        /// <!--<jsonoptional>Optional</jsonoptional><jsondefault>Consume All</jsondefault>-->
        /// How much of the liquid should be consumed in the recipe? This should only be used by recipes with a non-liquid output.
        /// </summary>
        [DocumentAsJson] public float? ConsumeLitres;

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
    }

}
