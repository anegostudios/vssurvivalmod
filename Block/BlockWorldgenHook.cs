using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockWorldgenHook : Block
    {
        ICoreServerAPI sapi;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api is ICoreServerAPI sapi)
            {
                this.sapi = sapi;
                var parsers = api.ChatCommands.Parsers;
                api.ChatCommands.GetOrCreate("dev")
                    .RequiresPrivilege(Privilege.controlserver)
                    .BeginSubCommand("worldgenhook")
                        .WithArgs(parsers.WorldPosition("pos"), parsers.OptionalWord("hook"), parsers.OptionalWord("hookparam"))
                        .HandleWith(onHookConfig)
                    .EndSubCommand()
                    .BeginSubCommand("testworldgenhook")
                        .WithArgs(parsers.WorldPosition("pos"), parsers.Word("hook"), parsers.OptionalWord("hookparam"))
                        .HandleWith(onHookTest)
                    .EndSubCommand();
            }
        }

        private TextCommandResult onHookConfig(TextCommandCallingArgs args)
        {
            BlockPos target = (args.Parsers[0].GetValue() as Vec3d).AsBlockPos;
            string hook = args.Parsers[1].GetValue() as string;
            string hookparam = args.Parsers[2].GetValue() as string;

            BlockEntityWorldgenHook bewh = sapi.World.BlockAccessor.GetBlockEntity(target) as BlockEntityWorldgenHook;

            if (bewh == null) return TextCommandResult.Error("No WorldgenHook block found at position " + target);
            if (hook == null) return TextCommandResult.Success("Hook is currently: \"" + bewh.GetHook() + "\"");

            bewh.SetHook(hook, hookparam);
            return TextCommandResult.Success("Ok, hook set");

        }

        private TextCommandResult onHookTest(TextCommandCallingArgs args)
        {
            BlockPos target = (args.Parsers[0].GetValue() as Vec3d).AsBlockPos;
            string hook = args.Parsers[1].GetValue() as string;
            string hookparam = args.Parsers[2].GetValue() as string;
            BlockEntityWorldgenHook.TriggerWorldgenHook(sapi, sapi.World.BlockAccessor, target, hook, hookparam);
            return TextCommandResult.Success("Ok, test was run");
        }
    }
}
