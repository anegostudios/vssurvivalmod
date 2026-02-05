using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class BEBehaviorRotatablePlaceable : BlockEntityBehavior
{
    Cuboidf[]? selectionBoxes;
    Cuboidf[]? collisionboxes;

    public BEBehaviorRotatablePlaceable(BlockEntity blockentity) : base(blockentity) { }

    public float MeshAngleRad { get; set; }


    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        MeshAngleRad = tree.GetFloat("meshAngleRad");
        Blockentity.MarkDirty(true);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("meshAngleRad", MeshAngleRad);
    }

    public Cuboidf[] GetCollisionBoxes()
    {
        if (collisionboxes == null)
        {
            collisionboxes = Block.CollisionBoxes.Select(box => box.RotatedCopy(0, MeshAngleRad * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0, 0.5))).ToArray();
        }

        return collisionboxes;
    }

    public Cuboidf[] GetSelectionBoxes()
    {
        if (selectionBoxes == null)
        {
            selectionBoxes = Block.SelectionBoxes.Select(box => box.RotatedCopy(0, MeshAngleRad * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0, 0.5))).ToArray();
        }

        return selectionBoxes;
    }

    public bool DoPartialSelection()
    {
        return true;
    }
}
