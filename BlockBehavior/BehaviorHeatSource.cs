using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows this block to work as a heat source for entities. BlockEntities for a block with this behavior may also define their own custom heat strength.
    /// Uses the code "HeatSource".
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "HeatSource",
	///		"properties": { "heatStrength": 10 }
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorHeatSource : BlockBehavior, IHeatSource
    {
        /// <summary>
        /// The level of heat that this block emits. Some BlockEntities may omit this property and define their own heat strength.
        /// Fire has a heat strength of 10.
        /// </summary>
        [DocumentAsJson("Recommended", "0")]
        float heatStrength;

        public BlockBehaviorHeatSource(Block block) : base(block)
        {
        }


        public override void Initialize(JsonObject properties)
        {
            heatStrength = properties["heatStrength"].AsFloat(0);

            base.Initialize(properties);
        }

        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            if (block.EntityClass != null)
            {
                var behs = world.BlockAccessor.GetBlockEntity(heatSourcePos) as IHeatSource;
                if (behs != null)
                {
                    return behs.GetHeatStrength(world, heatSourcePos, heatReceiverPos);
                }
            }

            return heatStrength;
        }
    }
}
