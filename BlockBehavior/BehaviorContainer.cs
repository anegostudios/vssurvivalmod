﻿using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorContainer : BlockBehavior
    {
        public BlockBehaviorContainer(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventSubsequent;

            BlockEntity entity = world.BlockAccessor.GetBlockEntity(blockSel.Position);

            if (entity is BlockEntityOpenableContainer)
            {
                BlockEntityOpenableContainer beContainer = (BlockEntityOpenableContainer)entity;
                return beContainer.OnPlayerRightClick(byPlayer, blockSel);
            }

            return false;
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;

            BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);

            if (entity is BlockEntityOpenableContainer)
            {
                BlockEntityOpenableContainer container = (BlockEntityOpenableContainer)entity;

                IPlayer[] players = world.AllOnlinePlayers;
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i].InventoryManager.HasInventory(container.Inventory))
                    {
                        players[i].InventoryManager.CloseInventory(container.Inventory);
                    }
                }
            }
        }

        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs, ref EnumHandling handled)
        {
            base.Activate(world, caller, blockSel, activationArgs, ref handled);
            BlockEntity entity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            int timeOut = (int)(activationArgs.TryGetLong("close") ?? 2000);
            if (entity is BlockEntityOpenableContainer container)
            {
                var open = SerializerUtil.Serialize(new OpenContainerLidPacket(caller.Entity.EntityId, true));
                ((ICoreServerAPI)world.Api).Network.BroadcastBlockEntityPacket(
                    blockSel.Position,
                    (int)EnumBlockContainerPacketId.OpenLidOthers,
                    open
                );
                (world.Api as ICoreServerAPI).World.PlaySoundAt(container.OpenSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);
                world.Api.Event.RegisterCallback((d) =>
                {
                    var close = SerializerUtil.Serialize(new OpenContainerLidPacket(caller.Entity.EntityId, false));
                    ((ICoreServerAPI)world.Api).Network.BroadcastBlockEntityPacket(
                        blockSel.Position,
                        (int)EnumBlockContainerPacketId.OpenLidOthers,
                        close
                    );
                    (world.Api as ICoreServerAPI).World.PlaySoundAt(container.CloseSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);
                }, timeOut);
            }
        }
    }
}
