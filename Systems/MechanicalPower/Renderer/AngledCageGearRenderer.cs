using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class AngledCageGearRenderer : MechBlockRenderer
    {
        static readonly Vec2f noUvOffset = new();

        readonly MaterialMeshPool pool;
        readonly float[] lightAndTransform = new float[InstanceDataSize];
        readonly Vec2f materialUvOffset = new();

        public AngledCageGearRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {

            AssetLocation loc = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = API.Common.Shape.TryGet(capi, loc);
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            var meshes = tesselateMaterialPieces(textureSoureBlock, shape, rot);
            MeshData blockMesh = meshes[0].Mesh;

            if (shapeLoc.Overlays != null)
            {
                for (int i = 0; i < shapeLoc.Overlays.Length; i++)
                {
                    CompositeShape ovShapeCmp = shapeLoc.Overlays[i];
                    rot = new Vec3f(ovShapeCmp.rotateX, ovShapeCmp.rotateY, ovShapeCmp.rotateZ);
                    
                    Shape ovshape = API.Common.Shape.TryGet(capi, ovShapeCmp.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                    capi.Tesselator.TesselateShape(textureSoureBlock, ovshape, out MeshData overlayMesh, rot);
                    blockMesh.AddMeshData(overlayMesh);
                }
            }

            //blockMesh.Rgba2 = null;

            pool = uploadMaterialMeshPool(meshes);
        }

        
        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPAngledGears gear = dev as BEBehaviorMPAngledGears;
            rotation = gear.GetLargeGearAngleRad(rotation);

            float rotX = rotation * dev.AxisSign[0];
            float rotY = rotation * dev.AxisSign[1];
            float rotZ = rotation * dev.AxisSign[2];

            UpdateLightAndTransformMatrix(lightAndTransform, 0, distToCamera, dev.LightRgba, rotX, rotY, rotZ);
            foreach (var piece in pool.Pieces)
            {
                Vec2f uvOffset = noUvOffset;
                if (piece.MaterialTextureCode != null)
                {
                    gear.TryGetMaterialUvOffset(piece.MaterialTextureCode, materialUvOffset);
                    uvOffset = materialUvOffset;
                }
                setMaterialInstance(piece.Floats.Values, index, lightAndTransform, uvOffset);
            }
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            pool.Count = quantityBlocks;
            renderMaterialMeshPool(prog, pool);
        }

        public override void Dispose()
        {
            base.Dispose();

            disposeMaterialMeshPool(pool);
        }
    }
}
