using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.WorldEdit;

#nullable disable

namespace Vintagestory.ServerMods
{
    public static class ChiselToolRegisterUtil
    {
        public static void Register(ModSystem mod)
        {
            ((WorldEdit.WorldEdit)mod).RegisterTool("chiselbrush", typeof(MicroblockTool));
        }
    }

    public class ChiselBlockInEdit
    {
        public BoolArray16x16x16 voxels;
        public byte[,,] voxelMaterial;

        public BlockEntityChisel be;
        public bool isNew;
    }

    internal class MicroblockTool : PaintBrushTool
    {
        Dictionary<BlockPos, ChiselBlockInEdit> blocksInEdit;

        public MicroblockTool()
        {
        }

        public MicroblockTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
            blocksInEdit = new Dictionary<BlockPos, ChiselBlockInEdit>();
        }

        public override void Load(ICoreAPI api)
        {
            api.ModLoader.GetModSystem<ModSystemDetailModeSync>().Toggle(workspace.PlayerUID, true);
        }

        public override void Unload(ICoreAPI api)
        {
            api.ModLoader.GetModSystem<ModSystemDetailModeSync>().Toggle(workspace.PlayerUID, false);
        }

        public override void HighlightBlocks(IPlayer player, ICoreServerAPI sapi, EnumHighlightBlocksMode mode)
        {
            sapi.World.HighlightBlocks(
                player, (int)EnumHighlightSlot.Brush, GetBlockHighlights(), GetBlockHighlightColors(),
                workspace.ToolOffsetMode == EnumToolOffsetMode.Center
                    ? EnumHighlightBlocksMode.CenteredToBlockSelectionIndex
                    : EnumHighlightBlocksMode.AttachedToBlockSelectionIndex,
                GetBlockHighlightShape(),
                1 / 16f
            );
        }

        public override void OnBreak(WorldEdit.WorldEdit worldEdit, BlockSelection blockSel, ref EnumHandling handling)
        {
            OnBuild(worldEdit, ba.GetBlock(blockSel.Position).Id, blockSel, null);
        }

        public override void OnBuild(WorldEdit.WorldEdit worldEdit, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            base.OnBuild(worldEdit, oldBlockId, blockSel, withItemStack);

            foreach (var val in blocksInEdit)
            {
                if (val.Value.isNew)
                {
                    var be = ba.GetBlockEntity(val.Value.be.Pos) as BlockEntityChisel;
                    TreeAttribute tree = new TreeAttribute();
                    val.Value.be.ToTreeAttributes(tree);
                    be.FromTreeAttributes(tree, worldEdit.sapi.World);

                    be.RebuildCuboidList();
                    be.MarkDirty(true);
                    continue;
                }

                val.Value.be.MarkDirty(true);
            }
        }

        public override void PerformBrushAction(WorldEdit.WorldEdit worldEdit, Block placedBlock, int oldBlockId, BlockSelection blockSel,
            BlockPos targetPos, ItemStack withItemStack)
        {
            if (BrushDim1 <= 0) return;

            var blockSelFace = blockSel.Face;
            targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSelFace.Opposite) : blockSel.Position;

            Block hereBlock = ba.GetBlock(targetPos);
            BlockChisel selectedBlock = hereBlock as BlockChisel;
            if (selectedBlock == null)
            {
                selectedBlock = ba.GetBlock(new AssetLocation("chiseledblock")) as BlockChisel;
            }

            if (withItemStack != null && withItemStack.Block != null &&
                !ItemChisel.IsValidChiselingMaterial(worldEdit.sapi, targetPos, withItemStack.Block, worldEdit.sapi.World.PlayerByUid(workspace.PlayerUID)))
            {
                (worldEdit.sapi.World.PlayerByUid(workspace.PlayerUID) as IServerPlayer).SendIngameError("notmicroblock",
                    Lang.Get("Must have a chisel material in hands"));
                return;
            }

            var targetBe = ba.GetBlockEntity(targetPos) as BlockEntityChisel;

