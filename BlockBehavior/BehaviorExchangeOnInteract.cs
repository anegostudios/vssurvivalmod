using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorExchangeOnInteract : BlockBehavior
    {
        AssetLocation[] blockCodes;
        string sound;
        string actionlangcode;

        public BlockBehaviorExchangeOnInteract(Block block) : base(block)
        {

            
        }

        public override void Initialize(JsonObject properties)
        {
            string[] blockCodes = properties["exchangeStates"].AsStringArray();

            this.blockCodes = new AssetLocation[blockCodes.Length];

            for (int i = 0; i < blockCodes.Length; i++)
            {
                this.blockCodes[i] = new AssetLocation(blockCodes[i]);
                if (!this.blockCodes[i].HasDomain())
                {
                    this.blockCodes[i].Domain = block.Code.Domain;
                }
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

            AssetLocation loc = block.WildCardReplace(blockCodes[index], blockCodes[(index + 1) % blockCodes.Length]);
            Block nextBlock = world.GetBlock(loc);

            if (nextBlock == null) return false;

            world.BlockAccessor.ExchangeBlock(nextBlock.BlockId, blockSel.Position);

            if (sound != null)
            {
                world.PlaySoundAt(new AssetLocation("sounds/" + sound), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            }

            return true;
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
