using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using System.Collections.Generic;

namespace Vintagestory.ServerMods
{
    [ProtoContract]
    internal class SetDetailModePacket
    {
        [ProtoMember(1)]
        public bool Enable;
    }

    internal class ModSystemDetailModeSync : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;
        ICoreServerAPI sapi;
        public override void Start(ICoreAPI api)
        {
            sapi = api as ICoreServerAPI;
            api.Network.RegisterChannel("detailmodesync").RegisterMessageType<SetDetailModePacket>();
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network.GetChannel("detailmodesync").SetMessageHandler<SetDetailModePacket>(onPacket);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            api.ChatCommands.GetOrCreate("we")
                .BeginSub("microblock")
                    .WithDesc("Microblock operations")
                    .BeginSub("fill")
                        .WithDesc("Fill empty space of microblocks with held block")
                        .HandleWith((args) => onCmdFill(args, false))
                    .EndSub()
                    .BeginSub("clearname")
                        .WithDesc("Delete all block names")
                        .HandleWith((args) => onCmdClearName(args, false))
                    .EndSub()
                    .BeginSub("delete")
                        .WithDesc("Delete a material from microblocks (select material with held block)")
                        .HandleWith((args) => onCmdFill(args, true))
                    .EndSub()
                .EndSub()
            ;
        }

        private TextCommandResult onCmdClearName(TextCommandCallingArgs args, bool v)
        {
            var uid = args.Caller.Player.PlayerUID;
            BlockPos startPos = null, endPos = null;

            if (sapi.ObjectCache.TryGetValue("weStartMarker-" + uid, out var start))
            {
                startPos = start as BlockPos;
            }

            if (sapi.ObjectCache.TryGetValue("weEndMarker-" + uid, out var end))
            {
                endPos = end as BlockPos;
            }

            if (startPos == null || endPos == null)
            {
                return TextCommandResult.Error("Please mark area with world edit");
            }

            int cnt = 0;

            var ba = sapi.World.BlockAccessor;
            BlockPos tmpPos = new BlockPos();
            BlockPos.Walk(startPos, endPos, ba.MapSize, (x, y, z) =>
            {
                tmpPos.Set(x, y, z);
                var be = ba.GetBlockEntity<BlockEntityMicroBlock>(tmpPos);

                if (be != null && be.BlockName != null && be.BlockName != "")
                {
                    be.BlockName = null;
                    be.MarkDirty(true);
                    cnt++;
                }
            });

            return TextCommandResult.Success(cnt + " microblocks modified");
        }

        private TextCommandResult onCmdFill(TextCommandCallingArgs args, bool delete)
        {
            var uid = args.Caller.Player.PlayerUID;
            BlockPos startPos = null, endPos=null;

            if (sapi.ObjectCache.TryGetValue("weStartMarker-" + uid, out var start))
            {
                startPos = start as BlockPos;
            }

            if (sapi.ObjectCache.TryGetValue("weEndMarker-" + uid, out var end))
            {
                endPos = end as BlockPos;
            }

            if (startPos == null || endPos == null)
            {
                return TextCommandResult.Error("Please mark area with world edit");
            }

            Block fillWitBlock = args.Caller.Player?.InventoryManager.ActiveHotbarSlot.Itemstack?.Block;

            if (fillWitBlock == null)
            {
                return TextCommandResult.Error("Please hold replacement material in active hands");
            }
            if (fillWitBlock is BlockMicroBlock)
            {
                return TextCommandResult.Error("Cannot use micro block as a material inside microblocks");
            }

            int cnt = 0;

            var ba = sapi.World.BlockAccessor;
            BlockPos tmpPos = new BlockPos();
            BlockPos.Walk(startPos, endPos, ba.MapSize, (x, y, z) =>
            {
                tmpPos.Set(x, y, z);
                var be = ba.GetBlockEntity<BlockEntityMicroBlock>(tmpPos);

                if (be != null)
                {
                    if (delete)
                    {
                        int delmatindex = be.BlockIds.IndexOf(fillWitBlock.Id);
                        if (delmatindex < 0) return;
                        cnt++;
                        List<uint> cwms = new List<uint>();
                        List<int> blockids = new List<int>();
                        CuboidWithMaterial cwm = new CuboidWithMaterial();
                        for (int i = 0; i < be.VoxelCuboids.Count; i++)
                        {
                            BlockEntityMicroBlock.FromUint(be.VoxelCuboids[i], cwm);

                            if (delmatindex != cwm.Material)
                            {
                                int blockId = be.BlockIds[cwm.Material];
                                int matindex = blockids.IndexOf(blockId);
                                if (matindex < 0) { blockids.Add(blockId); matindex = blockids.Count - 1; }

                                cwm.Material = (byte)matindex;
                                cwms.Add(BlockEntityMicroBlock.ToUint(cwm));
                            }
                        }

                        be.VoxelCuboids = cwms;
                        be.BlockIds = blockids.ToArray();

                        be.MarkDirty(true);
                    }
                    else
                    {
                        bool edited = false;
                        be.BeginEdit(out var voxels, out var voxMats);
                        edited = fillMicroblock(fillWitBlock, be, voxels, voxMats, edited);

                        if (edited)
                        {
                            cnt++;
                            be.EndEdit(voxels, voxMats);
                            be.MarkDirty(true);
                        }
                    }
                }
            });

            return TextCommandResult.Success(cnt + " microblocks modified");
        }



        private static bool fillMicroblock(Block fillWitBlock, BlockEntityMicroBlock be, BoolArray16x16x16 voxels, byte[,,] voxMats, bool edited)
        {
            byte matIndex = 0;
            for (int dx = 0; dx < 16; dx++)
            {
                for (int dy = 0; dy < 16; dy++)
                {
                    for (int dz = 0; dz < 16; dz++)
                    {
                        if (!voxels[dx, dy, dz])
                        {
                            if (!edited)
                            {

                                var index = be.BlockIds.IndexOf(fillWitBlock.Id);
                                if (be is BlockEntityChisel bec)
                                {
                                    index = bec.AddMaterial(fillWitBlock);
                                }
                                else
                                {
                                    if (index < 0)
                                    {
                                        be.BlockIds = be.BlockIds.Append(fillWitBlock.Id);
                                        index = be.BlockIds.Length - 1;
                                    }
                                }

                                matIndex = (byte)index;
                            }

                            voxels[dx, dy, dz] = true;
                            voxMats[dx, dy, dz] = matIndex;
                            edited = true;
                        }
                    }
                }
            }

            return edited;
        }

        private void onPacket(SetDetailModePacket packet)
        {
            BlockEntityChisel.ForceDetailingMode = packet.Enable;
        }

        internal void Toggle(string playerUID, bool on)
        {
            sapi.Network.GetChannel("detailmodesync").SendPacket(new SetDetailModePacket() { Enable = on }, sapi.World.PlayerByUid(playerUID) as IServerPlayer);
        }
    }
}