            var voxelpos = new Vec3i(
                Math.Min(16, (int)(blockSel.HitPosition.X * 16)),
                Math.Min(16, (int)(blockSel.HitPosition.Y * 16)),
                Math.Min(16, (int)(blockSel.HitPosition.Z * 16))
            );
            var matpos = new Vec3i(
                Math.Min(15, (int)(blockSel.HitPosition.X * 16)),
                Math.Min(15, (int)(blockSel.HitPosition.Y * 16)),
                Math.Min(15, (int)(blockSel.HitPosition.Z * 16))
            );
            var attachmentPoints = new BlockPos[6];
            for (var i = 0; i < BlockFacing.NumberOfFaces; i++)
            {
                var n = BlockFacing.ALLNORMALI[i];
                    attachmentPoints[i] = new BlockPos((int)(size.X / 2f * n.X), (int)(size.Y / 2f * n.Y), (int)(size.Z / 2f * n.Z));
            }
            if (workspace.ToolOffsetMode == EnumToolOffsetMode.Attach)
            {
                voxelpos.X += attachmentPoints[blockSel.Face.Index].X;
                voxelpos.Y += attachmentPoints[blockSel.Face.Index].Y;
                voxelpos.Z += attachmentPoints[blockSel.Face.Index].Z;

                // the below mess fixes wierd offset by one issues in various orientations, sizes and for different shapes
                if (attachmentPoints[blockSel.Face.Index].X < 0)
                {
                    voxelpos.X -= 1;
                }

                if (BrushShape == EnumBrushShape.Cuboid)
                {
                    if (attachmentPoints[blockSel.Face.Index].X < 0 && BrushDim1 % 2 == 0)
                    {
                        voxelpos.X += 1;
                    }
                    if (attachmentPoints[blockSel.Face.Index].Y < 0 && BrushDim2 % 2 != 0)
                    {
                        voxelpos.Y -= 1;
                    }
                    if (attachmentPoints[blockSel.Face.Index].Z < 0 && BrushDim3 % 2 != 0)
                    {
                        voxelpos.Z -= 1;
                    }

                    if (size.Y == 1 && blockSel.Face.Index == BlockFacing.DOWN.Index)
                    {
                        voxelpos.Y -= 1;
                    }
                    if (size.X == 1 && blockSel.Face.Index == BlockFacing.WEST.Index)
                    {
                        voxelpos.X -= 1;
                    }
                    if (size.Z == 1 && blockSel.Face.Index == BlockFacing.NORTH.Index)
                    {
                        voxelpos.Z -= 1;
                    }
                }
                else if (BrushShape == EnumBrushShape.Cylinder)
                {
                    if (attachmentPoints[blockSel.Face.Index].Y < 0 && size.Y > 2)
                    {
                        voxelpos.Y -= 1;
                    }
                    if (attachmentPoints[blockSel.Face.Index].Z < 0)
                    {
                        voxelpos.Z -= 1;
                    }
                    if (size.Y == 1 && blockSel.Face.Index == BlockFacing.DOWN.Index)
                    {
                        voxelpos.Y -= 1;
                    }
                }
                else
                {
                    if (attachmentPoints[blockSel.Face.Index].Y < 0)
                    {
                        voxelpos.Y -= 1;
                    }

                    if (attachmentPoints[blockSel.Face.Index].Z < 0)
                    {
                        voxelpos.Z -= 1;
                    }
                }
            }

