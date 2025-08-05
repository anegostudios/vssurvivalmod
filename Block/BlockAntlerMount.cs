using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockAntlerMount : BlockShapeMaterialFromAttributes
    {
        public override string MeshKey => "AntlerMount";
        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var bect = blockAccessor.GetBlockEntity(pos) as BlockEntityAntlerMount;
            float degY = bect == null ? 0 : bect.MeshAngleRad * GameMath.RAD2DEG;
            return new Cuboidf[] { SelectionBoxes[0].RotatedCopy(0, degY, 0, new Vec3d(0.5, 0.5, 0.5)) };
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // Prefer selected block face
            if (blockSel.Face.IsHorizontal)
            {
                if (TryAttachTo(world, byPlayer, blockSel, itemstack, ref failureCode)) return true;
                if (failureCode == "entityintersecting") return false;
            }

            // Otherwise attach to any possible face
            BlockFacing[] faces = BlockFacing.HORIZONTALS;
            blockSel = blockSel.Clone();
            for (int i = 0; i < faces.Length; i++)
            {
                blockSel.Face = faces[i];
                if (TryAttachTo(world, byPlayer, blockSel, itemstack, ref failureCode)) return true;
            }

            failureCode = "requirehorizontalattachable";

            return false;
        }


        bool TryAttachTo(IWorldAccessor world, IPlayer player, BlockSelection blockSel, ItemStack itemstack, ref string failureCode)
        {
            BlockFacing oppositeFace = blockSel.Face.Opposite;

            BlockPos attachingBlockPos = blockSel.Position.AddCopy(oppositeFace);
            Block attachingBlock = world.BlockAccessor.GetBlock(attachingBlockPos);

            if (attachingBlock.CanAttachBlockAt(world.BlockAccessor, this, attachingBlockPos, blockSel.Face, null) && CanPlaceBlock(world, player, blockSel, ref failureCode))
            {
                DoPlaceBlock(world, player, blockSel, itemstack);
                return true;
            }

            return false;
        }

        bool CanBlockStay(IWorldAccessor world, BlockPos pos)
        {
            var bect = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAntlerMount;
            var facing = BlockFacing.HorizontalFromAngle((bect?.MeshAngleRad ?? 0) + GameMath.PIHALF);
            Block attachingblock = world.BlockAccessor.GetBlock(pos.AddCopy(facing));

            return attachingblock.CanAttachBlockAt(world.BlockAccessor, this, pos.AddCopy(facing), facing.Opposite);
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi? attachmentArea = null)
        {
            return false;
        }


        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            // bool result = true;
            // bool preventDefault = false;
            //
            // foreach (BlockBehavior behavior in BlockBehaviors)
            // {
            //     EnumHandling handled = EnumHandling.PassThrough;
            //
            //     bool behaviorResult = behavior.DoPlaceBlock(world, byPlayer, blockSel, byItemStack, ref handled);
            //
            //     if (handled != EnumHandling.PassThrough)
            //     {
            //         result &= behaviorResult;
            //         preventDefault = true;
            //     }
            //
            //     if (handled == EnumHandling.PreventSubsequent) break;
            // }
            //
            // if (preventDefault) return result;
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            // world.BlockAccessor.SetBlock(BlockId, blockSel.Position, byItemStack);
            if (val)
            {
                var bect = world.BlockAccessor.GetBlockEntity(blockSel.Position).GetBehavior<BEBehaviorShapeMaterialFromAttributes>();
                if (bect != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int faceIndex = (blockSel.Face.HorizontalAngleIndex + i) % 4;

                        BlockPos attachingBlockPos = blockSel.Position.AddCopy(BlockFacing.HORIZONTALS_ANGLEORDER[faceIndex]);
                        Block attachingBlock = world.BlockAccessor.GetBlock(attachingBlockPos);

                        if (attachingBlock.CanAttachBlockAt(world.BlockAccessor, this, attachingBlockPos, blockSel.Face, null))
                        {
                            bect.MeshAngleY = faceIndex * 90 * GameMath.DEG2RAD - GameMath.PIHALF;
                            bect.OnBlockPlaced(byItemStack); // call again to regen mesh
                        }
                    }
                }
            }

            return val;
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var beb = GetBlockEntity<BlockEntityAntlerMount>(pos);
            if (beb?.Type != null && beb.Material != null)
            {
                var mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(beb.MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
                blockModelData = GetOrCreateMesh(beb.Type, beb.Material).Clone().MatrixTransform(mat);
                decalModelData = GetOrCreateMesh(beb.Type, beb.Material, null, decalTexSource).Clone().MatrixTransform(mat);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (!CanBlockStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }




        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var beshelf = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityAntlerMount;
            if (beshelf != null) return beshelf.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            var bemount = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAntlerMount;
            if (bemount == null) return base.GetPlacedBlockName(world, pos);

            return Lang.Get("block-antlermount-" + bemount.Type);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            var bemount = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAntlerMount;
            if (bemount == null) return base.GetPlacedBlockInfo(world, pos, forPlayer);

            return base.GetPlacedBlockInfo(world, pos, forPlayer) + "\n" + Lang.Get("Material: {0}", Lang.Get("material-" + bemount.Material));
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string type = itemStack.Attributes.GetString("type", "square");
            return Lang.Get("block-" + Code.Path + "-" + type);
        }
    }
}
