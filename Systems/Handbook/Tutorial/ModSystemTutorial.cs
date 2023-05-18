using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{

    [ProtoContract]
    public class ItemStackReceivedPacket
    {
        [ProtoMember(1)]
        public string eventname;
        [ProtoMember(2)]
        public byte[] stackbytes;
    }

    [ProtoContract]
    public class BlockPlacedPacket
    {
        [ProtoMember(1)]
        public BlockPos pos;
        [ProtoMember(2)]
        public int blockId;
        [ProtoMember(3)]
        public byte[] withStackInHands;
    }

    [ProtoContract]
    public class ActivateTutorialPacket
    {
        [ProtoMember(1)]
        public string Code;
    }

    public class ModSystemTutorial : ModSystem
    {
        ICoreAPI api;
        ICoreServerAPI sapi;
        ICoreClientAPI capi;

        HudTutorial hud;

        HashSet<string> tutorialModeActiveForPlayers = new HashSet<string>();
        bool eventsRegistered = false;

        ITutorial currentTutorialInst;

        public string CurrentTutorial { get; private set; }

        Dictionary<string, ITutorial> tutorials = new Dictionary<string, ITutorial>();

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.Network
                .RegisterChannel("tutorial")
                .RegisterMessageType<ItemStackReceivedPacket>()
                .RegisterMessageType<BlockPlacedPacket>()
                .RegisterMessageType<ActivateTutorialPacket>()
            ;
        }

        public void ActivateTutorialMode(string playerUid)
        {
            tutorialModeActiveForPlayers.Add(playerUid);
            if (!eventsRegistered)
            {
                sapi.Event.DidPlaceBlock += Event_DidPlaceBlock;
                api.Event.RegisterEventBusListener(onCollectedItem);
                eventsRegistered = true;
            }
        }

        internal void StopActiveTutorial()
        {
            hud.TryClose();
            CurrentTutorial = null;
            currentTutorialInst = null;
            tutorialModeActiveForPlayers.Remove(capi.World.Player.PlayerUID);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            api.Network
                .GetChannel("tutorial")
                .SetMessageHandler<ItemStackReceivedPacket>(onCollectedItemstack)
                .SetMessageHandler<BlockPlacedPacket>(onBlockPlaced)
            ;

            tutorials["firststeps"] = new FirstStepsTutorial(capi);

            api.ModLoader.GetModSystem<ModSystemHandbook>().OnInitCustomPages += ModSystemTutorial_OnInitCustomPages;
            api.Event.LevelFinalize += Event_LevelFinalize_Client;
            api.Event.LeaveWorld += Event_LeaveWorld_Client;
            api.Event.RegisterGameTickListener(onClientTick200ms, 200);
            capi.Input.AddHotkeyListener(onHotkey);
            capi.Input.InWorldAction += Input_InWorldAction;
            api.RegisterCommand("tutorial", "", "", onTutorialCmd);
        }

        private void Input_InWorldAction(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            onStateUpdate((step) => step.OnAction(action, on));
        }

        private void onHotkey(string hotkeycode, KeyCombination keyComb)
        {
            if (capi.World.Player == null) return;
            if (!tutorialModeActiveForPlayers.Contains(capi.World.Player.PlayerUID)) return;

            onStateUpdate((step) => step.OnHotkeyPressed(hotkeycode, keyComb));
        }

        private void onTutorialCmd(int groupId, CmdArgs args)
        {
            if (currentTutorialInst == null)
            {
                capi.ShowChatMessage("No current tutorial selected.");
                return;
            }

            string subcmd = args.PopWord();
            if (subcmd == "hud")
            {
                toggleHud();
            }

            if (subcmd == "restart")
            {
                currentTutorialInst.Restart();

                reloadTutorialPage();
                hud.loadHud(currentTutorialInst.PageCode);
            }

            if (subcmd == "skip")
            {
                int cnt = (int)args.PopInt(1);
                if (cnt <= 0) return;
                currentTutorialInst.Skip(cnt);
                reloadTutorialPage();
                hud.loadHud(currentTutorialInst.PageCode);
            }
        }


        public void StartTutorial(string code)
        {
            currentTutorialInst = tutorials[code];
            currentTutorialInst.Load();
            CurrentTutorial = code;
            hud.TryOpen();
            hud.loadHud(currentTutorialInst.PageCode);
            capi.Network.GetChannel("tutorial").SendPacket(new ActivateTutorialPacket() { Code = code });

            tutorialModeActiveForPlayers.Add(capi.World.Player.PlayerUID);
        }

        private void toggleHud()
        {
            if (hud.IsOpened())
            {
                hud.TryClose();
            } else
            {
                hud.TryOpen();
                hud.loadHud(currentTutorialInst.PageCode);
            }
        }

        private void Event_LeaveWorld_Client()
        {
            currentTutorialInst?.Save();
        }
        

        private void Event_LevelFinalize_Client()
        {
            hud = new HudTutorial(capi);
            capi.Gui.RegisterDialog(hud);

            if (currentTutorialInst != null)
            {
                currentTutorialInst.Load();
                hud.TryOpen();

                tutorialModeActiveForPlayers.Add(capi.World.Player.PlayerUID);
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            sapi.Event.PlayerJoin += Event_PlayerJoin;

            sapi.Network.GetChannel("tutorial").SetMessageHandler<ActivateTutorialPacket>(onActivateTutorial);
        }

        private void onActivateTutorial(IServerPlayer fromPlayer, ActivateTutorialPacket packet)
        {
            ActivateTutorialMode(fromPlayer.PlayerUID);
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            //ActivateTutorialMode(byPlayer.PlayerUID);
        }


        private void ModSystemTutorial_OnInitCustomPages(List<GuiHandbookPage> pages)
        {
            foreach (var val in tutorials)
            {
                pages.Add(new GuiHandbookTutorialPage(capi, "tutorial-" + val.Key));
            }
        }


        private void onStateUpdate(ActionBoolReturn<TutorialStepBase> stepCall)
        {
            if (currentTutorialInst == null) return;

            bool anyDirty = currentTutorialInst.OnStateUpdate(stepCall);

            if (anyDirty)
            {
                reloadTutorialPage();
                hud.loadHud(currentTutorialInst.PageCode);
            }
        }

        private void onCollectedItemstack(ItemStackReceivedPacket packet)
        {
            ItemStack stack = new ItemStack(packet.stackbytes);
            stack.ResolveBlockOrItem(api.World);
            onStateUpdate((step) => step.OnItemStackReceived(stack, packet.eventname));
        }

        private void onBlockPlaced(BlockPlacedPacket packet)
        {
            ItemStack stack = packet.withStackInHands == null ? null : new ItemStack(packet.withStackInHands);
            stack?.ResolveBlockOrItem(api.World);
            onStateUpdate((step) => step.OnBlockPlaced(packet.pos, api.World.Blocks[packet.blockId], stack));
        }
        private void onClientTick200ms(float dt)
        {
            if (capi.World.Player.CurrentBlockSelection == null) return;

            onStateUpdate((step) => step.OnBlockLookedAt(capi.World.Player.CurrentBlockSelection));
        }

        void reloadTutorialPage()
        {           
            GuiDialogHandbook hanndbookdlg = capi.Gui.OpenedGuis.FirstOrDefault(dlg => dlg is GuiDialogHandbook) as GuiDialogHandbook;
            hanndbookdlg?.ReloadPage();
        }



        HashSet<string> eventNames = new HashSet<string>() { "onitemcollected", "onitemcrafted", "onitemknapped", "onitemclayformed", "onitemgrabbed" };

        private void Event_DidPlaceBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            if (tutorialModeActiveForPlayers.Contains(byPlayer.PlayerUID))
            {
                sapi.Network.GetChannel("tutorial").SendPacket(new BlockPlacedPacket()
                {
                    pos = blockSel.Position,
                    blockId = api.World.BlockAccessor.GetBlock(blockSel.Position).Id,
                    withStackInHands = withItemStack?.ToBytes()
                }, byPlayer as IServerPlayer);
            }
        }


        private void onCollectedItem(string eventName, ref EnumHandling handling, IAttribute data)
        {
            if (!eventNames.Contains(eventName)) return;

            var tree = data as TreeAttribute;
            var entityId = tree.GetLong("byentityid");
            var plr = (api.World.GetEntityById(entityId) as EntityPlayer)?.Player;
            if (plr == null) return;

            var stack = tree.GetItemstack("itemstack");

            if (tutorialModeActiveForPlayers.Contains(plr.PlayerUID))
            {
                sapi.Network.GetChannel("tutorial").SendPacket(new ItemStackReceivedPacket() { 
                    eventname = eventName,
                    stackbytes = stack.ToBytes()
                }, plr as IServerPlayer);
            }
        }

        public RichTextComponentBase[] GetPageText(string pagecode, bool skipOld)
        {
            List<RichTextComponentBase> allcomps = new List<RichTextComponentBase>();
            var font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.5).WithFontSize(18);

            var tutorialInst = currentTutorialInst;

            if (tutorialInst == null)
            {
                tutorialInst = tutorials[pagecode.Substring("tutorial-".Length)];
            }

            var steps = tutorialInst.GetTutorialSteps(skipOld);

            steps.Reverse();

            foreach (var step in steps)
            {
                font = font.Clone();
                font.Color[3] = step.Complete ? 0.7 : 1;
                font.WithFontSize(step.Complete ? 15 : 18);

                var comps = step.GetText(font);

                if (step.Complete)
                {
                    allcomps.AddRange(VtmlUtil.Richtextify(capi, "<font color=\"green\"><icon path=\"icons/checkmark.svg\"></icon></font>", font));
                    foreach (var comp in comps)
                    {
                        if (comp is LinkTextComponent lcomp) lcomp.Clickable = false;
                    }
                }
                allcomps.AddRange(comps);
                allcomps.Add(new RichTextComponent(capi, "\n", font));
            }

            return allcomps.ToArray();
        }
    }
}