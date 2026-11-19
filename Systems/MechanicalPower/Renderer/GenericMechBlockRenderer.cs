using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using ShapeTransform = System.Func<Vintagestory.API.Common.Shape, Vintagestory.API.Common.Shape>;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class GenericMechBlockRenderer : MechBlockRenderer
    {
        class MeshPiece
        {
            public MultiTextureMeshRef MeshRef;
            public CustomMeshDataPartFloat Floats;
            public string MaterialTextureCode;
            public BlockFacing HostedFace;
        }

        static readonly Vec2f noUvOffset = new();

        readonly List<MeshPiece> pieces = [];
        readonly float[] lightAndTransform = new float[InstanceDataSize];
        readonly Vec2f materialUvOffset = new();

        public GenericMechBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc)
            : this(capi, mechanicalPowerMod, textureSoureBlock, shapeLoc, null)
        {
        }

        protected GenericMechBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc, ShapeTransform transformShape) : base(capi, mechanicalPowerMod)
        {
            AssetLocation loc = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = API.Common.Shape.TryGet(capi, loc);
            if (transformShape != null) shape = transformShape(shape.Clone());
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            var blockPieces = tesselateMaterialPieces(textureSoureBlock, shape, rot, shapeLoc.QuantityElements, shapeLoc.SelectiveElements);
            MeshData blockMesh = blockPieces[0].Mesh;
            for (int i = 1; i < blockPieces.Count; i++) addPiece(blockPieces[i].Mesh, blockPieces[i].MaterialTextureCode, null);
            
            if (shapeLoc.Overlays != null)
            {
                for (int i = 0; i < shapeLoc.Overlays.Length; i++)
                {
                    CompositeShape ovShapeCmp = shapeLoc.Overlays[i];
                    HostedSpurGearShape hostedGearShape = ovShapeCmp as HostedSpurGearShape;
                    CompositeShape sourceShape = hostedGearShape?.SourceShape ?? ovShapeCmp;
                    AssetLocation ovLoc = sourceShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                    Shape ovshape = API.Common.Shape.TryGet(capi, ovLoc);

                    if (hostedGearShape != null)
                    {
                        // The disc is mounted on the shaft this block already draws, so its own hub is left out.
                        rot = new Vec3f(sourceShape.rotateX, sourceShape.rotateY, sourceShape.rotateZ + hostedGearShape.SpinPhaseDeg);
                        var discPieces = tesselateMaterialPieces(hostedGearShape.DiscBlock, SpurGearRenderer.GetDiscShape(ovshape), rot);
                        blockMesh.AddMeshData(discPieces[0].Mesh);
                        for (int j = 1; j < discPieces.Count; j++) addPiece(discPieces[j].Mesh, discPieces[j].MaterialTextureCode, hostedGearShape.Face);
                    }
                    else
                    {
                        rot = new Vec3f(sourceShape.rotateX, sourceShape.rotateY, sourceShape.rotateZ);
                        capi.Tesselator.TesselateShape(textureSoureBlock, ovshape, out MeshData overlayMesh, rot);
                        blockMesh.AddMeshData(overlayMesh);
                    }
                }
            }

            addPiece(blockMesh, null, null);
        }

        void addPiece(MeshData mesh, string materialTextureCode, BlockFacing hostedFace)
        {
            if (mesh.VerticesCount == 0) return;

            CustomMeshDataPartFloat floats = createMaterialInstanceFloats();
            pieces.Add(new MeshPiece { MeshRef = uploadInstancedMesh(mesh, floats), Floats = floats, MaterialTextureCode = materialTextureCode, HostedFace = hostedFace });
        }

        
        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
        {
            float rotX = rotation * dev.AxisSign[0];
            float rotY = rotation * dev.AxisSign[1];
            float rotZ = rotation * dev.AxisSign[2];
            if (dev is BEBehaviorMPToggle tog && (dev.AxisSign[0] == 0 ^ tog.IsRotationReversed()))
            {
                rotY = GameMath.PI;
                rotZ = -rotZ;
            }

            UpdateLightAndTransformMatrix(lightAndTransform, 0, distToCamera, dev.LightRgba, rotX, rotY, rotZ);
            foreach (MeshPiece piece in pieces)
            {
                Vec2f uvOffset = noUvOffset;
                if (piece.HostedFace != null && dev is BEBehaviorMPAxle axle)
                {
                    axle.GetHostedMaterialUvOffset(capi, piece.HostedFace, piece.MaterialTextureCode, materialUvOffset);
                    uvOffset = materialUvOffset;
                }
                else if (piece.MaterialTextureCode != null && dev is BEBehaviorMPBase behavior)
                {
                    behavior.TryGetMaterialUvOffset(piece.MaterialTextureCode, materialUvOffset);
                    uvOffset = materialUvOffset;
                }
                setMaterialInstance(piece.Floats.Values, index, lightAndTransform, uvOffset);
            }
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks > 0)
            {
                foreach (MeshPiece piece in pieces) renderInstancedMesh(prog, piece.MeshRef, piece.Floats, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            foreach (MeshPiece piece in pieces) piece.MeshRef.Dispose();
        }
    }
}
