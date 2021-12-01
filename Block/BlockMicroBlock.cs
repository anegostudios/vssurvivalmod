using System.Collections.Generic;
using System.Text;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using VintagestoryAPI.Math.Vector;

namespace Vintagestory.GameContent
{
    public class MicroBlockSounds : BlockSounds
    {
        //public override AssetLocation Ambient { get => base.Ambient; set => base.Ambient = value; }
        public override AssetLocation Break { get => block.Sounds.Break; set { } }
        public override AssetLocation Hit { get => block.Sounds.Hit; set { } }
        public override AssetLocation Inside { get => block.Sounds.Inside; set { } }
        public override AssetLocation Place { get => block.Sounds.Place; set { } }
        public override AssetLocation Walk { get => block.Sounds.Walk ; set { } }
        public override Dictionary<EnumTool, BlockSounds> ByTool { get => block.Sounds.ByTool; set { } }


        public BlockEntityMicroBlock be;
        public Block defaultBlock;

        public MicroBlockSounds() { }

        public void Init(BlockEntityMicroBlock be, Block defaultBlock)
        {
            this.be = be;
            this.defaultBlock = defaultBlock;
            Ambient = defaultBlock.Sounds.Ambient;
        }

        Block block
        {
            get
            {
                if (be?.MaterialIds != null && be.MaterialIds.Length > 0)
                {
                    Block block = be.Api.World.GetBlock(be.MaterialIds[0]);
                    return block;
                }

                return defaultBlock;
            }
        }
    }

    public class BlockMicroBlock : Block
    {
        public int snowLayerBlockId;

        bool IsSnowCovered;

        public ThreadLocal<MicroBlockSounds> MBSounds = new ThreadLocal<MicroBlockSounds>(() => new MicroBlockSounds());

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            notSnowCovered = api.World.GetBlock(AssetLocation.Create(FirstCodePart(), Code.Domain));
            snowCovered1 = api.World.GetBlock(AssetLocation.Create(FirstCodePart() + "-snow", Code.Domain));
            snowCovered2 = api.World.GetBlock(AssetLocation.Create(FirstCodePart() + "-snow2", Code.Domain));
            snowCovered3 = api.World.GetBlock(AssetLocation.Create(FirstCodePart() + "-snow3", Code.Domain));
            if (this == snowCovered1) snowLevel = 1;
            if (this == snowCovered2) snowLevel = 2;
            if (this == snowCovered3) snowLevel = 3;


            snowLayerBlockId = api.World.GetBlock(new AssetLocation("snowlayer-1")).Id;

            IsSnowCovered = this.Id != notSnowCovered.Id;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            MBSounds.Dispose();
        }

        public override BlockSounds GetSounds(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            BlockEntityMicroBlock bec = blockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;

            return bec?.GetSounds() ?? base.GetSounds(blockAccessor, pos, stack);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            // We cannot call the base method, otherwise we'll destroy the chiseled block
            //base.OnNeighbourBlockChange(world, pos, neibpos);

            if (pos.X == neibpos.X && pos.Z == neibpos.Z && pos.Y + 1 == neibpos.Y && world.BlockAccessor.GetBlock(neibpos).Id != 0)
            {
                BlockEntityMicroBlock bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
                bool markdirty = bec.SnowLevel != 0 || bec.SnowCuboids.Count > 0 || bec.GroundSnowCuboids.Count > 0;

                if (Id != notSnowCovered.Id)
                {
                    world.BlockAccessor.ExchangeBlock(notSnowCovered.Id, pos);
                    markdirty = true;
                }

                bec.SnowLevel = 0;
                bec.SnowCuboids.Clear();
                bec.GroundSnowCuboids.Clear();

                if (markdirty) bec.MarkDirty(true);
                
            }
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            // We cannot call the base method, otherwise we'll destroy the chiseled block
            // base.OnServerGameTick(world, pos, extra);

            if (extra is string && (string)extra == "melt")
            {
                BlockEntityMicroBlock bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;

                if (this == snowCovered3)
                {
                    world.BlockAccessor.ExchangeBlock(snowCovered2.Id, pos);
                    bec.SnowLevel = 0;
                    bec.MarkDirty(true);
                }
                else if (this == snowCovered2)
                {
                    world.BlockAccessor.ExchangeBlock(snowCovered1.Id, pos);
                    bec.SnowLevel = 0;
                    bec.MarkDirty(true);
                }
                else if (this == snowCovered1)
                {
                    world.BlockAccessor.ExchangeBlock(notSnowCovered.Id, pos);
                    bec.SnowLevel = 0;
                    bec.MarkDirty(true);
                }
            }
        }

        public override float GetSnowLevel(BlockPos pos)
        {
            return IsSnowCovered ? 0.5f : 0;
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            //return forPlayer?.InventoryManager.ActiveHotbarSlot?.Itemstack?.Item is ItemChisel;
            return true;
        }

