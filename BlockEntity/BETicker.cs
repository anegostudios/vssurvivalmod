using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityTicker : BlockEntity
    {
        GuiDialogBlockEntityTicker clientDialog;
        long listenerId;
        bool active;

        /// <summary>
        /// Time (in ms) between attempted auto-execute calls
        /// </summary>
        private int tickIntervalMs;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreServerAPI && tickIntervalMs > 0 && active)
            {
                listenerId = RegisterGameTickListener(OnGameTick, tickIntervalMs);
            }
        }

        private void OnGameTick(float dt)
        {
            TryInteract(BlockFacing.NORTH);
            TryInteract(BlockFacing.EAST);
            TryInteract(BlockFacing.SOUTH);
            TryInteract(BlockFacing.WEST);
        }

        private void TryInteract(BlockFacing facing)
        {
            Block block = Api.World.BlockAccessor.GetBlockOnSide(Pos, facing);
            if (block != null)
            {
                try
                {
                    var caller = new Caller() { 
                        CallerPrivileges = new string[] { "*" }, 
                        Pos = Pos.ToVec3d(), 
                        Type = EnumCallerType.Block 
                    };

                    block.Activate(Api.World, caller, new BlockSelection(Pos.AddCopy(facing), facing.Opposite, block));
                }
                catch (Exception e)
                {
                    Api.Logger.Warning("Exception thrown when trying to interact with block {0}:", block.Code.ToShortString());
                    Api.Logger.Warning(e);
                }
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            tickIntervalMs = tree.GetInt("tickIntervalMs");
            active = tree.GetBool("active");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("tickIntervalMs", tickIntervalMs);
            tree.SetBool("active", active);
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

        public bool OnInteract(IPlayer byPlayer)
        {
            if (byPlayer == null || !byPlayer.Entity.Controls.ShiftKey) return false;
            
            if (Api.Side == EnumAppSide.Client && BlockEntityGuiConfigurableCommands.CanEditCommandblocks(byPlayer))
            {
                if (clientDialog != null)
                {
                    clientDialog.TryClose();
                    clientDialog.Dispose();
                    clientDialog = null;
                    return true;
                }

                clientDialog = new GuiDialogBlockEntityTicker(Pos, tickIntervalMs, active, Api as ICoreClientAPI);
                clientDialog.TryOpen();
                clientDialog.OnClosed += () => {
                    clientDialog?.Dispose(); clientDialog = null;
                };
            } else
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "noprivilege", "Can only be edited in creative mode and with controlserver privlege");
            }

            return true;
        }


        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(fromPlayer, packetid, data);

            if (packetid == 12 && BlockEntityGuiConfigurableCommands.CanEditCommandblocks(fromPlayer))
            {
                var packet = SerializerUtil.Deserialize<EditTickerPacket>(data);
                tickIntervalMs = packet.Interval.ToInt(500);
                active = packet.Active;

                if (listenerId > 0)
                {
                    UnregisterGameTickListener(listenerId);
                    listenerId = 0;
                }
                if (active && tickIntervalMs > 0)
                {
                    listenerId = RegisterGameTickListener(OnGameTick, tickIntervalMs);
                }

                MarkDirty(true);
            }
        }
    }
}
