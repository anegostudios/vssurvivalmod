using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent.Mechanics
{
    public sealed class AxleArmRenderer : GenericMechBlockRenderer
    {
        const double shapeUnitsPerBlock = 16;
        const double largePinExpansion = 0.5;

        public AxleArmRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSourceBlock, CompositeShape shapeLoc)
            : base(capi, mechanicalPowerMod, textureSourceBlock, shapeLoc, shape => shapeLoc is AxleArmShape axleArmShape ? transformPeg(shape, axleArmShape) : shape)
        {
        }

        static Shape transformPeg(Shape shape, AxleArmShape axleArmShape)
        {
            ShapeElement? peg = shape.GetElementByName("peg");
            if (peg?.From == null || peg.To == null || peg.RotationOrigin == null) return shape;

            double holeOffset = -shapeUnitsPerBlock * BlockAxleArm.CrankHoleSpacing * axleArmShape.CrankHole;
            peg.From[1] += holeOffset;
            peg.To[1] += holeOffset;
            peg.RotationOrigin[1] += holeOffset;
            if (axleArmShape.PinSize > 1)
            {
                peg.From[1] -= largePinExpansion;
                peg.To[1] += largePinExpansion;
                peg.From[2] -= largePinExpansion;
                peg.To[2] += largePinExpansion;
            }

            return shape;
        }
    }

    public sealed class AxleArmShape : CompositeShape
    {
        public int CrankHole { get; }
        public int PinSize { get; }

        public AxleArmShape(CompositeShape source, int crankHole, int pinSize)
        {
            Base = source.Base;
            Format = source.Format;
            InsertBakedTextures = source.InsertBakedTextures;
            rotateX = source.rotateX;
            rotateY = source.rotateY;
            rotateZ = source.rotateZ;
            offsetX = source.offsetX;
            offsetY = source.offsetY;
            offsetZ = source.offsetZ;
            Scale = source.Scale;
            Alternates = source.Alternates;
            BakedAlternates = source.BakedAlternates;
            Overlays = source.Overlays;
            VoxelizeTexture = source.VoxelizeTexture;
            QuantityElements = source.QuantityElements;
            SelectiveElements = source.SelectiveElements;
            IgnoreElements = source.IgnoreElements;
            CrankHole = crankHole;
            PinSize = pinSize;
        }

        public override int GetHashCode()
        {
            return (base.GetHashCode() * 397 ^ CrankHole) * 397 ^ PinSize;
        }
    }
}
