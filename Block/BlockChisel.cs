using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class BlockChisel : Block
    {
        ICoreAPI api;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.api = api;
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        /*public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            BlockEntityChisel bec = blockAccessor.GetBlockEntity(pos) as BlockEntityChisel;
            if (bec?.Materials == null)
            {
                return base.GetLightHsv(blockAccessor, pos, stack);
            }

            for (int i = 0; i < bec.Materials.Length; i++)
            {
                Block block = blockAccessor.GetBlock(bec.Materials[i]);
                if (block.LightHsv[2] > 0) return block.LightHsv;
            }

            return base.GetLightHsv(blockAccessor, pos, stack);
        }

        public override int GetLightAbsorption(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityChisel bec = blockAccessor.GetBlockEntity(pos) as BlockEntityChisel;
            if (bec?.Materials == null)
            {
                return base.GetLightAbsorption(blockAccessor, pos);
            }

            int absorb = 99;

            for (int i = 0; i < bec.Materials.Length; i++)
            {
                Block block = blockAccessor.GetBlock(bec.Materials[i]);
                absorb = Math.Min(absorb, block.LightAbsorption);
            }

            return base.GetLightAbsorption(blockAccessor, pos);
        }*/


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityChisel bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityChisel;
            if (bec == null)
            {
                return null;
            }

            TreeAttribute tree = new TreeAttribute();
            bec.ToTreeAttributes(tree);
            return new ItemStack(this.Id, EnumItemClass.Block, 1, tree, world);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack stack = OnPickBlock(world, pos);

                if (stack != null)
                {
                    world.SpawnItemEntity(stack, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                }

                if (Sounds != null && Sounds.Break != null)
                {
                    world.PlaySoundAt(Sounds.Break, pos.X, pos.Y, pos.Z, byPlayer);
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }



        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);

            BlockEntityChisel be = world.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityChisel;
            if (be != null && byItemStack != null)
            {
                byItemStack.Attributes.SetInt("posx", blockPos.X);
                byItemStack.Attributes.SetInt("posy", blockPos.Y);
                byItemStack.Attributes.SetInt("posz", blockPos.Z);

                be.FromTreeAtributes(byItemStack.Attributes, world);
                be.MarkDirty(true);

                if (world.Side == EnumAppSide.Client)
                {
                    be.RegenMesh();
                }

                be.RegenSelectionBoxes(null);
            }
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            ChiselBlockModelCache cache = capi.ModLoader.GetModSystem<ChiselBlockModelCache>();
            renderinfo.ModelRef = cache.GetOrCreateMeshRef(itemstack);
        }


        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            BlockEntityChisel be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityChisel;
            if (be != null)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;

                blockModelData = be.Mesh;
                decalModelData = be.CreateDecalMesh(decalTexSource);

                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }


        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityChisel bec = blockAccessor.GetBlockEntity(pos) as BlockEntityChisel;

            if (bec != null)
            {
                Cuboidf[] selectionBoxes = bec.GetSelectionBoxes(blockAccessor, pos);

                return selectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityChisel bec = blockAccessor.GetBlockEntity(pos) as BlockEntityChisel;

            if (bec != null)
            {
                Cuboidf[] selectionBoxes = bec.GetCollisionBoxes(blockAccessor, pos);

                return selectionBoxes;
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override int TextureSubIdForRandomBlockPixel(IWorldAccessor world, BlockPos pos, BlockFacing facing, ref int tintIndex)
        {
            BlockEntityChisel be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityChisel;
            if (be?.Materials != null && be.Materials.Length > 0)
            {
                Block block = world.GetBlock(be.Materials[0]);
                return block.TextureSubIdForRandomBlockPixel(world, pos, facing, ref tintIndex);
            }

            return base.TextureSubIdForRandomBlockPixel(world, pos, facing, ref tintIndex);
        }
    }
}
