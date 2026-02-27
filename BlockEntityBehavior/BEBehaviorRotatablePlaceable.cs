using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class BEBehaviorRotatablePlaceable : BlockEntityBehavior, IMultiBlockColSelBoxes
{
    Cuboidf[][] selectionBoxes;
    Cuboidf[][] collisionboxes;
    float height;

    public BEBehaviorRotatablePlaceable(BlockEntity blockentity) : base(blockentity) { }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        height = properties["height"].AsFloat(1);
        selectionBoxes = null;
        collisionboxes = null;
    }

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
        if (collisionboxes == null) collisionboxes = rotatedCopy(Block.CollisionBoxes);
        return collisionboxes[0];
    }


    public Cuboidf[] GetSelectionBoxes()
    {
        if (selectionBoxes == null) selectionBoxes = rotatedCopy(Block.SelectionBoxes);
        return selectionBoxes[0];
    }

    Cuboidf[][] rotatedCopy(Cuboidf[] origboxes)
    {
        var rotatedBoxes = new Cuboidf[(int)height + 1][];

        int y = 0;
        while (y < height) 
        {
            var boxes = origboxes.Select(box => box.RotatedCopy(0, MeshAngleRad * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0, 0.5))).ToArray();
            foreach (var box in boxes)
            {
                box.Offset(0, -y, 0);
            }
            rotatedBoxes[y] = boxes;
            y++;
        }

        return rotatedBoxes;
    }




    public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
    {
        if (collisionboxes == null) collisionboxes = rotatedCopy(Block.CollisionBoxes);
        return collisionboxes[GameMath.Clamp(-offset.Y, 0, collisionboxes.Length-1)];
    }

    public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
    {
        if (selectionBoxes == null) selectionBoxes = rotatedCopy(Block.SelectionBoxes);
        return selectionBoxes[GameMath.Clamp(-offset.Y, 0, selectionBoxes.Length - 1)];
    }


    public bool DoPartialSelection()
    {
        return true;
    }
}