        public override int GetHeatRetention(BlockPos pos, BlockFacing facing)
        {
            BlockEntityMicroBlock bemc = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;

            if (bemc?.MaterialIds != null && bemc.sideAlmostSolid[facing.Index] && bemc.MaterialIds.Length > 0 && bemc.VolumeRel >= 0.5f)
            {
                Block block = api.World.GetBlock(bemc.MaterialIds[0]);
                var mat = block.BlockMaterial;
                if (mat == EnumBlockMaterial.Ore || mat == EnumBlockMaterial.Stone || mat == EnumBlockMaterial.Soil || mat == EnumBlockMaterial.Ceramic)
                {
                    return -1;
                }
                return 1;
            }

            return base.GetHeatRetention(pos, facing);
        }

        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            int[] matids;
            if (pos != null)
            {
                BlockEntityMicroBlock bec = blockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
                return bec?.LightHsv ?? this.LightHsv;
            }
            else
            {
                matids = (stack.Attributes?["materials"] as IntArrayAttribute)?.value;
            }

            byte[] hsv = new byte[3];
            int q = 0;
            for (int i = 0; i < matids.Length; i++)
            {
                Block block = blockAccessor.GetBlock(matids[i]);
                if (block.LightHsv[2] > 0)
                {
                    hsv[0] += block.LightHsv[0];
                    hsv[1] += block.LightHsv[1];
                    hsv[2] += block.LightHsv[2];
                    q++;
                }
            }

            if (q == 0) return hsv;

            hsv[0] = (byte)(hsv[0] / q);
            hsv[1] = (byte)(hsv[1] / q);
            hsv[2] = (byte)(hsv[2] / q);


