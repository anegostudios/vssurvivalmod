using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public abstract class BlockMPBase : BlockGeneric, IMechanicalPowerBlock
    {
        public float RotationalInertia { get; private set; } = 1f;
        public EnumMechanicalPowerConnectorFlags ConnectorFlags { get; private set; } = EnumMechanicalPowerConnectorFlags.All;
        public bool UsesMaterialAttributes => materialProperties != null;
        public MechanicalMaterialProperties MaterialProperties => materialProperties;

        public IEnumerable<string> MaterialTextureCodes => materialProperties?.MaterialTextureCodes ?? [];

        MechanicalMaterialProperties materialProperties;
        string materialMeshCacheKey;

        public override void OnLoaded(ICoreAPI api)
        {
            RotationalInertia = Attributes?["mechanicalPower"]?["rotationalInertia"].AsFloat(RotationalInertia) ?? RotationalInertia;
            string connectorFlags = Attributes?["mechanicalPower"]?["connectorFlags"].AsString();
            if (connectorFlags != null)
            {
                if (Enum.TryParse(connectorFlags, out EnumMechanicalPowerConnectorFlags flags)) ConnectorFlags = flags;
                else api.Logger.Warning("{0} has unknown connectorFlags '{1}', it will accept every connector", Code, connectorFlags);
            }
            materialProperties = MechanicalMaterialProperties.Load(api, this, Textures);
            if (UsesMaterialAttributes) materialMeshCacheKey = $"{Code}-materialMeshRefs";
            base.OnLoaded(api);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            Dictionary<string, MultiTextureMeshRef> meshRefs = materialMeshCacheKey == null ? null : ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, materialMeshCacheKey);
            if (meshRefs != null)
            {
                foreach (MultiTextureMeshRef meshRef in meshRefs.Values) meshRef.Dispose();
            }

            base.OnUnloaded(api);
        }

        public bool HasCompatibleConnector(BlockMPBase other)
        {
            return other == null || (ConnectorFlags & other.ConnectorFlags) != 0;
        }

        public abstract void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face);
        public abstract bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock);



        public virtual void WasPlaced(IWorldAccessor world, BlockPos ownPos, BlockFacing connectedOnFacing)
        {
            if (connectedOnFacing == null)
            {
                foreach (var face in BlockFacing.HORIZONTALS)
                {
                    var nbpos = ownPos.AddCopy(face);
                    if (HasMechPowerConnectorAt(world, nbpos, face.Opposite, this))
                    {
                        BEBehaviorMPBase beMechBasen = world.BlockAccessor.GetBlockEntity(ownPos)?.GetBehavior<BEBehaviorMPBase>();
                        if (beMechBasen?.tryConnect(face.Opposite) == true)
                        {
                            return;
                        }
                    }
                }
                return;
            }

            BEBehaviorMPBase beMechBase = world.BlockAccessor.GetBlockEntity(ownPos)?.GetBehavior<BEBehaviorMPBase>();
            beMechBase?.tryConnect(connectedOnFacing);
        }


        public virtual bool tryConnect(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, BlockFacing face)
        {
            IBlockAccessor blockAccessor = world.BlockAccessor;
            BlockMPBase ownBlock = blockAccessor.GetBlock(pos) as BlockMPBase;
            if (ownBlock == null) return false;

            BlockPos neighbourPos = pos.AddCopy(face);
            IMechanicalPowerBlock neighbourBlock = blockAccessor.GetBlock(neighbourPos) as IMechanicalPowerBlock;
            if (neighbourBlock == null
                || !ownBlock.HasCompatibleConnector(neighbourBlock as BlockMPBase)
                || !ownBlock.HasMechPowerConnectorAt(world, pos, face, neighbourBlock as BlockMPBase)
                || !neighbourBlock.HasMechPowerConnectorAt(world, neighbourPos, face.Opposite, ownBlock))
            {
                return false;
            }

            neighbourBlock.DidConnectAt(world, neighbourPos, face.Opposite);
            ownBlock.WasPlaced(world, pos, face);
            return true;
        }


        public virtual MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            IMechanicalPowerDevice be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>() as IMechanicalPowerDevice;
            return be?.Network;
        }

        internal void ExchangeBlockAt(IWorldAccessor world, BlockPos pos)
        {
            world.BlockAccessor.ExchangeBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            BEBehaviorMPBase bemp = be?.GetBehavior<BEBehaviorMPBase>();

            if (bemp != null)
            {
                bemp.SetOrientations();
                bemp.Shape = Shape;
                be.MarkDirty();
            }
        }

        public string GetMaterialKey(ItemStack stack) => materialProperties?.GetMaterialKey(stack);

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            if (!UsesMaterialAttributes) return base.OnPickBlock(world, pos);

            return GetBEBehavior<BEBehaviorMPBase>(pos)?.GetPlacedStack() ?? base.OnPickBlock(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            ItemStack placedStack = UsesMaterialAttributes ? GetBEBehavior<BEBehaviorMPBase>(pos)?.GetPlacedStack() : null;
            if (placedStack == null) return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

            return [placedStack];
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            if (GetMaterialKey(handbookStack) == null) return base.GetDropsForHandbook(handbookStack, forPlayer);

            return [new BlockDropItemStack(handbookStack)];
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string materialKey = GetMaterialKey(itemStack);
            if (materialKey == null) return base.GetHeldItemName(itemStack);

            return Lang.GetMatchingIfExists($"{Code.Domain}:block-{Code.Path}-{materialKey}") ?? base.GetHeldItemName(itemStack);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            string materialKey = GetMaterialKey(itemstack);
            if (materialKey == null) return;

            Dictionary<string, MultiTextureMeshRef> meshRefs = ObjectCacheUtil.GetOrCreate(capi, materialMeshCacheKey, () => new Dictionary<string, MultiTextureMeshRef>());
            if (!meshRefs.TryGetValue(materialKey, out MultiTextureMeshRef meshRef))
            {
                MeshData mesh = capi.TesselatorManager.CreateMesh(Code + " material stack", ShapeInventory ?? Shape, (shape, name) => GetMaterialTextureSource(capi, shape, name, itemstack));
                meshRefs[materialKey] = meshRef = capi.Render.UploadMultiTextureMesh(mesh);
            }

            renderinfo.ModelRef = meshRef;
        }

        // Textures for meshes made per material, such as inventory items and chunk tesselated attachments.
        public ITexPositionSource GetMaterialTextureSource(ICoreClientAPI capi, Shape shape, string sourceForLogging, ItemStack stack)
        {
            var textures = materialProperties?.GetMaterialTextures(stack);
            if (textures == null) return capi.Tesselator.GetTextureSource(this);

            return new ShapeTextureSource(capi, shape, sourceForLogging, textures, path => path);
        }

        // Keeps the faces drawn with one material texture, or with none of them when textureCode is null.
        public Shape GetMaterialRenderShape(Shape shape, string textureCode) => materialProperties?.GetMaterialRenderShape(shape, textureCode) ?? shape.Clone();

        public ITexPositionSource GetMaterialRenderTextureSource(ICoreClientAPI capi, string textureCode)
        {
            return materialProperties.GetMaterialRenderTextureSource(capi, capi.Tesselator.GetTextureSource(this), textureCode);
        }

        public void GetMaterialUvOffset(ICoreClientAPI capi, ItemStack stack, string textureCode, Vec2f offset)
        {
            offset.X = 0;
            offset.Y = 0;
            materialProperties?.GetMaterialUvOffset(capi, stack, textureCode, offset);
        }

        public void GetMaterialUvOffset(ICoreClientAPI capi, string textureCode, string materialValue, Vec2f offset)
        {
            offset.X = 0;
            offset.Y = 0;
            materialProperties?.GetMaterialUvOffset(capi, textureCode, materialValue, offset);
        }
    }
}
