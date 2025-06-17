using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockCommand : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityGuiConfigurableCommands bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGuiConfigurableCommands;
            if (bec?.OnInteract(new Caller() { Player = byPlayer }) == false)
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }
            
            return true;
        }

        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
        {
            BlockEntityGuiConfigurableCommands bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGuiConfigurableCommands;
            bec?.OnInteract(caller);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            bool isConditionalBlock = this.Code.PathStartsWith("conditionalblock");
            var result = new WorldInteraction[isConditionalBlock ? 3 : 2];
            result[0] = new WorldInteraction()
            {
                ActionLangCode = isConditionalBlock ? "Execute condition" : "Run commands",
                MouseButton = EnumMouseButton.Right
            };
            result[1] = new WorldInteraction()
            {
                ActionLangCode = "Edit (requires Creative mode)",
                HotKeyCode = "shift",
                MouseButton = EnumMouseButton.Right
            };
            if (isConditionalBlock)
            {
                result[2] = new WorldInteraction()
                {
                    ActionLangCode = "Rotate (requires Creative mode)",
                    Itemstacks = world.SearchItems(new AssetLocation("wrench-*")).Select(item => new ItemStack(item)).ToArray(),
                    MouseButton = EnumMouseButton.Right
                };
            }

            return result;
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);

            BlockEntityGuiConfigurableCommands bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGuiConfigurableCommands;
            if (bec != null)
            {
                stack.Attributes.SetString("commands", bec.Commands);
                if (bec.CallingPrivileges != null)
                {
                    stack.Attributes["callingPrivileges"] = new StringArrayAttribute(bec.CallingPrivileges);
                }
            }

            return stack;
        }

    }
}
