using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows a block to swap between a set of states on activation. Note that this behavior has some small hardcoding for legacy trapdoors.
    /// This behavior is used with the code "ExchangeOnInteract".
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "ExchangeOnInteract",
	///		"properties": {
	///			"exchangeStates": [ "*-closed-*", "*-opened-*" ],
	///			"sound": "block/door",
	///			"actionLangCode": "blockhelp-trapdoor-openclose"
	///		}
	///	}
	///]
    ///</code></example>
    [DocumentAsJson]
    public class BlockBehaviorExchangeOnInteract : BlockBehavior
    {
        /// <summary>
        /// The list of block codes to iterate over upon interaction.
        /// </summary>
        [DocumentAsJson("Required")]
        AssetLocation[] blockCodes;

        /// <summary>
        /// The soudn to play when this block with exchanged.
        /// </summary>
        [DocumentAsJson("Required")]
        string sound;

        /// <summary>
        /// The lang code to show text when hovering over this block. 
        /// </summary>
        [DocumentAsJson("Required")]
        string actionlangcode;

        public BlockBehaviorExchangeOnInteract(Block block) : base(block)
        {

            
        }

        public override void Initialize(JsonObject properties)
        {
            string[] blockCodes = properties["exchangeStates"].AsArray<string>();

            this.blockCodes = new AssetLocation[blockCodes.Length];

            for (int i = 0; i < blockCodes.Length; i++)
            {
                this.blockCodes[i] = AssetLocation.Create(blockCodes[i], block.Code.Domain);
            }

            sound = properties["sound"].AsString();
            actionlangcode = properties["actionLangCode"].AsString();
            base.Initialize(properties);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            handling = EnumHandling.PreventDefault;

            return DoExchange(world, byPlayer, blockSel.Position);
        }

        private bool DoExchange(IWorldAccessor world, IPlayer byPlayer, BlockPos pos)
        {
            int index = -1;
            for (int i = 0; i < blockCodes.Length; i++)
            {
                if (block.WildCardMatch(blockCodes[i]))
                {
                    index = i;
                    break;
                }
            }
            if (index == -1) return false;

            AssetLocation loc = block.Code.WildCardReplace(blockCodes[index], blockCodes[(index + 1) % blockCodes.Length]);
            Block nextBlock = world.GetBlock(loc);

            if (nextBlock == null) return false;

            world.BlockAccessor.ExchangeBlock(nextBlock.BlockId, pos);

            if (sound != null)
            {
                world.PlaySoundAt(new AssetLocation("sounds/" + sound), pos, 0, byPlayer);
            }

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }

        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs, ref EnumHandling handled)
        {
            if (activationArgs != null && activationArgs.HasAttribute("opened"))
            {
                if (activationArgs.GetBool("opened") == block.Code.Path.Contains("opened")) return;   // do nothing if already in the required state: NOTE this is only effective if the required state is "opened", works for trapdoors but might not work for something else
            }
            DoExchange(world, caller.Player, blockSel.Position);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handled)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = actionlangcode,
                    MouseButton = EnumMouseButton.Right
                }
            };
        }
    }
}