            if (oldBlockId >= 0)
            {
                if (placedBlock.ForFluidsLayer)
                {
                    worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position, BlockLayersAccess.Fluid);
                }
                else
                {
                    worldEdit.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);
                }
            }

            EnumBrushMode brushMode = BrushMode;

            int blockId = withItemStack?.Block?.BlockId ?? 0;

            if (!workspace.MayPlace(ba.GetBlock(blockId), brushPositions.Length)) return;

            BlockPos tmpPos = new BlockPos(targetPos.dimension);
            Vec3i dvoxelpos = new Vec3i();

            blocksInEdit.Clear();

            int selectedMatId = targetBe?.GetVoxelMaterialAt(matpos) ?? hereBlock.Id;


            long voxelWorldX, voxelWorldY, voxelWorldZ;

            for (int i = 0; i < brushPositions.Length; i++)
            {
                var brushPos = brushPositions[i];

                voxelWorldX = targetPos.X * 16 + brushPos.X + voxelpos.X;
                voxelWorldY = targetPos.Y * 16 + brushPos.Y + voxelpos.Y;
                voxelWorldZ = targetPos.Z * 16 + brushPos.Z + voxelpos.Z;

                BlockPos dpos = tmpPos.Set((int)(voxelWorldX / 16), (int)(voxelWorldY / 16), (int)(voxelWorldZ / 16));
                dvoxelpos.Set((int)GameMath.Mod(voxelWorldX, 16), (int)GameMath.Mod(voxelWorldY, 16), (int)GameMath.Mod(voxelWorldZ, 16));

                if (!blocksInEdit.TryGetValue(dpos, out ChiselBlockInEdit editData))
                {
                    bool isNew = false;

                    var hereblock = ba.GetBlock(dpos);
                    var be = ba.GetBlockEntity(dpos) as BlockEntityChisel;
                    if (be == null)
                    {
                        if (withItemStack != null &&
                            ((brushMode == EnumBrushMode.ReplaceAir && hereblock.Id == 0) || brushMode == EnumBrushMode.Fill))
                        {
                            ba.SetBlock(selectedBlock.Id, dpos);
                            string blockName = withItemStack.GetName();
                            be = new BlockEntityChisel();
                            be.Pos = dpos.Copy();
                            be.CreateBehaviors(selectedBlock, worldEdit.sapi.World);
                            be.Initialize(worldEdit.sapi);
                            be.WasPlaced(withItemStack.Block, blockName);
                            be.VoxelCuboids = new List<uint>();
                            isNew = true;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    var hereblockId = hereblock.Id;
                    if (!isNew)
                    {
                        ba.SetHistoryStateBlock(dpos.X, dpos.Y, dpos.Z, hereblockId, hereblockId);
                    }
                    else
                    {
                        ba.SetHistoryStateBlock(dpos.X, dpos.Y, dpos.Z, 0, selectedBlock.Id);
                    }

                    be.BeginEdit(out var voxels, out var voxMats);
                    blocksInEdit[dpos.Copy()] = editData = new ChiselBlockInEdit()
                    {
                        voxels = voxels,
                        voxelMaterial = voxMats,
                        be = be,
                        isNew = isNew
                    };
                }

                int hereMatBlockId = 0;
                if (editData.voxels[dvoxelpos.X, dvoxelpos.Y, dvoxelpos.Z])
                {
                    hereMatBlockId = editData.be.BlockIds[editData.voxelMaterial[dvoxelpos.X, dvoxelpos.Y, dvoxelpos.Z]];
                }

                bool setHere;
                switch (brushMode)
                {
                    case EnumBrushMode.ReplaceAir:
                        setHere = hereMatBlockId == 0;
                        break;

                    case EnumBrushMode.ReplaceNonAir:
                        setHere = hereMatBlockId != 0;
                        break;

                    case EnumBrushMode.ReplaceSelected:
                        setHere = hereMatBlockId == selectedMatId;
                        break;

                    default:
                        setHere = true;
                        break;
                }

                if (setHere)
                {
                    if (blockId == 0)
                    {
                        editData.voxels[dvoxelpos.X, dvoxelpos.Y, dvoxelpos.Z] = false;
                    }
                    else
                    {
                        var matId = editData.be.BlockIds.IndexOf(blockId);
                        if (matId < 0) matId = editData.be.AddMaterial(ba.GetBlock(blockId));
                        editData.voxels[dvoxelpos.X, dvoxelpos.Y, dvoxelpos.Z] = true;
                        editData.voxelMaterial[dvoxelpos.X, dvoxelpos.Y, dvoxelpos.Z] = (byte)matId;
                    }
                }
            }

            foreach (var val in blocksInEdit)
            {
                val.Value.be.EndEdit(val.Value.voxels, val.Value.voxelMaterial);

                if (val.Value.be.VoxelCuboids.Count == 0)
                {
                    ba.SetBlock(0, val.Key);
                    ba.RemoveBlockLight(val.Value.be.GetLightHsv(ba), val.Key);
                }
            }
        }
    }
}
