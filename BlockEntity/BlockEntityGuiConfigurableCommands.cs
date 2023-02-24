using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityCommands : BlockEntity
    {
        public string Commands = "";
        public string[] CallingPrivileges;
        public bool Silent;
        public bool executing;

        public virtual void Execute(Caller caller, string Commands)
        {
            if (Commands == null) return;
            if (Api.Side == EnumAppSide.Server && !executing)
            {
                string[] commands = Commands.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

                executing = true;
                try
                {
                    execCommands(commands, caller);
                }
                catch
                {
                    executing = false;
                }

                if (commands.Length > 0 && caller.Player != null)
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/toggleswitch"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, false, 16, 0.5f);
                }
            }
        }


        public void FinishExecution()
        {
            executing = false;
        }


        protected virtual void execCommands(IEnumerable<string> commands, Caller caller)
        {
            caller.Type = EnumCallerType.Block;
            caller.Pos = this.Pos.ToVec3d();
            caller.CallerPrivileges = CallingPrivileges;

            List<string> commandsAfterWait = null;
            int waitMs = 0;

            foreach (var command in commands)
            {
                if (commandsAfterWait == null && command.StartsWith("/wait"))
                {
                    waitMs = command.Split(' ')[1].ToInt();
                    commandsAfterWait = new List<string>();
                    continue;
                }
                if (commandsAfterWait != null) commandsAfterWait.Add(command);
                else Api.ChatCommands.ExecuteUnparsed(command, new TextCommandCallingArgs() { Caller = caller }, (result) =>
                {
                    if (!Silent) Api.Logger.Notification("{0}: {1}", command, result.StatusMessage);
                });
            }

            if (commandsAfterWait != null)
            {
                Api.Event.RegisterCallback((dt) =>
                {
                    execCommands(commandsAfterWait, caller);
                }, waitMs);
            }
            else FinishExecution();
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            Commands = tree.GetString("commands");
            Silent = tree.GetBool("silent");
            CallingPrivileges = (tree["callingPrivileges"] as StringArrayAttribute)?.value;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("commands", Commands);
            tree.SetBool("silent", Silent);
            if (CallingPrivileges != null)
            {
                tree["callingPrivileges"] = new StringArrayAttribute(CallingPrivileges);
            }
        }
    }


    public class BlockEntityGuiConfigurableCommands : BlockEntityCommands
    {
        protected GuiDialogBlockEntity clientDialog;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Block.Attributes["runOnInitialize"]?.AsBool(false) == true)
            {
                RegisterDelayedCallback((dt) =>
                {
                    try
                    {
                        var caller = new Caller()
                        {
                            CallerPrivileges = new string[] { "*" },
                            Pos = Pos.ToVec3d(),
                            Type = EnumCallerType.Block
                        };

                        OnInteract(caller);
                    }
                    catch (Exception e)
                    {
                        Api.Logger.Warning("Exception thrown when trying to call commands on init with block: {1}", e);
                    }
                }, 2000);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (byItemStack == null) return; // Placed by worldgen
            
            Commands = byItemStack?.Attributes.GetString("commands") ?? "";
            CallingPrivileges = (byItemStack?.Attributes["callingPrivileges"] as StringArrayAttribute)?.value;
        }

        public virtual bool OnInteract(Caller caller)
        {
            if (caller.Player != null && caller.Player.Entity.Controls.ShiftKey)
            {
                if (Api.Side == EnumAppSide.Client && caller.Player.WorldData.CurrentGameMode == EnumGameMode.Creative && caller.Player.HasPrivilege("controlserver"))
                {
                    if (clientDialog != null)
                    {
                        clientDialog.TryClose();
                        clientDialog.Dispose();
                        clientDialog = null;
                        return true;
                    }

                    clientDialog = new GuiDialogBlockEntityCommand(Pos, Commands, Silent, Api as ICoreClientAPI, "Command editor");
                    clientDialog.TryOpen();
                    clientDialog.OnClosed += () => {
                        clientDialog?.Dispose(); clientDialog = null;
                    };
                } else
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "noprivilege", "Can only be edited in creative mode and with controlserver privlege");
                }

                return false;
            }

            Execute(caller, this.Commands);
            return true;
        }


        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);

            if (packetid == 12 && CanEditCommandblocks(fromPlayer))
            {
                CallingPrivileges = (fromPlayer as IServerPlayer).Role.AutoGrant ? new string[] { "*" } : fromPlayer.Privileges;
                UpdateFromPacket(data);
            }
        }

        public static bool CanEditCommandblocks(IPlayer player)
        {
            return player.WorldData.CurrentGameMode == EnumGameMode.Creative && (player.HasPrivilege("controlserver") || player.Entity.World.Config.GetBool("allowCreativeModeCommandBlocks") == true);
        }

        protected virtual void UpdateFromPacket(byte[] data)
        {
            var packet = SerializerUtil.Deserialize<BlockEntityCommandPacket>(data);
            this.Commands = packet.Commands;
            this.Silent = packet.Silent;
            MarkDirty(true);
        }




        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            clientDialog?.TryClose();
            clientDialog?.Dispose();
            clientDialog = null;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            clientDialog?.TryClose();
            clientDialog?.Dispose();
            clientDialog = null;
        }

    }
}
