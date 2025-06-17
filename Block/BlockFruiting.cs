using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Used for the fruiting stages of a crop, in conjunction with BlockEntityBehavior BEBehaviorFruiting
    /// </summary>
    public class BlockFruiting : BlockCrop
    {
        double[] FruitPoints { get; set; }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            return base.GetColor(capi, pos);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (Attributes?["pickPrompt"].AsBool(false) != true)
            {
                return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            }

            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-fruiting-harvest",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = BlockUtil.GetKnifeStacks(api)
                }
            };
        }


        /// <summary>
        /// Normally the fruiting points should be provided only by the final mature stage of a plant
        /// </summary>
        public virtual double[] GetFruitingPoints()
        {
            if (FruitPoints == null) SetUpFruitPoints();
            return FruitPoints;
        }


        /// <summary>
        /// Extract fruit positions from the shape of this plant  (assumes every fruit element name starts with "fruit" and all are top level or children of the origin)
        /// </summary>
        public virtual void SetUpFruitPoints()
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            ShapeElement[] elements = capi.TesselatorManager.GetCachedShape(Shape.Base).Elements;
            double offsetX = 0;
            double offsetY = 0;
            double offsetZ = 0;
            float scaleFactor = Shape.Scale;
            if (elements.Length == 1 && elements[0].Children != null)  //origin
            {
                offsetX = (elements[0].From[0] + elements[0].To[0]) / 32.0;
                offsetY = (elements[0].From[1] + elements[0].To[1]) / 32.0;
                offsetZ = (elements[0].From[2] + elements[0].To[2]) / 32.0;
                elements = elements[0].Children;
            }

            int count = 0;
            foreach (ShapeElement element in elements)
            {
                if (element.Name.StartsWithOrdinal("fruit"))
                {
                    count++;
                }
            }

            FruitPoints = new double[count * 3];
            double[] matrix = new double[16];
            double[] triple = new double[3];
            double[] pos = new double[4];

            count = 0;
            foreach (ShapeElement element in elements)
            {
                if (element.Name.StartsWithOrdinal("fruit"))
                {
                    //Used to obtain the child positions
                    double mainX = (element.From[0]) / 16.0;
                    double mainY = (element.From[1]) / 16.0;
                    double mainZ = (element.From[2]) / 16.0;

                    double highestX = (element.To[0] - element.From[0]) / 32.0;
                    double highestY = (element.To[1] - element.From[1]) / 16.0;
                    double highestZ = (element.To[2] - element.From[2]) / 32.0;

                    if (element.Children != null)
                    {
                        foreach (ShapeElement child in element.Children)
                        {
                            pos[0] = (child.To[0] - child.From[0]) / 32.0;
                            pos[1] = (child.To[1] - child.From[1]) / 16.0;
                            pos[2] = (child.To[2] - child.From[2]) / 32.0;
                            pos[3] = 1;
                            double[] actual = Rotate(pos, child, matrix, triple);
                            if (actual[1] > highestY)
                            {
                                highestX = actual[0];
                                highestY = actual[1];
                                highestZ = actual[2];
                            }
                        }
                    }
                    pos[0] = highestX;
                    pos[1] = highestY;
                    pos[2] = highestZ;
                    pos[3] = 0;
                    double[] mainActual = Rotate(pos, element, matrix, triple);

                    FruitPoints[count * 3] = (mainActual[0] + mainX + offsetX - 0.5) * scaleFactor + 0.5 + Shape.offsetX;
                    FruitPoints[count * 3 + 1] = (mainActual[1] + mainY + offsetY) * scaleFactor + Shape.offsetY;
                    FruitPoints[count * 3 + 2] = (mainActual[2] + mainZ + offsetZ - 0.5) * scaleFactor + 0.5 + Shape.offsetZ;
                    count++;
                }
            }

        }


        /// <summary>
        /// Correctly adjust the position in accordance with the fruit element's rotation and rotationOrigin  (compare ShapeTesselator.TesselateShapeElements())
        /// </summary>
        private double[] Rotate(double[] pos, ShapeElement element, double[] matrix, double[] triple)
        {
            Mat4d.Identity(matrix);
            Mat4d.Translate(matrix, matrix, (element.RotationOrigin[0]) / 16, (element.RotationOrigin[1]) / 16, (element.RotationOrigin[2]) / 16);

            if (element.RotationX != 0)
            {
                triple[0] = 1;
                triple[1] = 0;
                triple[2] = 0;
                Mat4d.Rotate(matrix, matrix, element.RotationX * GameMath.DEG2RAD, triple);
            }
            if (element.RotationY != 0)
            {
                triple[0] = 0;
                triple[1] = 1;
                triple[2] = 0;
                Mat4d.Rotate(matrix, matrix, element.RotationY * GameMath.DEG2RAD, triple);
            }
            if (element.RotationZ != 0)
            {
                triple[0] = 0;
                triple[1] = 0;
                triple[2] = 1;
                Mat4d.Rotate(matrix, matrix, element.RotationZ * GameMath.DEG2RAD, triple);
            }

            Mat4d.Translate(matrix, matrix, (element.From[0] - element.RotationOrigin[0]) / 16, (element.From[1] - element.RotationOrigin[1]) / 16, (element.From[2] - element.RotationOrigin[2]) / 16);

            return Mat4d.MulWithVec4(matrix, pos);
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) as BlockEntityFarmland;
            if (befarmland != null && befarmland.OnBlockInteract(byPlayer)) return true;

            BEBehaviorFruiting bef = world.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorFruiting>();
            if (bef != null) return true;  //Move to BlockInteractStep

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEBehaviorFruiting bef = world.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorFruiting>();
            if (bef != null) return bef.OnPlayerInteract(secondsUsed, byPlayer, blockSel.HitPosition);

            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEBehaviorFruiting bef = world.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorFruiting>();
            if (bef != null) bef.OnPlayerInteractStop(secondsUsed, byPlayer, blockSel.HitPosition);
        }


    }
}
