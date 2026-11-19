using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class HostedSpurGearShape : CompositeShape
    {
        public CompositeShape SourceShape { get; }
        public float SpinPhaseDeg { get; }
        public BlockSpurGear DiscBlock { get; }
        public BlockFacing Face { get; }

        public HostedSpurGearShape(CompositeShape sourceShape, float spinPhaseDeg, BlockSpurGear discBlock, BlockFacing face)
        {
            SourceShape = sourceShape;
            SpinPhaseDeg = spinPhaseDeg;
            DiscBlock = discBlock;
            Face = face;
            Base = sourceShape.Base;
        }

        public override int GetHashCode()
        {
            return SourceShape.GetHashCode() ^ SpinPhaseDeg.GetHashCode() ^ DiscBlock.BlockId;
        }
    }

    public class FaceMountedGearStacks<TGear> where TGear : Block
    {
        readonly ItemStack[] gearStacks = new ItemStack[BlockFacing.ALLFACES.Length];
        readonly string treePrefix;

        public FaceMountedGearStacks(string treePrefix)
        {
            this.treePrefix = treePrefix;
        }

        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < gearStacks.Length; i++) if (gearStacks[i]?.Block is TGear) count++;
                return count;
            }
        }

        public bool Has(BlockFacing face) => GetBlock(face) != null;

        // Returns false if the face is already occupied.
        public bool TryAdd(BlockFacing face, ItemStack gearStack)
        {
            if (face == null || gearStack?.Block is not TGear || Has(face)) return false;

            ItemStack stack = gearStack.Clone();
            stack.StackSize = 1;
            gearStacks[face.Index] = stack;
            return true;
        }

        public void Remove(BlockFacing face)
        {
            if (face == null || gearStacks[face.Index] == null) return;

            gearStacks[face.Index] = null;
        }

        public void GetMaterialUvOffset(ICoreClientAPI capi, BlockFacing face, string textureCode, Vec2f offset)
        {
            ItemStack stack = getStack(face);
            if (stack?.Block is BlockMPBase materialBlock)
            {
                materialBlock.GetMaterialUvOffset(capi, stack, textureCode, offset);
                return;
            }

            offset.X = 0;
            offset.Y = 0;
        }

        ItemStack getStack(BlockFacing face)
        {
            return face == null ? null : gearStacks[face.Index];
        }

        public TGear GetBlock(BlockFacing face)
        {
            return getStack(face)?.Block as TGear;
        }

        public ItemStack GetDrop(BlockFacing face)
        {
            ItemStack stack = getStack(face);
            return stack?.Block is TGear ? stack.Clone() : null;
        }

        public ItemStack GetFirstDrop()
        {
            for (int i = 0; i < gearStacks.Length; i++)
            {
                if (gearStacks[i]?.Block is TGear) return gearStacks[i].Clone();
            }
            return null;
        }

        public ItemStack[] GetDrops()
        {
            List<ItemStack> drops = [];
            for (int i = 0; i < gearStacks.Length; i++)
            {
                ItemStack stack = gearStacks[i];
                if (stack?.Block is TGear) drops.Add(stack.Clone());
            }
            return drops.ToArray();
        }

        public SmallBoolArray GetFaces()
        {
            SmallBoolArray faces = new();
            for (int i = 0; i < gearStacks.Length; i++) faces[i] = gearStacks[i]?.Block is TGear;
            return faces;
        }

        public void ToTreeAttributes(ITreeAttribute tree)
        {
            for (int i = 0; i < gearStacks.Length; i++)
            {
                ItemStack stack = gearStacks[i];
                if (stack?.Block is not TGear) tree.RemoveAttribute(getStackKey(i));
                else tree.SetItemstack(getStackKey(i), stack);
            }
        }

        public void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
        {
            for (int i = 0; i < gearStacks.Length; i++)
            {
                ItemStack stack = tree.GetItemstack(getStackKey(i));
                stack?.ResolveBlockOrItem(world);
                gearStacks[i] = stack;
            }
        }

        public void OnLoadCollectibleMappings(IWorldAccessor world, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, bool resolveImports)
        {
            for (int i = 0; i < gearStacks.Length; i++)
            {
                ItemStack stack = gearStacks[i];
                if (stack == null) continue;

                if (!stack.FixMapping(oldBlockIdMapping, oldItemIdMapping, world)
                    || stack.Block is not TGear)
                {
                    gearStacks[i] = null;
                    continue;
                }

                stack.Collectible.OnLoadCollectibleMappings(world, new DummySlot(stack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
            }
        }

        public void OnStoreCollectibleMappings(IWorldAccessor world, Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            for (int i = 0; i < gearStacks.Length; i++)
            {
                ItemStack stack = gearStacks[i];
                if (stack?.Block is TGear) stack.Collectible.OnStoreCollectibleMappings(world, new DummySlot(stack), blockIdMapping, itemIdMapping);
            }
        }

        public void OnTransformed(ITreeAttribute tree, int degreeRotation, EnumAxis? flipAxis)
        {
            ItemStack[] transformedStacks = new ItemStack[gearStacks.Length];
            for (int i = 0; i < transformedStacks.Length; i++)
            {
                BlockFacing face = getTransformedFace(BlockFacing.ALLFACES[i], degreeRotation, flipAxis);
                ItemStack stack = tree.GetItemstack(getStackKey(i));
                if (stack != null) transformedStacks[face.Index] = stack;
            }

            for (int i = 0; i < transformedStacks.Length; i++)
            {
                tree.RemoveAttribute(getStackKey(i));
                if (transformedStacks[i] != null) tree.SetItemstack(getStackKey(i), transformedStacks[i]);
            }
        }

        static BlockFacing getTransformedFace(BlockFacing face, int degreeRotation, EnumAxis? flipAxis)
        {
            BlockFacing flippedFace = face.Axis == flipAxis ? face.Opposite : face;
            return flippedFace.GetHorizontalRotated(degreeRotation);
        }

        string getStackKey(int faceIndex) => $"{treePrefix}Stack{faceIndex}";
    }

    public class HostedSpurGears : FaceMountedGearStacks<BlockSpurGear>
    {
        public HostedSpurGears(string treePrefix) : base(treePrefix)
        {
        }

        public int GetToothCount(BlockFacing face) => GetBlock(face)?.ToothCount ?? 0;

        public float GetRadius(BlockFacing face) => GetBlock(face)?.PitchRadius ?? 0f;

        public BlockSpurGear GetDiscBlock(BlockFacing face) => GetBlock(face);

        public CompositeShape GetFaceShape(IWorldAccessor world, BlockFacing face, float spinPhaseDeg = 0f)
        {
            BlockSpurGear disc = GetDiscBlock(face);
            if (disc == null) return null;

            BlockSpurGear faceBlock = world.GetBlock(disc.CodeWithVariant("orientation", $"{face.Code[0]}")) as BlockSpurGear;
            if (faceBlock?.Shape == null) return null;
            return new HostedSpurGearShape(faceBlock.Shape, spinPhaseDeg, faceBlock, face);
        }
    }
}
