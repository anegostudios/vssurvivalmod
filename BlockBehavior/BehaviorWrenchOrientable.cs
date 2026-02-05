using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows a block to cycle through variants when using a wrench.
    /// Uses the code "WrenchOrientable".
    /// </summary>
    /// <example>
    /// <code lang="json">
    ///"behaviorsByType": {
	///	"*": [
	///		{
	///			"name": "WrenchOrientable",
	///			"properties": {
	///				"baseCode": "log-placed-{wood}"
	///			}
	///		}
	///	]
	///},
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorWrenchOrientable : BlockBehavior
    {
        public static Dictionary<string, SortedSet<AssetLocation>> VariantsByType = new ();

        /// <summary>
        /// The code of the block that should be cycled through when used with a wrench. Required if not using a block class which inherits IWrenchOrientable.
        /// </summary>
        [DocumentAsJson("Required")]
        public string BaseCode;

        /// <summary>
        /// Should the block hide the placed block interaction help when in survival mode?
        /// </summary>
        [DocumentAsJson("Optional", "False")]
        bool hideInteractionHelpInSurvival;

        public static ItemStack[] wrenchItems;

        public static void loadWrenchItems(IWorldAccessor world)
        {
            // This is a potentially rather slow wildcard search of all items (especially if mods add many items) therefore we want to run this only once per game
            Item[] wrenches = world.SearchItems(new AssetLocation("wrench-*"));
            wrenchItems = new ItemStack[wrenches.Length];
            for (int i = 0; i < wrenches.Length; i++) wrenchItems[i] = new ItemStack(wrenches[i]);
        }

        public BlockBehaviorWrenchOrientable(Block block) : base(block)
        {
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            if (hideInteractionHelpInSurvival && forPlayer?.WorldData.CurrentGameMode == EnumGameMode.Survival) return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);

            handling = EnumHandling.PassThrough;
            if (wrenchItems == null) loadWrenchItems(world);

            bool notProtected = true;

            if (world.Claims != null && world is IClientWorldAccessor clientWorld && clientWorld.Player?.WorldData.CurrentGameMode == EnumGameMode.Survival)
            {
                EnumWorldAccessResponse resp = world.Claims.TestAccess(clientWorld.Player, selection.Position, EnumBlockAccessFlags.BuildOrBreak);
                if (resp != EnumWorldAccessResponse.Granted) notProtected = false;
            }

            if (wrenchItems.Length > 0 && notProtected)
            {
                return new WorldInteraction[] { new WorldInteraction()
                {
                    ActionLangCode = "Rotate",
                    Itemstacks = wrenchItems,
                    MouseButton = EnumMouseButton.Right
                } };
            }
            else return System.Array.Empty<WorldInteraction>();
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            hideInteractionHelpInSurvival = properties["hideInteractionHelpInSurvival"].AsBool(false);
            BaseCode = properties["baseCode"].AsString();
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (BaseCode != null)
            {
                if (!VariantsByType.TryGetValue(BaseCode, out var vars))
                    VariantsByType[BaseCode] = vars = new SortedSet<AssetLocation>();
                
                vars.Add(block.Code);
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            wrenchItems = null;
            VariantsByType.Clear();
        }
    }
}
