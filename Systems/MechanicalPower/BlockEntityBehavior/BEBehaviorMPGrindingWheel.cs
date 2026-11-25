using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPGrindingWheel(BlockEntity blockentity) : BEBehaviorMPAxle(blockentity)
    {
        readonly BlockEntityGrindingWheel grindingWheel = (BlockEntityGrindingWheel)blockentity;
        readonly Vec2f woodUvOffset = new();
        readonly Vec2f stoneSideUvOffset = new();
        readonly Vec2f stoneEdgeUvOffset = new();

        protected override CompositeShape GetShape()
        {
            return getGrindingWheelShape(Api.World);
        }

        protected override void updateShape(IWorldAccessor worldForResolve)
        {
            Shape = getGrindingWheelShape(worldForResolve);
        }

        CompositeShape getGrindingWheelShape(IWorldAccessor world)
        {
            CompositeShape shape = getAxleShape(world).Clone();
            shape.SelectiveElements = grindingWheel.HasGrindstone ? ["Shaft1/*"] : ["Shaft1/Shaft2"];
            return shape;
        }

        public override float GetResistance()
        {
            return grindingWheel.HasGrindstone ? 0.05f : base.GetResistance();
        }

        public override void OnBlockPlaced(ItemStack? byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            updateComponentMaterialUvOffsets();
        }

        public void RefreshComponents()
        {
            updateComponentMaterialUvOffsets();
            updateShape(Api.World);
        }

        void updateComponentMaterialUvOffsets()
        {
            woodUvOffset.X = woodUvOffset.Y = 0;
            stoneSideUvOffset.X = stoneSideUvOffset.Y = 0;
            stoneEdgeUvOffset.X = stoneEdgeUvOffset.Y = 0;
            if (Api is ICoreClientAPI capi && Block is BlockGrindingWheelAssembly assembly)
            {
                assembly.GetMaterialUvOffset(capi, "wood", PlacedStack?.Attributes.GetString("wood"), woodUvOffset);
                string? stone = grindingWheel.GetGrindstoneMaterial();
                if (stone != null)
                {
                    assembly.GetMaterialUvOffset(capi, "sides", stone, stoneSideUvOffset);
                    assembly.GetMaterialUvOffset(capi, "edge", stone, stoneEdgeUvOffset);
                }
            }
        }

        public override bool TryGetMaterialUvOffset(string textureCode, Vec2f offset)
        {
            Vec2f? materialOffset = textureCode switch
            {
                "wood" => woodUvOffset,
                "sides" => stoneSideUvOffset,
                "edge" => stoneEdgeUvOffset,
                _ => null
            };
            if (materialOffset == null) return base.TryGetMaterialUvOffset(textureCode, offset);

            offset.X = materialOffset.X;
            offset.Y = materialOffset.Y;
            return true;
        }
    }
}
