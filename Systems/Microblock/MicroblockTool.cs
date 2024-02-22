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

namespace Vintagestory.ServerMods
{
    public static class ChiselToolRegisterUtil
    {
        public static void Register(ModSystem mod)
        {
            ((WorldEdit.WorldEdit)mod).RegisterTool("chiselbrush", typeof(MicroblockTool));
        }

        public static void RegisterChiselBlockBehavior(ICoreAPI api)
        {
           /* foreach (var block in api.World.Blocks)
            {
                if (block is BlockChisel)
                {
                    block.BlockBehaviors = block.BlockBehaviors.Append(new BlockBehaviorWorldEditFixGhostBlockPlace(block));
                    block.CollectibleBehaviors = block.CollectibleBehaviors.Append(new BlockBehaviorWorldEditFixGhostBlockPlace(block));
                }
            }*/
        }
    }

    internal class BlockBehaviorWorldEditFixGhostBlockPlace : BlockBehavior
    {
        public BlockBehaviorWorldEditFixGhostBlockPlace(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            /*if (world.Side == EnumAppSide.Client)
            {
                var we = world.Api.ModLoader.GetModSystem<WorldEdit.WorldEdit>();
                var ws = we.clientHandler.ownWorkspace;
                if (ws != null && ws.ToolName == "chiselbrush")
                {
                    handling = EnumHandling.PreventDefault;
                }

                (world.Api as ICoreClientAPI).Input.InWorldAction
            }
            */
            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
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
        Dictionary<BlockPos, ChiselBlockInEdit> blocksInEdit = new Dictionary<BlockPos, ChiselBlockInEdit>();

        public MicroblockTool(WorldEditWorkspace workspace, IBlockAccessorRevertable blockAccess) : base(workspace, blockAccess)
        {
        }

        public override void Load(ICoreAPI api)
        {
            api.ModLoader.GetModSystem<ModSystemDetailModeSync>().Toggle(workspace.PlayerUID, true);
        }

        public override void Unload(ICoreAPI api)
        {
            api.ModLoader.GetModSystem<ModSystemDetailModeSync>().Toggle(workspace.PlayerUID, false);
        }

        public override void HighlightBlocks(IPlayer player, WorldEdit.WorldEdit we, EnumHighlightBlocksMode mode)
        {
            we.sapi.World.HighlightBlocks(
                player, (int)EnumHighlightSlot.Brush, GetBlockHighlights(we), GetBlockHighlightColors(we), 
                workspace.ToolOffsetMode == EnumToolOffsetMode.Center ? EnumHighlightBlocksMode.CenteredToBlockSelectionIndex : EnumHighlightBlocksMode.AttachedToBlockSelectionIndex, 
                GetBlockHighlightShape(we), 
                1/16f
            );
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

        public override bool PerformBrushAction(WorldEdit.WorldEdit we, Block placedBlock, int oldBlockId, BlockSelection blockSel, BlockPos targetPos, ItemStack withItemStack)
        {
            if (BrushDim1 <= 0) return false;

            targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;

            Block hereBlock = ba.GetBlock(targetPos);
            BlockChisel selectedBlock = hereBlock as BlockChisel;
            if (selectedBlock == null)
            {
                selectedBlock = ba.GetBlock(new AssetLocation("chiseledblock")) as BlockChisel;
            }
            if (withItemStack?.Block == null || !ItemChisel.IsValidChiselingMaterial(we.sapi, targetPos, withItemStack.Block, we.sapi.World.PlayerByUid(workspace.PlayerUID)))
            {
                (we.sapi.World.PlayerByUid(workspace.PlayerUID) as IServerPlayer).SendIngameError("notmicroblock", Lang.Get("Must have a chisel material in hands"));
                return false;
            }

            var targetBe = ba.GetBlockEntity(targetPos) as BlockEntityChisel;

            Vec3i voxelpos = new Vec3i(
                Math.Min(15, (int)(blockSel.HitPosition.X * 16)),
                Math.Min(15, (int)(blockSel.HitPosition.Y * 16)),
                Math.Min(15, (int)(blockSel.HitPosition.Z * 16))
            );


            if (oldBlockId >= 0)
            {
                if (placedBlock.ForFluidsLayer)
                {
                    we.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position, BlockLayersAccess.Fluid);
                }
                else
                {
                    we.sapi.World.BlockAccessor.SetBlock(oldBlockId, blockSel.Position);
                }
            }

            EnumBrushMode brushMode = BrushMode;

            int blockId = withItemStack.Block.BlockId;

            if (!we.MayPlace(ba.GetBlock(blockId), brushPositions.Length)) return false;

            BlockPos tmpPos = new BlockPos();
            Vec3i dvoxelpos = new Vec3i();

            blocksInEdit.Clear();

            int selectedMatId = targetBe?.GetVoxelMaterialAt(voxelpos) ?? hereBlock.Id;


            long voxelWorldX, voxelWorldY, voxelWorldZ;

            for (int i = 0; i < brushPositions.Length; i++)
            {
                var brushPos = brushPositions[i];
                
                voxelWorldX = targetPos.X * 16 + brushPos.X + voxelpos.X;
                voxelWorldY = targetPos.Y * 16 + brushPos.Y + voxelpos.Y;
                voxelWorldZ = targetPos.Z * 16 + brushPos.Z + voxelpos.Z;

                BlockPos dpos = tmpPos.Set((int)(voxelWorldX / 16), (int)(voxelWorldY / 16), (int)(voxelWorldZ / 16));
                dvoxelpos.Set((int)GameMath.Mod(voxelWorldX, 16), (int)GameMath.Mod(voxelWorldY, 16), (int)GameMath.Mod(voxelWorldZ, 16));

                ChiselBlockInEdit editData;
                if (!blocksInEdit.TryGetValue(dpos, out editData))
                {
                    bool isNew = false;

                    var hereblock = ba.GetBlock(dpos);
                    var be = ba.GetBlockEntity(dpos) as BlockEntityChisel;
                    if (be == null)
                    {
                        if ((brushMode == EnumBrushMode.ReplaceAir && hereblock.Id == 0) || brushMode == EnumBrushMode.Fill)
                        {
                            ba.SetBlock(selectedBlock.Id, dpos);
                            string blockName = withItemStack.GetName();
                            be = new BlockEntityChisel();
                            be.Pos = dpos.Copy();
                            be.CreateBehaviors(selectedBlock, we.sapi.World);
                            be.Initialize(we.sapi);
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
                    } else
                    {
                        ba.SetHistoryStateBlock(dpos.X, dpos.Y, dpos.Z, 0, selectedBlock.Id);
                    }

                    be.BeginEdit(out var voxels, out var voxMats);
                    blocksInEdit[dpos] = editData = new ChiselBlockInEdit() { 
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
                    var matId = editData.be.BlockIds.IndexOf(blockId);
                    if (matId < 0) matId = editData.be.AddMaterial(withItemStack.Block);

                    editData.voxels[dvoxelpos.X, dvoxelpos.Y, dvoxelpos.Z] = true;
                    editData.voxelMaterial[dvoxelpos.X, dvoxelpos.Y, dvoxelpos.Z] = (byte)matId;
                }
            }

            foreach (var val in blocksInEdit)
            {
                val.Value.be.EndEdit(val.Value.voxels, val.Value.voxelMaterial);
            }

            return true;
        }
    }

    public class ChiselBlockBulkSetMaterial : ModSystem
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            sapi.ChatCommands.GetOrCreate("we")
                .BeginSubCommand("microblock")
                    .WithDescription("Recalculate microblocks")
                    .RequiresPrivilege("worldedit")
                    .BeginSubCommand("recalc")
                        .WithDescription("Recalc")
                        .RequiresPlayer()
                        .HandleWith(OnMicroblockCmd)
                    .EndSubCommand()
                .EndSubCommand();
        } 

        private TextCommandResult OnMicroblockCmd(TextCommandCallingArgs args)
        {
            var wmod = sapi.ModLoader.GetModSystem<WorldEdit.WorldEdit>();
            var workspace = wmod.GetWorkSpace(args.Caller.Player.PlayerUID);

            if (workspace == null || workspace.StartMarker == null || workspace.EndMarker == null)
            {
                return TextCommandResult.Success("Select an area with worldedit first");
            }
            
            int i = 0;
            sapi.World.BlockAccessor.WalkBlocks(workspace.StartMarker, workspace.EndMarker, (block, x, y, z) =>
            {
                if (block is BlockMicroBlock)
                {
                    BlockEntityMicroBlock bemc = sapi.World.BlockAccessor.GetBlockEntity(new BlockPos(x, y, z)) as BlockEntityMicroBlock;
                    if (bemc != null)
                    {
                        bemc.RebuildCuboidList();
                        bemc.MarkDirty(true);
                        i++;
                    }
                }
            });

            return TextCommandResult.Success(i + " microblocks recalced");
        }
    }
}
