using Vintagestory.API.Client;
using System.Collections.Generic;
using Vintagestory.API.Common;
using System;
using Vintagestory.API.Server;
using System.Text;
using ProtoBuf;

namespace Vintagestory.GameContent
{

    [ProtoContract]
    public class ChatCommandSyntax : IChatCommand
    {
        [ProtoMember(1)]
        public string FullName { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public string Description { get; set; }

        [ProtoMember(4)]
        public string AdditionalInformation { get; set; }

        [ProtoMember(5)]
        public string[] Examples { get; set; }

        [ProtoMember(6)]
        public string CallSyntax { get; set; }

        [ProtoMember(7)]
        public string FullSyntax;

        [ProtoMember(8)]
        public string CallSyntaxUnformatted { get; set; }

        [ProtoMember(9)]
        public string FullnameAlias { get; set; }

        [ProtoMember(10)]
        public List<string> Aliases { get; set; }

        [ProtoMember(11)]
        public List<string> RootAliases { get; set; }

        public string CommandPrefix => throw new NotImplementedException();
        public IChatCommand this[string name] => throw new NotImplementedException();

        public bool Incomplete => throw new NotImplementedException();

        public IEnumerable<IChatCommand> Subcommands => throw new NotImplementedException();

        public Dictionary<string, IChatCommand> AllSubcommands => throw new NotImplementedException();


        public string GetFullName(string alias, bool isRootAlias)
        {
            return FullnameAlias;
        }

        public override string ToString()
        {
            return CallSyntaxUnformatted;
        }

        public string GetCallSyntax(string alias, bool isRootAlias = false)
        {
            return CallSyntax;
        }

        public string GetCallSyntaxUnformatted(string alias, bool isRootAlias = false)
        {
            return CallSyntaxUnformatted;
        }

        public void AddParameterSyntax(StringBuilder sb, string indent)
        {
            throw new NotImplementedException();
        }

        public void AddSyntaxExplanation(StringBuilder sb, string indent)
        {
            throw new NotImplementedException();
        }

        public IChatCommand BeginSubCommand(string name)
        {
            throw new NotImplementedException();
        }

        public IChatCommand BeginSubCommands(params string[] name)
        {
            throw new NotImplementedException();
        }

        public IChatCommand EndSubCommand()
        {
            throw new NotImplementedException();
        }

        public void Execute(TextCommandCallingArgs callargs, Action<TextCommandResult> onCommandComplete = null)
        {
            throw new NotImplementedException();
        }


        public string GetFullSyntaxConsole(Caller caller)
        {
            return FullSyntax;
        }

        public IChatCommand HandleWith(OnCommandDelegate handler)
        {
            throw new NotImplementedException();
        }

        public IChatCommand IgnoreAdditionalArgs()
        {
            throw new NotImplementedException();
        }

        public bool IsAvailableTo(Caller caller)
        {
            throw new NotImplementedException();
        }

        public IChatCommand RequiresPlayer()
        {
            throw new NotImplementedException();
        }

        public IChatCommand RequiresPrivilege(string privilege)
        {
            throw new NotImplementedException();
        }

        public void Validate()
        {
            throw new NotImplementedException();
        }

        public IChatCommand WithAdditionalInformation(string detail)
        {
            throw new NotImplementedException();
        }

        public IChatCommand WithAlias(params string[] name)
        {
            throw new NotImplementedException();
        }

        public IChatCommand WithArgs(params ICommandArgumentParser[] args)
        {
            throw new NotImplementedException();
        }

        public IChatCommand WithDescription(string description)
        {
            throw new NotImplementedException();
        }

        public IChatCommand WithExamples(params string[] examaples)
        {
            throw new NotImplementedException();
        }

        public IChatCommand WithName(string name)
        {
            throw new NotImplementedException();
        }

        public IChatCommand WithPreCondition(CommandPreconditionDelegate p)
        {
            throw new NotImplementedException();
        }

        public IChatCommand WithRootAlias(string name)
        {
            throw new NotImplementedException();
        }

        public string GetFullSyntaxHandbook(Caller caller, string indent = "", bool isRootAlias = false)
        {
            return FullSyntax;
        }

    }

    [ProtoContract]
    public class ServerCommandsSyntax
    {
        [ProtoMember(1)]
        public ChatCommandSyntax[] Commands; 
    }

    public class ModSystemCommandHandbook : ModSystem
    {
        ICoreClientAPI capi;
        GuiDialogHandbook dialog;
        ICoreServerAPI sapi;
        ServerCommandsSyntax serverCommandsSyntaxClient;


        public event InitCustomPagesDelegate OnInitCustomPages;

        internal void TriggerOnInitCustomPages(List<GuiHandbookPage> pages)
        {
            OnInitCustomPages?.Invoke(pages);
        }