            return hsv;
        }

        

        public override bool MatchesForCrafting(ItemStack inputStack, GridRecipe gridRecipe, CraftingRecipeIngredient ingredient)
        {
            int len = (inputStack.Attributes["materials"] as StringArrayAttribute)?.value?.Length ?? 0;

            if (len > 2) return false;

            return base.MatchesForCrafting(inputStack, gridRecipe, ingredient);
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            List<int> matids = new List<int>();
            bool first = false;
            foreach (var val in allInputslots)
            {
                if (val.Empty) continue;
                if (!first)
                {
                    first = true;
                    outputSlot.Itemstack.Attributes = val.Itemstack.Attributes.Clone();
                }

                int[] mats = (val.Itemstack.Attributes?["materials"] as IntArrayAttribute)?.value;
                if (mats != null) matids.AddRange(mats);

                string[] smats = (val.Itemstack.Attributes?["materials"] as StringArrayAttribute)?.value;
                if (smats != null)
                {
                    foreach (var code in smats)
                    {
                        Block block = api.World.GetBlock(new AssetLocation(code));
                        if (block != null) matids.Add(block.Id);
                    }
                }
            }

            IntArrayAttribute attr = new IntArrayAttribute(matids.ToArray());
            outputSlot.Itemstack.Attributes["materials"] = attr;

            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        }

        public override int GetLightAbsorption(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetLightAbsorption(blockAccessor.GetChunkAtBlockPos(pos), pos);
        }

        public override int GetLightAbsorption(IWorldChunk chunk, BlockPos pos)
        {
            BlockEntityMicroBlock bec = chunk?.GetLocalBlockEntityAtBlockPos(pos) as BlockEntityMicroBlock;
            return bec?.GetLightAbsorption() ?? 0;
        }

        public override EnumBlockMaterial GetBlockMaterial(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            if (pos != null)
            {
                BlockEntityMicroBlock be = blockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
                if (be?.MaterialIds != null && be.MaterialIds.Length > 0)
                {
                    Block block = api.World.GetBlock(be.MaterialIds[0]);
                    return block.BlockMaterial;
                }
            } else
            {
                int[] mats = (stack.Attributes?["materials"] as IntArrayAttribute)?.value;
                
                if (mats != null && mats.Length > 0)
                {
                    Block block = api.World.GetBlock(mats[0]);
                    return block.BlockMaterial;
                }
            }

            return base.GetBlockMaterial(blockAccessor, pos, stack);
        }

        public override bool DoEmitSideAo(IGeometryTester caller, BlockFacing facing)
        {
            BlockEntityMicroBlock bec = caller.GetCurrentBlockEntityOnSide(facing.Opposite) as BlockEntityMicroBlock;
            return bec?.DoEmitSideAo(facing.Index) ?? base.DoEmitSideAo(caller, facing);
        }

        public override bool DoEmitSideAoByFlag(IGeometryTester caller, Vec3iAndFacingFlags vec)
        {
            BlockEntityMicroBlock bec = caller.GetCurrentBlockEntityOnSide(vec) as BlockEntityMicroBlock;
            return bec?.DoEmitSideAoByFlag(vec.OppositeFlags) ?? base.DoEmitSideAoByFlag(caller, vec);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityMicroBlock bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
            if (bec == null)
            {
                return null;
            }

            TreeAttribute tree = new TreeAttribute();
            bec.ToTreeAttributes(tree);
            tree.RemoveAttribute("posx");
            tree.RemoveAttribute("posy");
            tree.RemoveAttribute("posz");
            tree.RemoveAttribute("snowcuboids");
            
            return new ItemStack(notSnowCovered.Id, EnumItemClass.Block, 1, tree, world);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (Attributes?.IsTrue("dropSelf") == true)
            {
                return new ItemStack[] { OnPickBlock(world, pos) };
            }

            return new ItemStack[0];
        }

        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            BlockEntityMicroBlock be = world.GetBlockEntity(pos) as BlockEntityMicroBlock;
            if (be != null)
            {
                return be.CanAttachBlockAt(blockFace, attachmentArea);
            }

            return base.CanAttachBlockAt(world, block, pos, blockFace);
        }

        public override bool AllowSnowCoverage(IWorldAccessor world, BlockPos blockPos)
        {
            BlockEntityMicroBlock be = world.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityMicroBlock;
            if (be != null)
            {
                return be.sideAlmostSolid[BlockFacing.UP.Index];
            }

            return base.AllowSnowCoverage(world, blockPos);
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);

            BlockEntityMicroBlock be = world.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityMicroBlock;
            if (be != null && byItemStack != null)
            {
                ITreeAttribute tree = byItemStack.Attributes.Clone();
                tree.SetInt("posx", blockPos.X);
                tree.SetInt("posy", blockPos.Y);
                tree.SetInt("posz", blockPos.Z);

                be.FromTreeAttributes(tree, world);
                be.MarkDirty(true);

                if (world.Side == EnumAppSide.Client)
                {
                    be.RegenMesh();
                }

                be.RegenSelectionBoxes(null);
            }
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override float GetResistance(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityMicroBlock be = blockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
            if (be?.MaterialIds != null && be.MaterialIds.Length > 0)
            {
                Block block = api.World.GetBlock(be.MaterialIds[0]);
                return block.Resistance;
            }

            return base.GetResistance(blockAccessor, pos);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            ChiselBlockModelCache cache = capi.ModLoader.GetModSystem<ChiselBlockModelCache>();
            renderinfo.ModelRef = cache.GetOrCreateMeshRef(itemstack);
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            BlockEntityMicroBlock be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
            if (be != null)
            {
                blockModelData = be.Mesh;
                decalModelData = be.CreateDecalMesh(decalTexSource);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }


        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityMicroBlock bec = blockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;

            if (bec != null)
            {
                Cuboidf[] selectionBoxes = bec.GetSelectionBoxes(blockAccessor, pos);

                return selectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityMicroBlock bec = blockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;

            if (bec != null)
            {
                Cuboidf[] selectionBoxes = bec.GetCollisionBoxes(blockAccessor, pos);

                return selectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            BlockEntityMicroBlock be = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
            if (be?.MaterialIds != null && be.MaterialIds.Length > 0)
            {
                Block block = capi.World.GetBlock(be.MaterialIds[0]);
                if (block is BlockMicroBlock) return 0; // Prevent-chisel-ception. Happened to WQP, not sure why

                return block.GetRandomColor(capi, pos, facing, rndIndex);
            }

            return base.GetRandomColor(capi, pos, facing, rndIndex);
        }


        public override bool Equals(ItemStack thisStack, ItemStack otherStack, params string[] ignoreAttributeSubTrees)
        {
            List<string> ign = ignoreAttributeSubTrees == null ? new List<string>() : new List<string>(ignoreAttributeSubTrees);
            ign.Add("meshId");
            return base.Equals(thisStack, otherStack, ign.ToArray());
        }

        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
        {
            BlockEntityMicroBlock be = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
            if (be?.MaterialIds != null && be.MaterialIds.Length > 0)
            {
                Block block = capi.World.GetBlock(be.MaterialIds[0]);
                if (block is BlockMicroBlock) return 0; // Prevent-chisel-ception. Happened to WQP, not sure why

                return block.GetColor(capi, pos);
            }

            return base.GetColorWithoutTint(capi, pos);
        }


        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (itemStack.Attributes.HasAttribute("blockName"))
            {
                return itemStack.Attributes.GetString("blockName", "").Split('\n')[0];
            }

            return base.GetHeldItemName(itemStack);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack.Attributes.HasAttribute("blockName"))
            {
                string bname = inSlot.Itemstack.Attributes.GetString("blockName", "");
                dsc.AppendLine(bname.Substring(bname .IndexOf('\n') + 1));
            }
            

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityMicroBlock be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMicroBlock;
            if (be != null) return be.BlockName.Split('\n')[0];

            return base.GetPlacedBlockName(world, pos);
        }


        public override Block GetSnowCoveredVariant(BlockPos pos, float snowLevel)
        {
            return base.GetSnowCoveredVariant(pos, snowLevel);
        }


        public override void PerformSnowLevelUpdate(IBulkBlockAccessor ba, BlockPos pos, Block newBlock, float snowLevel)
        {
            if (newBlock.Id != Id && (BlockMaterial == EnumBlockMaterial.Snow || BlockId == 0 || this.FirstCodePart() == newBlock.FirstCodePart()))
            {
                ba.ExchangeBlock(newBlock.Id, pos);
            }
        }



    }
}
