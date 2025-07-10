using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BookShelfTypeProps : IShapeTypeProps
    {
        public BookShelfVariantGroup group;
        static Random rnd = new Random();

        public string Code { get; set; }
        public string Type1 { get; set; }
        public string Type2 { get; set; }

        public string Variant { get; set; }

        public Vec3f Rotation => group.Rotation;
        public Cuboidf[] ColSelBoxes { get { return group.ColSelBoxes; } set { group.ColSelBoxes = value; } }
        public Cuboidf[] SelBoxes { get; set; } = null;
        public ModelTransform GuiTransform { get { return group.GuiTf; } set { group.GuiTf = value; } }
        public ModelTransform FpTtransform { get { return group.FpTf; } set { group.FpTf = value; } }
        public ModelTransform TpTransform { get { return group.TpTf; } set { group.TpTf = value; } }
        public ModelTransform GroundTransform { get { return group.GroundTf; } set { group.GroundTf = value; } }
        public string RotInterval { get { return group.RotInterval; } set { group.RotInterval = value; } }

        public string FirstTexture { get; set; }
        public TextureAtlasPosition TexPos { get; set; }
        public Dictionary<long, Cuboidf[]> ColSelBoxesByHashkey { get { return group.ColSelBoxesByHashkey; } set { group.ColSelBoxesByHashkey = value; } }
        public Dictionary<long, Cuboidf[]> SelBoxesByHashkey { get; set; }

        public AssetLocation ShapePath
        {
            get
            {
                if (Variant.Contains("doublesided"))
                {
                    if (Type1 == null)
                    {
                        int rndindex = rnd.Next(group.typesByCode.Count);
                        Type1 = group.typesByCode.GetKeyAtIndex(rndindex);
                    }
                    return AssetLocation.Create("shapes/" + group.block.basePath + "/" + Type1 + ".json", group.block.Code.Domain);
                }

                return AssetLocation.Create("shapes/" + group.block.basePath + "/" + Code + ".json", group.block.Code.Domain);
            }
        }
        public AssetLocation ShapePath2 {
            get
            {
                if (Variant.Contains("doublesided"))
                {
                    if (Type2 == null)
                    {
                        int rndindex = rnd.Next(group.typesByCode.Count);
                        Type2 = group.typesByCode.GetKeyAtIndex(rndindex);
                    }
                    return AssetLocation.Create("shapes/" + group.block.basePath + "/" + Type2 + ".json", group.block.Code.Domain);
                }

                return ShapePath;
            }
        }
        public Shape ShapeResolved { get; set; }
        public Shape ShapeResolved2 { get; set; }

        public string HashKey => Code + "-" + Type1 + "-" + Type2 + "-" + Variant;

        public bool RandomizeYSize => false;

        public byte[] LightHsv { get; set; }

        public Dictionary<string, CompositeTexture> Textures => null;

        public string TextureFlipCode { get; set; }

        public string TextureFlipGroupCode { get; set; }
        BlockDropItemStack[] IShapeTypeProps.Drops { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string HeldIdleAnim { get; set; }
        public string HeldReadyAnim { get; set; }
        public int Reparability { get; set; }

        public bool CanAttachBlockAt(Vec3f blockRot, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            return false;
        }
    }
}
