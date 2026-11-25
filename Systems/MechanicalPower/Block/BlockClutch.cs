using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockClutch : Block
    {
        readonly Dictionary<string, MultiTextureMeshRef> materialMeshes = [];
        public MechanicalMaterialProperties MaterialProperties { get; private set; }

        public override void OnLoaded(ICoreAPI api)
        {
            MaterialProperties = MechanicalMaterialProperties.Load(api, this, Textures);
            base.OnLoaded(api);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            foreach (MultiTextureMeshRef mesh in materialMeshes.Values) mesh.Dispose();
            materialMeshes.Clear();
            base.OnUnloaded(api);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string materialKey = MaterialProperties?.GetMaterialKey(itemStack);
            return materialKey == null ? base.GetHeldItemName(itemStack) : Lang.GetMatchingIfExists($"{Code.Domain}:block-{Code.Path}-{materialKey}") ?? base.GetHeldItemName(itemStack);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetBlockEntity<BEClutch>(pos)?.PlacedStack?.Clone() ?? base.OnPickBlock(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            ItemStack stack = GetBlockEntity<BEClutch>(pos)?.PlacedStack;
            return stack == null ? base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier) : [stack.Clone()];
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return MaterialProperties?.GetMaterialKey(handbookStack) == null ? base.GetDropsForHandbook(handbookStack, forPlayer) : [new BlockDropItemStack(handbookStack)];
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
            string materialKey = MaterialProperties?.GetMaterialKey(itemstack);
            if (materialKey == null) return;

            if (!materialMeshes.TryGetValue(materialKey, out MultiTextureMeshRef mesh))
            {
                MeshData meshData = capi.TesselatorManager.CreateMesh(Code + " material stack", ShapeInventory ?? Shape, (shape, name) => GetMaterialTextureSource(capi, shape, name, itemstack));
                materialMeshes[materialKey] = mesh = capi.Render.UploadMultiTextureMesh(meshData);
            }
            renderinfo.ModelRef = mesh;
        }

        public ITexPositionSource GetMaterialTextureSource(ICoreClientAPI capi, Shape shape, string sourceForLogging, ItemStack stack)
        {
            var textures = MaterialProperties?.GetMaterialTextures(stack);
            return textures == null ? capi.Tesselator.GetTextureSource(this) : new ShapeTextureSource(capi, shape, sourceForLogging, textures, path => path);
        }

        static bool isClutchableBlock(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (blockAccessor.GetBlock(pos) is BlockTransmission) return true;
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAngledGears>()?.IsCollinearHub == true;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

            IBlockAccessor blockAccessor = world.BlockAccessor;
            BlockFacing frontFacing = Block.SuggestedHVOrientation(byPlayer, blockSel)[0];
            BlockFacing[] facings = [frontFacing, frontFacing.GetCW(), frontFacing.GetCCW(), frontFacing.Opposite];
            BlockFacing bestFacing = frontFacing;
            BlockPos neighbourPos = blockSel.Position.Copy();
            foreach (BlockFacing facing in facings)
            {
                neighbourPos.Set(blockSel.Position).Add(facing);
                if (!isClutchableBlock(blockAccessor, neighbourPos)) continue;

                bestFacing = facing;
                break;
            }

            Block orientedBlock = blockAccessor.GetBlock(CodeWithVariant("side", bestFacing.Code));
            return orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEClutch be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEClutch;
            if (be != null) return be.OnInteract(byPlayer);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs)
        {
            BEClutch be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEClutch;
            if (be == null) return;
            if (activationArgs != null && activationArgs.HasAttribute("engaged"))
            {
                if (activationArgs.GetBool("engaged") == be.Engaged) return;   // Do nothing if already in the required state
            }
            be.OnInteract(caller.Player);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BEClutch be = world.BlockAccessor.GetBlockEntity(pos) as BEClutch;
            be?.onNeighbourChange(neibpos);
        }

    }
}
