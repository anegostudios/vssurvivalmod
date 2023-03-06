using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ModSystemResoArchiveCommands : ModSystem
    {
        ICoreServerAPI sapi;
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands
                .GetOrCreate("dev")
                .WithDesc("Gamedev tools")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSub("setmbname")
                    .WithDesc("Set the name of a microblock")
                    .WithArgs(parsers.WorldPosition("micro block position"), parsers.All("name"))
                    .HandleWith(onSetMicroBlockName)
                .EndSub()
                .BeginSub("setlorecode")
                    .WithDesc("Set the lore code of a bookshelf with lore")
                    .WithArgs(parsers.OptionalWord("lorecode"))
                    .HandleWith(onSetLoreCode)
                .EndSub()
                .BeginSub("lampncfg")
                    .WithDesc("Set the network code of a light source")
                    .WithArgs(parsers.OptionalWord("networkcode"))
                    .HandleWith(onLampNodeConfig)
                .EndSub()
                .BeginSub("pumponcmd")
                    .WithDesc("Set command to run when the pump is on")
                    .WithArgs(parsers.OptionalAll("command"))
                    .HandleWith(args => onSetCmd(args, true))
                .EndSub()
                .BeginSub("pumpoffcmd")
                    .WithDesc("Set command to run when the pump is off")
                    .WithArgs(parsers.OptionalAll("command"))
                    .HandleWith(args => onSetCmd(args, false))
                .EndSub()
                .BeginSub("musictrigger")
                    .WithDesc("Configure the music trigger meta block")
                    .RequiresPlayer()
                    .BeginSub("setarea")
                        .WithDesc("Define trigger area")
                        .WithArgs(parsers.Int("minX"), parsers.Int("minY"), parsers.Int("minZ"), parsers.Int("maxX"), parsers.Int("maxY"), parsers.Int("maxZ"))
                        .HandleWith(onSetMusicTriggerArea)
                    .EndSub()
                    .BeginSub("hidearea")
                        .WithDesc("Hide trigger preview")
                        .HandleWith(onMusicTriggerHideArea)
                    .EndSub()
                    .BeginSub("settrack")
                        .WithDesc("Set track file")
                        .WithArgs(parsers.Word("track file location"))
                        .HandleWith(onMusicTriggerSetTrack)
                    .EndSub()
                .EndSub()
                .Validate()
            ;
        }

        private TextCommandResult onSetMicroBlockName(TextCommandCallingArgs args)
        {
            BlockPos pos = (args[0] as Vec3d).AsBlockPos;
            string name = args[1] as string;

            var be = sapi.World.BlockAccessor.GetBlockEntity<BlockEntityMicroBlock>(pos);
            if (be == null)
            {
                return TextCommandResult.Error("Not looking at a microblock");
            }

            be.BlockName = name;
            be.MarkDirty(true);
            return TextCommandResult.Success("Microblock name set.");
        }

        private TextCommandResult onMusicTriggerSetTrack(TextCommandCallingArgs args)
        {
            BlockPos pos = (args.Caller.Player == null) ? args.Caller.Pos.AsBlockPos : args.Caller.Player?.CurrentBlockSelection?.Position;

            if (args.Caller.Player != null && pos == null)
            {
                return TextCommandResult.Error("Need to look at a block");
            }

            var bec = sapi.World.BlockAccessor.GetBlockEntity<BlockEntityMusicTrigger>(pos);
            if (bec == null)
            {
                return TextCommandResult.Error("Selected block is not a music trigger");
            }

            bec.musicTrackLocation = new AssetLocation((args[0]) as string);
            bec.MarkDirty(true);

            return TextCommandResult.Success("Ok, music track set");
        }


        private TextCommandResult onMusicTriggerHideArea(TextCommandCallingArgs args)
        {
            sapi.World.HighlightBlocks(args.Caller.Player, 1292, new List<BlockPos>(), API.Client.EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cube);

            return TextCommandResult.Success("Ok, preview area removed");
        }

        private TextCommandResult onSetMusicTriggerArea(TextCommandCallingArgs args)
        {
            BlockPos pos = (args.Caller.Player == null) ? args.Caller.Pos.AsBlockPos : args.Caller.Player?.CurrentBlockSelection?.Position;

            if (args.Caller.Player != null && pos == null)
            {
                return TextCommandResult.Error("Need to look at a block");
            }

            var bec = sapi.World.BlockAccessor.GetBlockEntity<BlockEntityMusicTrigger>(pos);
            if (bec == null)
            {
                return TextCommandResult.Error("Selected block is not a music trigger");
            }

            bec.minX = (int)args[0];
            bec.minY = (int)args[1];
            bec.minZ = (int)args[2];

            bec.maxX = (int)args[3];
            bec.maxY = (int)args[4];
            bec.maxZ = (int)args[5];
            bec.MarkDirty(true);

            List<BlockPos> minmax = new List<BlockPos>();
            minmax.Add(bec.Pos.AddCopy(bec.minX, bec.minY, bec.minZ));
            minmax.Add(bec.Pos.AddCopy(bec.maxX, bec.maxY, bec.maxZ));
            sapi.World.HighlightBlocks(args.Caller.Player, 1292, minmax, API.Client.EnumHighlightBlocksMode.Absolute, EnumHighlightShape.Cube);
            

            return TextCommandResult.Success("Ok, area set");
        }

        private TextCommandResult onSetLoreCode(TextCommandCallingArgs args)
        {
            BlockPos pos = (args.Caller.Player == null) ? args.Caller.Pos.AsBlockPos : args.Caller.Player?.CurrentBlockSelection?.Position;

            if (args.Caller.Player != null && pos == null)
            {
                return TextCommandResult.Error("Need to look at a block");
            }

            var bec = sapi.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorClutterBookshelfWithLore>();
            if (bec == null)
            {
                return TextCommandResult.Error("Selected block is not a bookshelf with lore");
            }

            bec.LoreCode = args[0] as string;
            bec.Blockentity.MarkDirty(true);

            return TextCommandResult.Success("Lore code set");
        }

        private TextCommandResult onSetCmd(TextCommandCallingArgs args, bool on)
        {
            BlockPos pos = (args.Caller.Player == null) ? args.Caller.Pos.AsBlockPos : args.Caller.Player?.CurrentBlockSelection?.Position;

            if (args.Caller.Player != null && pos == null)
            {
                return TextCommandResult.Error("Need to look at a block");
            }

            var bec = sapi.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorJonasHydraulicPump>();
            if (bec == null)
            {
                return TextCommandResult.Error("Selected block is not a command block");
            }

            var cmds = args[0] as string;
            if (on)
            {
                bec.oncommands = cmds;
            } else
            {
                bec.offcommands = cmds;
            }

            bec.Blockentity.MarkDirty(true);

            if (cmds == null || cmds.Length == 0)
            {
                return TextCommandResult.Success((on ? "On" : "Off") + " Command cleared.");
            }

            return TextCommandResult.Success((on ? "On" : "Off") + " Command " + cmds.Replace("{", "{{").Replace("}","}}") + " set.");
        }

        private TextCommandResult onLampNodeConfig(TextCommandCallingArgs args)
        {
            BlockPos pos = (args.Caller.Player == null) ? args.Caller.Pos.AsBlockPos : args.Caller.Player?.CurrentBlockSelection?.Position;

            if (args.Caller.Player != null && pos == null)
            {
                return TextCommandResult.Error("Need to look at a block");
            }

            var beh = sapi.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<INetworkedLight>();
            if (beh == null)
            {
                return TextCommandResult.Error("Selected block is not a lamp node");
            }

            beh.setNetwork(args[0] as string);
            return TextCommandResult.Success("Network " + args[0] + " set.");
        }
    }
}
