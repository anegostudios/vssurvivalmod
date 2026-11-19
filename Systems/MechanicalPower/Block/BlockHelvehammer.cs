using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockHelveHammer : Block
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
            return GetBlockEntity<BEHelveHammer>(pos)?.PlacedStack?.Clone() ?? base.OnPickBlock(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            ItemStack stack = GetBlockEntity<BEHelveHammer>(pos)?.PlacedStack;
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


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEHelveHammer beh = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEHelveHammer;

            if (beh != null && beh.HammerStack==null && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.FirstCodePart().Equals("helvehammer"))
                {
                    beh.HammerStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Clone();
                    beh.MarkDirty();
                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                    }
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    api.World.PlaySoundAt(new AssetLocation("sounds/player/build"), blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5, null, 0.88f + (float)api.World.Rand.NextDouble() * 0.24f, 16);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

    }
}
