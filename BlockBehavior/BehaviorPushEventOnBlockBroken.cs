using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Publishes the BlockPos when a Block is broken with the given event name. Likely used in tandem with code mods. 
    /// Uses the "PushEventOnBlockBroken" code.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviorsByType": {
	///	"bamboo-grown-*": [
	///		{
	///			"name": "PushEventOnBlockBroken",
	///			"properties": { "eventName": "testForDecay" }
	///		}
	///	]
	///}
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorPushEventOnBlockBroken : BlockBehavior
    {
        /// <summary>
        /// The name of the event to call. Use Api.Event.RegisterEventBusListener in code to register an event.
        /// </summary>
        [DocumentAsJson("Required")]
        private string eventName;

        public BlockBehaviorPushEventOnBlockBroken(Block block) : base(block) { }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            eventName = properties["eventName"]?.AsString();
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;

            if(byPlayer != null)
            {
                TreeAttribute tree = new TreeAttribute();
                tree.SetInt("x", pos.X);
                tree.SetInt("y", pos.Y);
                tree.SetInt("z", pos.Z);
                world.Api.Event.PushEvent(eventName, tree);
            }
        }

    }
}
