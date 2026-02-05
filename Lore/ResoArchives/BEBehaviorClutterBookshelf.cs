using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BEBehaviorClutterBookshelf : BEBehaviorShapeFromAttributes
    {        
        public string Variant;
        public string Type2;

        public BEBehaviorClutterBookshelf(BlockEntity blockentity) : base(blockentity)
        {
        }

        BookShelfVariantGroup vgroup
        {
            get
            {
                (Block as BlockClutterBookshelf).variantGroupsByCode.TryGetValue(Variant, out var vgroup);
                return vgroup;
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            Variant = byItemStack?.Attributes.GetString("variant") ?? Variant;

            if (Variant != null && vgroup?.DoubleSided == true)
            {
                Type = (Block as BlockClutterBookshelf).RandomType(Variant);
                Type2 = (Block as BlockClutterBookshelf).RandomType(Variant);
                loadMesh();
                Blockentity.MarkDirty(true);
                return;
            }

            base.OnBlockPlaced(byItemStack);
        }

        public override void loadMesh()
        {
            if (Type == null || Api.Side == EnumAppSide.Server || Variant == null) return;

            IShapeTypeProps cprops = clutterBlock.GetTypeProps(Type, null, this);
            if (cprops == null) return;

            mesh = createMesh(cprops, false);
            if (cprops.ShapeLOD2Resolved != null)
            {
                lod2mesh = createMesh(cprops, true);
            }
        }

        public override MeshData createMesh(IShapeTypeProps cprops, bool forLOD2 = false)
        {
            MeshData mesh;
            bool noOffset = offsetX == 0 && offsetY == 0 && offsetZ == 0;
            float angleY = rotateY + cprops.Rotation.Y * GameMath.DEG2RAD;
            MeshData baseMesh = clutterBlock.GetOrCreateMesh(cprops, null, null, forLOD2);

            if (rotateX == 0 && angleY == 0 && rotateZ == 0 && noOffset) mesh = baseMesh;
            else mesh = baseMesh.Clone().Rotate(Origin, rotateX, angleY, rotateZ);

            if (!noOffset) mesh.Translate(offsetX, offsetY, offsetZ);

            return mesh;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            string prevType2 = Type2;

            Variant = tree.GetString("variant");
            Type2 = tree.GetString("type2");

            if (worldAccessForResolve.Side == EnumAppSide.Client && Api != null && (mesh == null || prevType2 != Type2))
            {
                MaybeInitialiseMesh_OnMainThread();
                Blockentity.MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("variant", Variant);
            tree.SetString("type2", Type2);
        }


    }
}
