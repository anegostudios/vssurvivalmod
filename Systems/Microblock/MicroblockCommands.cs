using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using System.Collections.Generic;
using System;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class MicroblockCommands
    {
        ICoreServerAPI sapi;

        public void Start(ICoreServerAPI api)
        {
            sapi = api;
            var parsers = sapi.ChatCommands.Parsers;

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
                    .BeginSub("setname")
                        .WithDesc("Set multiple block names")
                        .WithArgs(parsers.All("name"))
                        .HandleWith((args) => onCmdSetName(args, false))
                    .EndSub()
                    .BeginSub("delete")
                        .WithDesc("Delete a material from microblocks (select material with held block)")
                        .HandleWith((args) => onCmdFill(args, true))
                    .EndSub()
                    .BeginSub("deletemat")
                        .WithDesc("Delete a named material from microblocks")
                        .WithArgs(parsers.Word("material code"))
                        .HandleWith(onCmdDeleteMat)
                    .EndSub()
                    .BeginSub("removeunused")
                        .WithDesc("Remove any unused materials from microblocks")
                        .HandleWith(onCmdRemoveUnused)
                    .EndSub()

                    .BeginSubCommand("editable")
                        .WithDescription("Upgrade/Downgrade chiseled blocks to an editable/non-editable state in given area")
                        .WithArgs(parsers.Bool("editable"))
                        .HandleWith(onCmdEditable)
                    .EndSubCommand()

                .EndSub()
            ;
        }


        private int WalkMicroBlocks(BlockPos startPos, BlockPos endPos, ActionBoolReturn<BlockEntityMicroBlock> action)
        {
            int cnt = 0;
            var ba = sapi.World.BlockAccessor;
            BlockPos tmpPos = new BlockPos(startPos.dimension);
            BlockPos.Walk(startPos, endPos, ba.MapSize, (x, y, z) =>
            {
                tmpPos.Set(x, y, z);
                var be = ba.GetBlockEntity<BlockEntityMicroBlock>(tmpPos);

                if (be != null)
                {
                    if (action(be)) cnt++;
                }
            });

            return cnt;
        }


        private void GetMarkedArea(Caller caller, out BlockPos startPos, out BlockPos endPos)
        {
            var uid = caller.Player.PlayerUID;
            startPos = null;
            endPos = null;

            if (sapi.ObjectCache.TryGetValue("weStartMarker-" + uid, out var start))
            {
                startPos = start as BlockPos;
            }

            if (sapi.ObjectCache.TryGetValue("weEndMarker-" + uid, out var end))
            {
                endPos = end as BlockPos;
            }
        }


        private TextCommandResult onCmdSetName(TextCommandCallingArgs args, bool v)
        {
            string name = args[0] as string;

            GetMarkedArea(args.Caller, out BlockPos startPos, out BlockPos endPos);
            if (startPos == null || endPos == null)
            {
                return TextCommandResult.Error("Please mark area with world edit");
            }

            int cnt = WalkMicroBlocks(startPos, endPos, (be) =>
            {
                be.BlockName = name;
                be.MarkDirty(true);
                return true;
            });

            return TextCommandResult.Success(cnt + " microblocks modified");
        }


        private TextCommandResult onCmdClearName(TextCommandCallingArgs args, bool v)
        {
            GetMarkedArea(args.Caller, out BlockPos startPos, out BlockPos endPos);
            if (startPos == null || endPos == null)
            {
                return TextCommandResult.Error("Please mark area with world edit");
            }

            int cnt = WalkMicroBlocks(startPos, endPos, (be) =>
            {
                if (be.BlockName != null && be.BlockName != "")
                {
                    be.BlockName = null;
                    be.MarkDirty(true);
                    return true;
                }

                return false;
            });

            return TextCommandResult.Success(cnt + " microblocks modified");
        }


        private TextCommandResult onCmdFill(TextCommandCallingArgs args, bool delete)
        {
            GetMarkedArea(args.Caller, out BlockPos startPos, out BlockPos endPos);
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

            materialBlock = fillWitBlock;
            int cnt = WalkMicroBlocks(startPos, endPos, delete ? DeleteMaterial : FillMaterial);

            return TextCommandResult.Success(cnt + " microblocks modified");
        }


        private TextCommandResult onCmdDeleteMat(TextCommandCallingArgs args)
        {
            GetMarkedArea(args.Caller, out BlockPos startPos, out BlockPos endPos);
            if (startPos == null || endPos == null)
            {
                return TextCommandResult.Error("Please mark area with world edit");
            }

            string matCode = (string)args[0];
            Block fillWitBlock = sapi.World.BlockAccessor.GetBlock(new AssetLocation(matCode));

            if (fillWitBlock == null)
            {
                return TextCommandResult.Error("Unknown block code: " + matCode);
            }
            if (fillWitBlock is BlockMicroBlock)
            {
                return TextCommandResult.Error("Cannot use micro block as a material inside microblocks");
            }

            materialBlock = fillWitBlock;
            int cnt = WalkMicroBlocks(startPos, endPos, DeleteMaterial);

            return TextCommandResult.Success(cnt + " microblocks modified");
        }


        private TextCommandResult onCmdRemoveUnused(TextCommandCallingArgs args)
        {
            GetMarkedArea(args.Caller, out BlockPos startPos, out BlockPos endPos);
            if (startPos == null || endPos == null)
            {
                return TextCommandResult.Error("Please mark area with world edit");
            }

            HashSet<string> removedMaterials = new();
            int cnt = WalkMicroBlocks(startPos, endPos, (be) => RemoveUnused(be, removedMaterials));

            string message = cnt + " microblocks modified";
            if (removedMaterials.Count > 0)
            {
                message += ", removed materials: ";
                bool comma = false;
                foreach (string mat in removedMaterials)
                {
                    if (comma) message += ", ";
                    else comma = true;
                    message += mat;
                }
            }

            return TextCommandResult.Success(message);
        }


        Block materialBlock;
        private bool DeleteMaterial(BlockEntityMicroBlock be)
        {
            int delmatindex = be.BlockIds.IndexOf(materialBlock.Id);
            if (delmatindex < 0) return false;

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
            return true;
        }


        private bool FillMaterial(BlockEntityMicroBlock be)
        {
            be.BeginEdit(out var voxels, out var voxMats);

            if (fillMicroblock(materialBlock, be, voxels, voxMats))
            {
                be.EndEdit(voxels, voxMats);
                be.MarkDirty(true);
                return true;
            }

            return false;
        }


        private static bool fillMicroblock(Block fillWitBlock, BlockEntityMicroBlock be, BoolArray16x16x16 voxels, byte[,,] voxMats)
        {
            bool edited = false;
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

        private bool RemoveUnused(BlockEntityMicroBlock be, HashSet<string> materialsRemoved)
        {
            bool edited = false;
            for (int i = 0; i < be.BlockIds.Length; i++)
            {
                if (be.NoVoxelsWithMaterial((uint)i))
                {
                    Block material = sapi.World.BlockAccessor.GetBlock(be.BlockIds[i]);
                    be.RemoveMaterial(material);
                    materialsRemoved.Add(material.Code.ToShortString());
                    edited = true;
                    i--;
                }
            }

            if (edited) be.MarkDirty(true);
            return edited;
        }




        private TextCommandResult onCmdEditable(TextCommandCallingArgs args)
        {
            GetMarkedArea(args.Caller, out BlockPos startPos, out BlockPos endPos);
            if (startPos == null || endPos == null)
            {
                return TextCommandResult.Error("Please mark area with world edit");
            }

            var editable = (bool)args.Parsers[0].GetValue();

            Block chiselBlock = sapi.World.GetBlock(new AssetLocation("chiseledblock"));
            Block microblock = sapi.World.GetBlock(new AssetLocation("microblock"));
            Block targetBlock = editable ? chiselBlock : microblock;

            var ba = sapi.World.BlockAccessor;


            int cnt = WalkMicroBlocks(startPos, endPos, (be) =>
            {
                var pos = be.Pos;
                Block block = ba.GetBlock(pos);
                if (block is BlockMicroBlock && block.Id != targetBlock.Id)
                {
                    TreeAttribute tree = new TreeAttribute();
                    be.ToTreeAttributes(tree);
                    sapi.World.BlockAccessor.SetBlock(targetBlock.Id, pos);

                    be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
                    be.FromTreeAttributes(tree, sapi.World);

                    return true;
                }

                return false;
            });

            return TextCommandResult.Success(cnt + " microblocks modified");
        }
    }
}
