using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// A block entity behavior for a trapdoor. Requires <see cref="BlockBehaviorTrapDoor"/> on a block to work.
    /// Defined with the "TrapDoor" code.
    /// </summary>
    /// <example><code lang="json">
    ///"entityBehaviors": [
    /// { "name": "TrapDoor" }
    ///],
    ///...
    ///"behaviors": [
    /// { "name": "TrapDoor" }
    ///],
    /// </code></example>
    [DocumentAsJson]
    [AddDocumentationProperty("easingSpeed", "Affects animation speed of trapdoor opening or closing", "System.Single", "Optional", "10", true)]
    public class BEBehaviorTrapDoor : BEBehaviorAnimatable, IInteractable, IRotatable
    {
        protected bool opened;
        protected Cuboidf[] boxesClosed, boxesOpened;

        public int AttachedFace;
        public int RotDeg; 

        public float RotRad => RotDeg * GameMath.DEG2RAD;

        public BlockFacing facingWhenClosed
        {
            get
            {
                if (BlockFacing.ALLFACES[AttachedFace].IsVertical) return BlockFacing.ALLFACES[AttachedFace].Opposite;
                else return BlockFacing.DOWN.FaceWhenRotatedBy(0f, (float)BlockFacing.ALLFACES[AttachedFace].HorizontalAngleIndex * 90f * GameMath.DEG2RAD + 90f * GameMath.DEG2RAD, RotRad);
            }
        }
        public BlockFacing facingWhenOpened
        {
            get
            {
                if (BlockFacing.ALLFACES[AttachedFace].IsVertical) return BlockFacing.ALLFACES[AttachedFace].Opposite.FaceWhenRotatedBy((BlockFacing.ALLFACES[AttachedFace].Negative ? -90f : 90f) * GameMath.DEG2RAD, 0f, 0).FaceWhenRotatedBy(0f, RotRad, 0);
                else return BlockFacing.ALLFACES[AttachedFace].Opposite;
            }
        }

        protected BlockBehaviorTrapDoor doorBh;

        public Cuboidf[] ColSelBoxes => opened ? boxesOpened : boxesClosed;
        public bool Opened => opened;

        public BEBehaviorTrapDoor(BlockEntity blockentity) : base(blockentity)
        {
            boxesClosed = blockentity.Block.CollisionBoxes;

            doorBh = blockentity.Block.GetBehavior<BlockBehaviorTrapDoor>();
        }

        protected override BlockEntityAnimationUtil CreateAnimationUtil(ICoreAPI api)
        {
            float easingSpeed = Block.Attributes?["easingSpeed"].AsFloat(10) ?? 10;
            return api.Side == EnumAppSide.Client
                ? new BlockEntityAnimationUtil(api, Blockentity, BlockEntityAnimationUtil.GetSettleDurationFromEasing(easingSpeed))
                : base.CreateAnimationUtil(api);
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            UpdateHitBoxes();

            if (opened && animUtil != null && !animUtil.activeAnimationsByAnimCode.ContainsKey("opened"))
            {
                ToggleDoorWing(true);
            }
        }

        protected virtual void UpdateMeshAndAnimations()
        {
            var renderer = animUtil?.renderer;
            if (renderer == null) return;

            ApplyPlacement(renderer);
            animUtil.RefreshStaticMesh();
        }

        private void ApplyPlacement(AnimatableRenderer renderer)
        {
            renderer.CustomTransform = getTfMatrix().Values;
        }

        private Matrixf getTfMatrix(float rotz=0)
        {
            if (BlockFacing.ALLFACES[AttachedFace].IsVertical)
            {
                return new Matrixf()
                    .Translate(0.5f, 0.5f, 0.5f)
                    .RotateYDeg(RotDeg)
                    .RotateZDeg(BlockFacing.ALLFACES[AttachedFace].Negative ? 180 : 0)
                    .Translate(-0.5f, -0.5f, -0.5f)
                ;
            }

            int hai = BlockFacing.ALLFACES[AttachedFace].HorizontalAngleIndex;

            Matrixf mat = new Matrixf();
            mat
                .Translate(0.5f, 0.5f, 0.5f)
                .RotateYDeg(hai * 90)
                .RotateYDeg(90)
                .RotateZDeg(RotDeg)
                .Translate(-0.5f, -0.5f, -0.5f)
            ;
            return mat;
        }

        protected virtual void UpdateHitBoxes()
        {
            Matrixf mat = getTfMatrix();

            boxesClosed = Blockentity.Block.CollisionBoxes;
            var boxes = new Cuboidf[boxesClosed.Length];
            for (int i = 0; i < boxesClosed.Length; i++)
            {
                boxes[i] = boxesClosed[i].TransformedCopy(mat.Values);
            }


            var boxesopened = new Cuboidf[boxesClosed.Length];
            for (int i = 0; i < boxesClosed.Length; i++)
            {
                boxesopened[i] = boxesClosed[i].RotatedCopy(90, 0, 0, new Vec3d(0.5, 0.5, 0.5)).TransformedCopy(mat.Values); 
            }

            this.boxesOpened = boxesopened;
            boxesClosed = boxes;
        }

        public virtual void OnBlockPlaced(ItemStack byItemStack, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byItemStack == null) return; // Placed by worldgen

            AttachedFace = blockSel.Face.Index;

            var center = blockSel.Face.ToAB(blockSel.Face.PlaneCenter);
            var hitpos = blockSel.Face.ToAB(blockSel.HitPosition.ToVec3f());
            RotDeg = (int)Math.Round(GameMath.RAD2DEG * (float)Math.Atan2(center.A - hitpos.A, center.B - hitpos.B) / 90) * 90;

            if (blockSel.Face == BlockFacing.WEST || blockSel.Face == BlockFacing.SOUTH) RotDeg *= -1; // Why?

            UpdateHitBoxes();
        }

        public bool IsSideSolid(BlockFacing facing)
        {
            return (!opened && facing == facingWhenClosed) || (opened && facing == facingWhenOpened);
        }

        public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!doorBh.handopenable && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                (Api as ICoreClientAPI).TriggerIngameError(this, "nothandopenable", Lang.Get("This door cannot be opened by hand."));
                return true;
            }

            ToggleDoorState(byPlayer, !opened);
            handling = EnumHandling.PreventDefault;
            return true;
        }

        public void ToggleDoorState(IPlayer byPlayer, bool opened)
        {
            this.opened = opened;
            ToggleDoorWing(opened);

            var be = Blockentity;
            float pitch = opened ? 1.1f : 0.9f;

            var bh = Blockentity.Block.GetBehavior<BlockBehaviorTrapDoor>();
            var sound = opened ? bh?.OpenSound : bh?.CloseSound;

            Api.World.PlaySoundAt(sound, be.Pos.X + 0.5f, be.Pos.InternalY + 0.5f, be.Pos.Z + 0.5f, byPlayer, EnumSoundType.Sound, pitch);

            be.MarkDirty(true);

            if (Api.Side == EnumAppSide.Server)
            {
                Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
            }
        }

        private void ToggleDoorWing(bool opened)
        {
            this.opened = opened;
            if (!opened)
            {
                animUtil.StopAnimation("opened");
            }
            else
            {
                float easingSpeed = Blockentity.Block.Attributes?["easingSpeed"].AsFloat(10) ?? 10;
                animUtil.StartAnimation(new AnimationMetaData() { Animation = "opened", Code = "opened", EaseInSpeed = easingSpeed, EaseOutSpeed = easingSpeed });
            }
            Blockentity.MarkDirty();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            ensureAnimatorInitialized();
            bool skipMesh = base.OnTesselation(mesher, tessThreadTesselator);
            if (!skipMesh)
            {
                mesher.AddMeshData(doorBh.animatableOrigMesh, getTfMatrix().Values);
            }
            return true;
        }

        private void ensureAnimatorInitialized()
        {
            if (animUtil.Initialized) return;

            // Created only once per block type, so no need to worry about this one performance wise
            if (doorBh.animatableOrigMesh == null)
            {
                string animkey = "trapdoor-" + Blockentity.Block.Variant["style"];
                doorBh.animatableOrigMesh = animUtil.CreateMesh(animkey, null, out Shape shape, null);
                doorBh.animatableShape = shape;
                doorBh.animatableDictKey = animkey;
            }

            animUtil.InitializeAnimator(doorBh.animatableDictKey, doorBh.animatableOrigMesh, doorBh.animatableShape, null, true, ApplyPlacement);

            // This happens offthread. Fix potential race condition where Dispose() runs first, and only then this runs
            if (animUtil.Disposed)
            {
                animUtil.renderer?.Dispose();
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            bool beforeOpened = opened;

            AttachedFace = tree.GetInt("attachedFace");
            RotDeg = tree.GetInt("rotDeg");
            opened = tree.GetBool("opened");

            if (opened != beforeOpened && animUtil != null) ToggleDoorWing(opened);
            if (Api != null && Api.Side is EnumAppSide.Client)
            {
                UpdateMeshAndAnimations();
                if (opened && !beforeOpened && animUtil != null && !animUtil.activeAnimationsByAnimCode.ContainsKey("opened"))
                {
                    ToggleDoorWing(true);
                }
                UpdateHitBoxes();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("attachedFace", AttachedFace);
            tree.SetInt("rotDeg", RotDeg);
            tree.SetBool("opened", opened);
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            AttachedFace = tree.GetInt("attachedFace");
            var face = BlockFacing.ALLFACES[AttachedFace];
            if (face.IsVertical)
            {
                RotDeg = tree.GetInt("rotDeg");
                RotDeg = GameMath.Mod(RotDeg - degreeRotation, 360);
                tree.SetInt("rotDeg", RotDeg);
            }
            else
            {
                var rIndex = degreeRotation / 90;
                var horizontalAngleIndex = GameMath.Mod(face.HorizontalAngleIndex - rIndex, 4);
                var newFace = BlockFacing.HORIZONTALS_ANGLEORDER[horizontalAngleIndex];
                AttachedFace = newFace.Index;
                tree.SetInt("attachedFace", AttachedFace);
            }
        }
    }
}
