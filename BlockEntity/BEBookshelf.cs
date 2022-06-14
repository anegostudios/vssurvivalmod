using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class BlockEntityBookshelf : BlockEntityShapeFromAttributes
    {
        public string Variant;
        public string Type2;

        BookShelfVariantGroup vgroup
        {
            get
            {
                (Block as BlockBookShelf).variantGroupsByCode.TryGetValue(Variant, out var vgroup);
                return vgroup;
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            Variant = byItemStack?.Attributes.GetString("variant");

            if (Variant != null && vgroup?.DoubleSided == true)
            {
                Type = (Block as BlockBookShelf).RandomType(Variant);
                Type2 = (Block as BlockBookShelf).RandomType(Variant);
                initShape();
                MarkDirty(true);
                return;
            }

            base.OnBlockPlaced(byItemStack);
        }

        protected override void initShape()
        {
            if (Type == null || Api.Side == EnumAppSide.Server || Variant == null) return;

            IShapeTypeProps cprops = clutterBlock.GetTypeProps(Type, null, this);
            if (cprops == null) return;

            mesh = clutterBlock.GenMesh(cprops).Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngleRad + cprops.Rotation.Y * GameMath.DEG2RAD, 0);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            Variant = tree.GetString("variant");
            Type2 = tree.GetString("type2");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("variant", Variant);
            tree.SetString("type2", Type2);
        }
    }
}