        public override bool ShouldLoad(EnumAppSide side) => true;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("commandhandbook").RegisterMessageType<ServerCommandsSyntax>();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.PlayerNowPlaying += Event_PlayerNowPlaying;

            api.ChatCommands.Create("chbr")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithDescription("Reload command handbook texts")
                .HandleWith(onCommandHandbookReload)
            ;
        }

        private void Event_PlayerNowPlaying(IServerPlayer byPlayer)
        {
            sendSyntaxPacket(byPlayer);
        }

        private void sendSyntaxPacket(IServerPlayer byPlayer)
        {
            var cmdsyntaxPacket = genCmdSyntaxPacket(new Caller() { Player = byPlayer, Type = EnumCallerType.Player });
            sapi.Network.GetChannel("commandhandbook").SendPacket(cmdsyntaxPacket, byPlayer);
        }

        private ServerCommandsSyntax genCmdSyntaxPacket(Caller caller)
        {
            List<ChatCommandSyntax> cmds = new List<ChatCommandSyntax>();

            foreach (var val in IChatCommandApi.GetOrdered(sapi.ChatCommands))
            {
                var cmd = val.Value;
                cmds.Add(new ChatCommandSyntax()
                {
                    AdditionalInformation = cmd.AdditionalInformation,
                    CallSyntax = cmd.CallSyntax,
                    CallSyntaxUnformatted = cmd.CallSyntaxUnformatted,
                    Description = cmd.Description,
                    Examples = cmd.Examples,
                    FullName = cmd.FullName,
                    Name = val.Key,
                    FullnameAlias = cmd.GetFullName(val.Key, true), // everything in the top level is either a root cmd or a rootalias
                    FullSyntax = cmd.GetFullSyntaxHandbook(caller, string.Empty, cmd.RootAliases?.Contains(val.Key) == true),
                    Aliases = cmd.Aliases,
                    RootAliases = cmd.RootAliases
                });
            }

            return new ServerCommandsSyntax() { Commands = cmds.ToArray() };
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            api.Network.GetChannel("commandhandbook").SetMessageHandler<ServerCommandsSyntax>(onServerCommandsSyntax);

            api.RegisterLinkProtocol("commandhandbook", onHandBookLinkClicked);

            api.ChatCommands
                .Create("chb")
                .WithDescription("Opens the command hand book")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(onCommandHandbook)
                .Validate()
            ;
        }

        private void onHandBookLinkClicked(LinkTextComponent comp)
        {
            string target = comp.Href.Substring("commandhandbook://".Length);

            // Seems to fix links like thos not working: block-labeledchest-east-{{ \"type\": \\\"normal-labeled\\\" }}
            target = target.Replace("\\", "");

            if (target.StartsWith("tab-"))
            {
                if (!dialog.IsOpened()) dialog.TryOpen();
                dialog.selectTab(target.Substring(4));
                return;
            }

            if (!dialog.IsOpened()) dialog.TryOpen();

            if (target.Length > 0) dialog.OpenDetailPageFor(target);
        }

        private TextCommandResult onCommandHandbookReload(TextCommandCallingArgs args)
        {
            sendSyntaxPacket(args.Caller.Player as IServerPlayer);
            return TextCommandResult.Success("ok, reloaded");
        }

        private void onServerCommandsSyntax(ServerCommandsSyntax packet)
        {
            serverCommandsSyntaxClient = packet;

            dialog = new GuiDialogCommandHandbook(capi, onCreatePagesAsync, onComposePage);
            capi.Logger.VerboseDebug("Done initialising handbook");
        }

        private TextCommandResult onCommandHandbook(TextCommandCallingArgs args)
        {
            if (dialog.IsOpened())
            {
                dialog.TryClose();
            } else
            {
                dialog.TryOpen();
            }

            return TextCommandResult.Success();
        }


        private List<GuiHandbookPage> onCreatePagesAsync()
        {
            var pages = new List<GuiHandbookPage>();

            foreach (var val in IChatCommandApi.GetOrdered(capi.ChatCommands))
            {
                if (capi.IsShuttingDown) break;
                var cmd = val.Value;
                
                pages.Add(new GuiHandbookCommandPage(cmd, cmd.CommandPrefix + val.Key, "client", cmd.RootAliases?.Contains(val.Key) == true));
            }

            foreach (var cmd in serverCommandsSyntaxClient.Commands)
            {
                if (capi.IsShuttingDown) break;
                pages.Add(new GuiHandbookCommandPage(cmd, cmd.FullnameAlias, "server"));
            }

            return pages;
        }

        private void onComposePage(GuiHandbookPage page, GuiComposer detailViewGui, ElementBounds textBounds, ActionConsumable<string> openDetailPageFor)
        {
            page.ComposePage(detailViewGui, textBounds, null, openDetailPageFor);
        }



        public override void Dispose()
        {
            base.Dispose();
            dialog?.Dispose();
        }
    }
}
