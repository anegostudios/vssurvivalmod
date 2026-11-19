using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockMechanicalBellows : BlockBellows
    {
        readonly Dictionary<string, MultiTextureMeshRef> materialMeshes = [];
        string? materialNameCode;
        public MechanicalMaterialProperties? MaterialProperties { get; private set; }

        public override void OnLoaded(ICoreAPI api)
        {
            MaterialProperties = MechanicalMaterialProperties.Load(api, this, Textures);
            materialNameCode = Attributes?["materialNameCode"].AsString();
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
            var material = MaterialProperties?.GetMaterialValues(itemStack);
            if (material == null) return base.GetHeldItemName(itemStack);

            object[] materialNames = material.ValuesOrdered.Select(value => (object)Lang.Get("material-" + value)).ToArray();
            return Lang.GetIfExists($"{Code.Domain}:{materialNameCode}", materialNames) ?? base.GetHeldItemName(itemStack);
        }

        public override ItemStack? OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetBlockEntity<BlockEntityMechPoweredBellows>(pos)?.PlacedStack?.Clone() ?? base.OnPickBlock(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            ItemStack? stack = GetBlockEntity<BlockEntityMechPoweredBellows>(pos)?.PlacedStack;
            return stack == null ? base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier) : [stack.Clone()];
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return MaterialProperties?.GetMaterialKey(handbookStack) == null ? base.GetDropsForHandbook(handbookStack, forPlayer) : [new BlockDropItemStack(handbookStack)];
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
            string? materialKey = MaterialProperties?.GetMaterialKey(itemstack);
            if (materialKey == null) return;

            if (!materialMeshes.TryGetValue(materialKey, out MultiTextureMeshRef? mesh))
            {
                MeshData meshData = capi.TesselatorManager.CreateMesh(Code + " material stack", ShapeInventory ?? Shape, (shape, name) => GetMaterialTextureSource(capi, shape, name, itemstack));
                materialMeshes[materialKey] = mesh = capi.Render.UploadMultiTextureMesh(meshData);
            }
            renderinfo.ModelRef = mesh;
        }

        public ITexPositionSource GetMaterialTextureSource(ICoreClientAPI capi, Shape shape, string sourceForLogging, ItemStack? stack)
        {
            var textures = MaterialProperties?.GetMaterialTextures(stack);
            return textures == null ? capi.Tesselator.GetTextureSource(this) : new ShapeTextureSource(capi, shape, sourceForLogging, textures, path => path);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockFacing facing = getAirReceiverFacing(world, byPlayer, blockSel);
            if (world.GetBlock(CodeWithVariant("side", facing.Code)) is not BlockMechanicalBellows bellowsBlock)
            {
                failureCode = "notreplaceable";
                return false;
            }

            string? bodyBlockCode = Attributes?["bodyBlockCode"].AsString();
            Block? resolvedBodyBlock = bodyBlockCode == null ? null : world.GetBlock(AssetLocation.Create(bodyBlockCode, Code.Domain));
            if (resolvedBodyBlock is not BlockMPMultiblockBellows bodyBlock)
            {
                failureCode = "notreplaceable";
                return false;
            }

            if (!bellowsBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

            BlockPos bodyPos = blockSel.Position.AddCopy(facing.Opposite);
            BlockSelection bodySelection = blockSel.Clone();
            bodySelection.Position = bodyPos;
            if (!bodyBlock.CanPlaceBlock(world, byPlayer, bodySelection, ref failureCode)) return false;
            if (!bellowsBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack)) return false;

            world.BlockAccessor.SetBlock(bodyBlock.BlockId, bodyPos);
            if (world.BlockAccessor.GetBlockEntity(bodyPos) is BEMPMultiblock bodyBlockEntity)
            {
                bodyBlockEntity.Principal = blockSel.Position.Copy();
                bodyBlockEntity.MarkDirty(true);
            }

            return true;
        }

        private BlockFacing getAirReceiverFacing(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockFacing? clickedFacing = blockSel.Face?.IsHorizontal == true ? blockSel.Face.Opposite : null;
            BlockFacing[] candidates = clickedFacing == null
                ? BlockFacing.HORIZONTALS
                : [clickedFacing, clickedFacing.GetCW(), clickedFacing.Opposite, clickedFacing.GetCCW()];
            BlockPos receiverPos = blockSel.Position.Copy();

            foreach (BlockFacing facing in candidates)
            {
                receiverPos.Set(blockSel.Position).Add(facing);
                Block receiverBlock = world.BlockAccessor.GetBlock(receiverPos);
                if (receiverBlock.GetInterface<IBellowsAirReceiver>(world, receiverPos) != null) return facing;
            }

            return SuggestedHVOrientation(byPlayer, blockSel)[0];
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);

            BlockFacing facing = BlockFacing.FromCode(Variant["side"]);
            BlockPos bodyPos = pos.AddCopy(facing.Opposite);
            if (world.BlockAccessor.GetBlock(bodyPos) is BlockMPMultiblockBellows
                && world.BlockAccessor.GetBlockEntity(bodyPos) is BEMPMultiblock bodyBlockEntity
                && pos.Equals(bodyBlockEntity.Principal))
            {
                bodyBlockEntity.Principal = null;
                world.BlockAccessor.SetBlock(0, bodyPos);
            }
        }
    }
}
