using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class MSShapeFromAttrCacheHelper : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public static bool IsInCache(ICoreClientAPI capi, Block block, IShapeTypeProps cprops, string overrideTextureCode)
        {
            var blockTextures = (block as BlockShapeFromAttributes).blockTextures;


            // Prio 0: Shape textures
            var shape = cprops.ShapeResolved;
            if (shape == null) return false;
            if (shape.Textures != null)
            {
                foreach (var val in shape.Textures)
                {
                    if (capi.BlockTextureAtlas[val.Value] == null) return false;
                }
            }

            // Prio 1: Block wide custom textures
            if (blockTextures != null)
            {
                foreach (var val in blockTextures)
                {
                    if (val.Value.Baked == null) val.Value.Bake(capi.Assets);
                    if (capi.BlockTextureAtlas[val.Value.Baked.BakedName] == null) return false;
                }
            }

            // Prio 2: Variant textures
            if (cprops.Textures != null)
            {
                foreach (var val in cprops.Textures)
                {
                    var baked = val.Value.Baked ?? CompositeTexture.Bake(capi.Assets, val.Value);
                    if (capi.BlockTextureAtlas[baked.BakedName] == null) return false;
                }
            }

            // Prio 3: Override texture
            if (overrideTextureCode != null && cprops.TextureFlipCode != null)
            {
                if ((block as BlockShapeFromAttributes).OverrideTextureGroups[cprops.TextureFlipGroupCode].TryGetValue(overrideTextureCode, out var ctex))
                {
                    if (ctex.Baked == null) ctex.Bake(capi.Assets);
                    if (capi.BlockTextureAtlas[ctex.Baked.BakedName] == null) return false;
                }
            }

            return true;
        }
    }

    public class BEBehaviorShapeFromAttributes : BlockEntityBehavior, IRotatable, IExtraWrenchModes
    {
        public string Type;
        public BlockShapeFromAttributes clutterBlock;
        protected MeshData mesh;
        public float rotateX;
        public float rotateY { get; internal set; }
        public float rotateZ;
        public bool Collected;

        public string overrideTextureCode;
        /// <summary>
        /// Range from 0.0 for fully broken, to 1.0 (or above) for fully repaired
        /// </summary>
        public float repairState;

        /// <summary>
        /// The amount of glue needed for a full repair (abstract units corresponding to 1 resin, **PLUS ONE**), e.g. 5 resin is shown as 6.   0 means unspecified (we don't use the repair system), -1 means cannot be repaired will alway shatter
        /// </summary>
        public int reparability;

        /// <summary>
        /// Used for rotations, constant
        /// </summary>
        protected static Vec3f Origin = new Vec3f(0.5f, 0.5f, 0.5f);

        public float offsetX, offsetY, offsetZ;

        protected bool loadMeshDuringTesselation;

        public BEBehaviorShapeFromAttributes(BlockEntity blockentity) : base(blockentity)
        {
        }

        #region IExtraWrenchModes
        public SkillItem[] GetExtraWrenchModes(IPlayer byPlayer, BlockSelection blockSelection)
        {
            return clutterBlock?.extraWrenchModes;
        }

        public void OnWrenchInteract(IPlayer player, BlockSelection blockSel, int mode, int rightmouseBtn)
        {
            switch (mode)
            {
                case 0: offsetZ += (1 - rightmouseBtn * 2) / 16f; break; // N/S
                case 1: offsetX += (1 - rightmouseBtn * 2) / 16f; break; // W/E
                case 2: offsetY += (1 - rightmouseBtn * 2) / 16f; break; // U/D
            }

            loadMesh();
            Blockentity.MarkDirty(true);
        }

        #endregion


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            clutterBlock = Block as BlockShapeFromAttributes;

            if (Type != null)
            {
                MaybeInitialiseMesh_OnMainThread();

                var brep = clutterBlock.GetBehavior<BlockBehaviorReparable>();
                brep?.Initialize(Type, this);
            }
        }

        public virtual void loadMesh()
        {
            if (Type == null || Api == null || Api.Side == EnumAppSide.Server) return;

            var cprops = clutterBlock?.GetTypeProps(Type, null, this);

            if (cprops != null)
            {
                bool noOffset = offsetX == 0 && offsetY == 0 && offsetZ == 0;
                float angleY = rotateY + cprops.Rotation.Y * GameMath.DEG2RAD;

                MeshData baseMesh = clutterBlock.GetOrCreateMesh(cprops, null, overrideTextureCode);
                if (cprops.RandomizeYSize && clutterBlock?.AllowRandomizeDims != false)
                {
                    mesh = baseMesh.Clone().Rotate(Origin, rotateX, angleY, rotateZ).Scale(Vec3f.Zero, 1, 0.98f + GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 1000) / 1000f * 0.04f, 1);
                } else
                {

                    if (rotateX == 0 && angleY == 0 && rotateZ == 0 && noOffset) mesh = baseMesh;
                    else mesh = baseMesh.Clone().Rotate(Origin, rotateX, angleY, rotateZ);
                }

                if (!noOffset) mesh.Translate(offsetX, offsetY, offsetZ);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (byItemStack != null) // When generated by worldgen then byItemStack is null
            {
                Type = byItemStack.Attributes.GetString("type");
                Collected = byItemStack.Attributes.GetBool("collected");
            }

            loadMesh();
            var brep = clutterBlock.GetBehavior<BlockBehaviorReparable>();
            brep?.Initialize(Type, this);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            var cprops = clutterBlock?.GetTypeProps(Type, null, this);
            if (cprops?.LightHsv != null)
            {
                Api.World.BlockAccessor.RemoveBlockLight(cprops.LightHsv, Pos);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            string prevType = Type;
            string prevOverrideTextureCode = overrideTextureCode;
            float prevRotateX = rotateX;
            float prevRotateY = rotateY;
            float prevRotateZ = rotateZ;

            float prevOffsetX = offsetX;
            float prevOffsetY = offsetY;
            float prevOffsetZ = offsetZ;

            Type = tree.GetString("type");
            if (Type != null) {
                Type = BlockClutter.Remap(worldAccessForResolve, Type);
            }

            rotateX = tree.GetFloat("rotateX");
            rotateY = tree.GetFloat("meshAngle");
            rotateZ = tree.GetFloat("rotateZ");
            overrideTextureCode = tree.GetString("overrideTextureCode");
            Collected = tree.GetBool("collected");
            repairState = tree.GetFloat("repairState");

            offsetX = tree.GetFloat("offsetX");
            offsetY = tree.GetFloat("offsetY");
            offsetZ = tree.GetFloat("offsetZ");

            if (worldAccessForResolve.Side == EnumAppSide.Client && Api != null && (mesh == null || prevType != Type || prevOverrideTextureCode != overrideTextureCode || rotateX != prevRotateX || rotateY != prevRotateY || rotateZ != prevRotateZ || offsetX != prevOffsetX || offsetY != prevOffsetY || offsetZ != prevOffsetZ))
            {
                MaybeInitialiseMesh_OnMainThread();
                relight(prevType);
                Blockentity.MarkDirty(true);
            }
        }

        protected void relight(string oldType)
        {
            var cprops = clutterBlock?.GetTypeProps(oldType, null, this);
            if (cprops?.LightHsv != null)
            {
                Api.World.BlockAccessor.RemoveBlockLight(cprops.LightHsv, Pos);
            }

            cprops = clutterBlock?.GetTypeProps(Type, null, this);
            if (cprops?.LightHsv != null)
            {
                // This should force a relight at this position
                Api.World.BlockAccessor.ExchangeBlock(Block.Id, Pos);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetString("type", Type);
            tree.SetFloat("rotateX", rotateX);
            tree.SetFloat("meshAngle", rotateY);
            tree.SetFloat("rotateZ", rotateZ);
            tree.SetBool("collected", Collected);
            tree.SetFloat("repairState", repairState);

            tree.SetFloat("offsetX", offsetX);
            tree.SetFloat("offsetY", offsetY);
            tree.SetFloat("offsetZ", offsetZ);

            if (overrideTextureCode!=null) tree.SetString("overrideTextureCode", overrideTextureCode);
            base.ToTreeAttributes(tree);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            MaybeInitialiseMesh_OffThread();

            mesher.AddMeshData(mesh);
            return true;
        }

        protected void MaybeInitialiseMesh_OnMainThread()
        {
            if (Api.Side == EnumAppSide.Server) return;
            if (RequiresTextureUploads())
            {
                loadMesh();
            }
            else
            {
                loadMeshDuringTesselation = true;
            }
        }

        protected void MaybeInitialiseMesh_OffThread()
        {
            if (loadMeshDuringTesselation)
            {
                loadMeshDuringTesselation = false;
                loadMesh();
            }
        }

        private bool RequiresTextureUploads()
        {
            var cprops = clutterBlock?.GetTypeProps(Type, null, this);

            if (cprops == null) { 
                // Ignore invalid clutter blocks
                return false; 
            }

            if (cprops?.Textures == null)
            {
                if (overrideTextureCode == null) return false;
            }

            bool isInCache = MSShapeFromAttrCacheHelper.IsInCache(this.Api as ICoreClientAPI, Block, cprops, overrideTextureCode);
            return !isInCache;
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            float thetaX = tree.GetFloat("rotateX");
            float thetaY = tree.GetFloat("meshAngle");
            float thetaZ = tree.GetFloat("rotateZ");
            var cprops = clutterBlock?.GetTypeProps(Type, null, this);
            if (cprops != null) thetaY += cprops.Rotation.Y * GameMath.DEG2RAD;

            float[] m = Mat4f.Create();
            Mat4f.RotateY(m, m, -degreeRotation * GameMath.DEG2RAD);   // apply the new rotation
            Mat4f.RotateX(m, m, thetaX);
            Mat4f.RotateY(m, m, thetaY);
            Mat4f.RotateZ(m, m, thetaZ);

            Mat4f.ExtractEulerAngles(m, ref thetaX, ref thetaY, ref thetaZ);  // extract the new angles
            if (cprops != null) thetaY -= cprops.Rotation.Y * GameMath.DEG2RAD;
            tree.SetFloat("rotateX", thetaX);
            tree.SetFloat("meshAngle", thetaY);
            tree.SetFloat("rotateZ", thetaZ);
            rotateX = thetaX;
            rotateY = thetaY;
            rotateZ = thetaZ;

            var tmpOffsetX = tree.GetFloat("offsetX");
            offsetY = tree.GetFloat("offsetY");
            var tmpOffsetZ = tree.GetFloat("offsetZ");

            switch (degreeRotation)
            {
                case 90:
                {
                    offsetX = -tmpOffsetZ;
                    offsetZ = tmpOffsetX;
                    break;
                }
                case 180:
                {
                    offsetX = -tmpOffsetX;
                    offsetZ = -tmpOffsetZ;
                    break;
                }
                case 270:
                {
                    offsetX = tmpOffsetZ;
                    offsetZ = -tmpOffsetX;
                    break;
                }
            }

            tree.SetFloat("offsetX", offsetX);
            tree.SetFloat("offsetY", offsetY);
            tree.SetFloat("offsetZ", offsetZ);
        }

        public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            if (byEntity.Controls.ShiftKey)
            {
                if (blockSel.Face.Axis == EnumAxis.X)
                {
                    rotateX += GameMath.PIHALF * dir;
                }
                if (blockSel.Face.Axis == EnumAxis.Y)
                {
                    rotateY += GameMath.PIHALF * dir;
                }
                if (blockSel.Face.Axis == EnumAxis.Z)
                {
                    rotateZ += GameMath.PIHALF * dir;
                }
            }
            else
            {
                float deg22dot5rad = GameMath.PIHALF / 4;
                rotateY += deg22dot5rad * dir;
            }

            loadMesh();
            Blockentity.MarkDirty(true);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (Api is ICoreClientAPI capi)
            {
                if (capi.Settings.Bool["extendedDebugInfo"] == true)
                {
                    dsc.AppendLine("<font color=\"#bbbbbb\">Type:" + Type + "</font>");
                }
            }
        }

        public string GetFullCode()
        {
            return clutterBlock.BaseCodeForName() + Type?.Replace("/", "-");
        }
    }
}
