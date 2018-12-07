using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.ServerMods
{
    public class NpcControl : ModSystem
    {
        ICoreServerAPI sapi;
        Dictionary<string, long> currentEntityIdByPlayerUid = new Dictionary<string, long>();

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;
            api.RegisterCommand("npc", "Npc control", "[list|enqueue or enq|upd|start|clear]", OnCmdNpc, Privilege.controlserver);
            api.RegisterCommand("npcs", "Npc control", "[startall|stopall]", OnCmdNpcs, Privilege.controlserver);

            api.Event.OnPlayerInteractEntity += Event_OnPlayerInteractEntity;
        }

        private void Event_OnPlayerInteractEntity(Entity entity, IPlayer byPlayer, IItemSlot slot, Vec3d hitPosition, int mode, ref EnumHandling handling)
        {
            if (entity is EntityAnimalBot && mode == 1)
            {
                currentEntityIdByPlayerUid[byPlayer.PlayerUID] = entity.EntityId;
                (byPlayer as IServerPlayer).SendMessage(GlobalConstants.CurrentChatGroup, "Ok, npc selected", EnumChatType.Notification);
            }
        }

        private void OnCmdNpcs(IServerPlayer player, int groupId, CmdArgs args)
        {
            string cmd = args.PopWord();
            bool exec = cmd == "startall" || cmd == "startallrandom";

            if (cmd != "startall" && cmd == "stopall" || cmd=="startallrandom")
            {
                player.SendMessage(groupId, "Unknown command", EnumChatType.Notification);
            }

            foreach (var val in sapi.World.LoadedEntities)
            {
                EntityAnimalBot npc = val.Value as EntityAnimalBot;
                if (npc != null)
                {
                    if (exec)
                    {
                        if (cmd == "startallrandom")
                        {
                            sapi.Event.RegisterCallback((dt) => npc.StartExecuteCommands(), (int)(sapi.World.Rand.NextDouble() * 200));
                        } else
                        {
                            npc.StartExecuteCommands();
                        }

                    }
                    else npc.StopExecuteCommands();
                }
            }
        }

        private void OnCmdNpc(IServerPlayer player, int groupId, CmdArgs args)
        {
            long entityid = 0;

            currentEntityIdByPlayerUid.TryGetValue(player.PlayerUID, out entityid);

            if (entityid == 0)
            {
                player.SendMessage(groupId, "Select a npc first", EnumChatType.Notification);
                return;
            }

            Entity entity;
            sapi.World.LoadedEntities.TryGetValue(entityid, out entity);
            EntityAnimalBot entityNpc = entity as EntityAnimalBot;
            
            if (entityNpc == null)
            {
                player.SendMessage(groupId, "No such npc with this id found", EnumChatType.Notification);
                return;
            }
            
            string cmd = args.PopWord();


            switch (cmd)
            {

                case "start":
                    entityNpc.StartExecuteCommands();
                    player.SendMessage(groupId, "Started command execution", EnumChatType.Notification);
                    break;

                case "stop":
                    entityNpc.StopExecuteCommands();
                    player.SendMessage(groupId, "Stopped command execution", EnumChatType.Notification);
                    break;

                case "loop":
                    entityNpc.LoopCommands = (bool)args.PopBool();
                    player.SendMessage(groupId, "Command list set to looping", EnumChatType.Notification);
                    break;

                case "clear":
                    entityNpc.Commands.Clear();
                    if (entityNpc.ExecutingCommands.Count > 0) entityNpc.ExecutingCommands.Peek().Stop();
                    entityNpc.ExecutingCommands.Clear();
                    player.SendMessage(groupId, "Command list cleared", EnumChatType.Notification);
                    break;

                case "remove":
                    int index = (int)args.PopInt();
                    if (index >= 0 && index < entityNpc.Commands.Count)
                    {
                        entityNpc.Commands.RemoveAt(index);

                        player.SendMessage(groupId, "Ok, removed given command", EnumChatType.Notification);
                    } else
                    {
                        player.SendMessage(groupId, "Index out of range or command list empty", EnumChatType.Notification);
                    }

                    break;

                case "list":
                    StringBuilder sb = new StringBuilder();
                    int i = 0;
                    foreach (var val in entityNpc.Commands)
                    {
                        sb.AppendLine(i + ": " + val.ToString());
                        i++;
                    }

                    player.SendMessage(groupId, sb.ToString(), EnumChatType.Notification);

                    break;



                case "upd":
                    {
                        int? idx = args.PopInt(null);
                        if (idx == null)
                        {
                            player.SendMessage(groupId, "No index supplied", EnumChatType.Notification);
                            return;
                        }
                        if (idx < 0 || idx > entityNpc.Commands.Count)
                        {
                            player.SendMessage(groupId, "Index out of range", EnumChatType.Notification);
                            return;
                        }

                        entityNpc.Commands[(int)idx].Update(player, sapi, args);
                        return;
                    }


                case "exec":
                    {
                        string subcmd = args.PopWord();
                        if (subcmd == null)
                        {
                            player.SendMessage(groupId, "Syntax: /npc enq [tp|goto|upd|playanim]", EnumChatType.Notification);
                            return;
                        }

                        Vec3d target = null;


                        if (subcmd == "tp" || subcmd == "goto")
                        {
                            Vec3d spawnpos = sapi.World.DefaultSpawnPosition.XYZ;
                            spawnpos.Y = 0;
                            target = args.PopFlexiblePos(player.Entity.Pos.XYZ, spawnpos);
                        }
                        if (subcmd == "goto")
                        {
                            if (args.Length <= 2)
                            {
                                player.SendMessage(groupId, "Syntax: /npc exec goto x y z animcode speed [animspeed]", EnumChatType.Notification);
                                return;
                            }
                            entityNpc.ExecutingCommands.Enqueue(new NpcGotoCommand(entityNpc, target, args.PopWord(), (float)args.PopFloat(0.02f), (float)args.PopFloat(1)));
                        }

                        if (subcmd == "lookat")
                        {
                            entityNpc.ExecutingCommands.Enqueue(new NpcLookatCommand(entityNpc, (float)args.PopFloat(0)));
                        }

                        if (subcmd == "tp")
                        {
                            entityNpc.ExecutingCommands.Enqueue(new NpcTeleportCommand(entityNpc, target));
                        }

                        if (subcmd == "playanim")
                        {
                            if (args.Length < 1)
                            {
                                player.SendMessage(groupId, "Syntax: /npc exec animcode [animspeed]", EnumChatType.Notification);
                                return;
                            }
                            entityNpc.ExecutingCommands.Enqueue(new NpcPlayAnimationCommand(entityNpc, args.PopWord(), (float)args.PopFloat(1)));
                        }

                        entityNpc.StartExecuteCommands(false);

                        player.SendMessage(groupId, "Started executing. " + entityNpc.ExecutingCommands.Count + " commands in queue", EnumChatType.Notification);
                    }
                    break;

                case "enq":
                case "enqueue":
                    {
                        string subcmd = args.PopWord();
                        if (subcmd == null)
                        {
                            player.SendMessage(groupId, "Syntax: /npc enq [tp|goto|upd|playanim]", EnumChatType.Notification);
                            return;
                        }

                        Vec3d target = null;


                        if (subcmd == "tp" || subcmd == "goto")
                        {
                            Vec3d spawnpos = sapi.World.DefaultSpawnPosition.XYZ;
                            spawnpos.Y = 0;
                            target = args.PopFlexiblePos(player.Entity.Pos.XYZ, spawnpos);
                        }

                        if (subcmd == "goto")
                        {
                            if (args.Length <= 2)
                            {
                                player.SendMessage(groupId, "Syntax: /npc enq goto x y z animcode speed [animspeed]", EnumChatType.Notification);
                                return;
                            }

                            entityNpc.Commands.Add(new NpcGotoCommand(entityNpc, target, args.PopWord(), (float)args.PopFloat(0.02f), (float)args.PopFloat(1)));
                            player.SendMessage(groupId, "Command enqueued", EnumChatType.Notification);
                            return;
                        }

                        if (subcmd == "lookat")
                        {
                            entityNpc.Commands.Add(new NpcLookatCommand(entityNpc, (float)args.PopFloat(0)));
                            player.SendMessage(groupId, "Command enqueued", EnumChatType.Notification);
                        }

                        if (subcmd == "tp")
                        {
                            entityNpc.Commands.Add(new NpcTeleportCommand(entityNpc, target));
                            player.SendMessage(groupId, "Command enqueued", EnumChatType.Notification);
                            return;
                        }

                        if (subcmd == "playanim")
                        {
                            if (args.Length < 1)
                            {
                                player.SendMessage(groupId, "Syntax: /npc enq animcode [animspeed]", EnumChatType.Notification);
                                return;
                            }

                            entityNpc.Commands.Add(new NpcPlayAnimationCommand(entityNpc, args.PopWord(), (float)args.PopFloat(1)));
                            player.SendMessage(groupId, "Command enqueued", EnumChatType.Notification);
                            return;
                        }

                        player.SendMessage(groupId, "Unknown command", EnumChatType.Notification);
                        break;
                    }

                default:
                    player.SendMessage(groupId, "Syntax: /npc [list|enqueue or enq|start|stop|clear|remove|loop]", EnumChatType.Notification);
                    break;
            }

            
        }
    }
}
