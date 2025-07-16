using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

#nullable disable

namespace Vintagestory.GameContent
{
    public class CmdTimeswitch
    {
        private ICoreServerAPI sapi;

        public CmdTimeswitch(ICoreServerAPI api)
        {
            sapi = api;
            var cmdapi = api.ChatCommands;
            var parsers = api.ChatCommands.Parsers;

            cmdapi
                .Create("timeswitch")
                .WithDescription("Timeswitch and dimensions switching commands")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("toggle")
                    .RequiresPrivilege(Privilege.chat)
                    .WithDescription("Toggle timeswitch state for the calling player")
                    .RequiresPlayer()
                    .HandleWith(ToggleState)
                .EndSubCommand()
                .BeginSubCommand("start")
                    .WithDescription("Start the system (to be used by a proximity trigger")
                    .HandleWith(Start)
                .EndSubCommand()
                .BeginSubCommand("setpos")
                    .WithDescription("Set the chunk column used for timeswitching")
                    .WithArgs(parsers.WorldPosition("column position"))
                    .HandleWith(SetPos)
                .EndSubCommand()
                .BeginSubCommand("copy")
                    .WithDescription("Copy blocks from normal dimension to timeswitch dimension")
                    .WithAdditionalInformation("(Destructive of the timeswitch dimension! Use argument 'confirm' to confirm)")
                    .WithArgs(parsers.OptionalWord("confirmation"))
                    .HandleWith(CopyBlocks)
                .EndSubCommand()
                .BeginSubCommand("relight")
                    .WithDescription("Relight the alternate dimension")
                    .HandleWith(Relight)
                .EndSubCommand()
            ;
        }


        private TextCommandResult ToggleState(TextCommandCallingArgs args)
        {
            var serverPlayer = args.Caller.Player as IServerPlayer;
            if (serverPlayer == null) return TextCommandResult.Error("The toggle command must be called by a currently active player");

            var TimeswitchSys = sapi.ModLoader.GetModSystem<Timeswitch>();
            bool result = TimeswitchSys.ActivateTimeswitchServer(serverPlayer, false, out string failureReason);

            return TextCommandResult.Success();
            //result ? TextCommandResult.Success() : failureReason == null ? TextCommandResult.Error("Timeswitch system not available on this server") : TextCommandResult.Success();
        }


        private TextCommandResult CopyBlocks(TextCommandCallingArgs args)
        {
            string confirmation = (args.Parsers[0].IsMissing ? "" : args[0] as string);
            if (confirmation != "confirm") return TextCommandResult.Error("The copy command will destroy existing blocks in the timeswitch dimension. To confirm, type: /timeswitch copy confirm");

            var TimeswitchSys = sapi.ModLoader.GetModSystem<Timeswitch>();
            var serverPlayer = args.Caller.Player as IServerPlayer;
            TimeswitchSys.CopyBlocksToAltDimension(sapi.World.BlockAccessor, serverPlayer);

            return TextCommandResult.Success();
        }


        private TextCommandResult Relight(TextCommandCallingArgs args)
        {
            var TimeswitchSys = sapi.ModLoader.GetModSystem<Timeswitch>();
            var serverPlayer = args.Caller.Player as IServerPlayer;
            TimeswitchSys.RelightCommand(sapi.World.BlockAccessor, serverPlayer);

            return TextCommandResult.Success();
        }


        private TextCommandResult SetPos(TextCommandCallingArgs args)
        {
            BlockPos pos = (args[0] as Vec3d).AsBlockPos;

            var TimeswitchSys = sapi.ModLoader.GetModSystem<Timeswitch>();
            TimeswitchSys.SetPos(pos);

            return TextCommandResult.Success();
        }


        private TextCommandResult Start(TextCommandCallingArgs args)
        {
            var TimeswitchSys = sapi.ModLoader.GetModSystem<Timeswitch>();
            var serverPlayer = args.Caller.Player as IServerPlayer;
            TimeswitchSys.OnStartCommand(serverPlayer);

            return TextCommandResult.Success();
        }
    }
}
