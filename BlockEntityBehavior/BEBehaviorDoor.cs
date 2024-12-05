using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{

    public class BEBehaviorDoor : BEBehaviorAnimatable, IInteractable, IRotatable
    {
        public float RotateYRad;
        protected bool opened;
        protected bool invertHandles;
        protected MeshData mesh;
        protected Cuboidf[] boxesClosed, boxesOpened;

        public BlockFacing facingWhenClosed
        {
            get
            {
                int face = ((int)(RotateYRad / (GameMath.PIHALF - 0.00001F)) % 4 + 4) % 4;
                switch (face)
                {
                    case 0: return BlockFacing.SOUTH;
                    case 1: return BlockFacing.EAST;
                    case 2: return BlockFacing.NORTH;
                    case 3: return BlockFacing.WEST;
                    default: return BlockFacing.SOUTH;
                }
            }
        }

        /// <summary>
        /// A rather counter-intuitive property, setting this actually sets up an internal Vec3i giving the offset to the Pos of the supplied door
        /// </summary>
        BEBehaviorDoor leftDoor
        {
            get { return leftDoorOffset == null ? null : BlockBehaviorDoor.getDoorAt(Api.World, Pos.AddCopy(leftDoorOffset)); }
            set { leftDoorOffset = value == null ? null : value.Pos.SubCopy(Pos).ToVec3i(); }
        }
        /// <summary>
        /// A rather counter-intuitive property, setting this actually sets up an internal Vec3i giving the offset to the Pos of the supplied door
        /// </summary>
        BEBehaviorDoor rightDoor
        {
            get { return rightDoorOffset == null ? null : BlockBehaviorDoor.getDoorAt(Api.World, Pos.AddCopy(rightDoorOffset)); }
            set { rightDoorOffset = value == null ? null : value.Pos.SubCopy(Pos).ToVec3i(); }
        }

        protected Vec3i leftDoorOffset;
        protected Vec3i rightDoorOffset;

        protected BlockBehaviorDoor doorBh;

        public Cuboidf[] ColSelBoxes => opened ? boxesOpened : boxesClosed;
        public bool Opened => opened;
        public bool InvertHandles => invertHandles;

        public BEBehaviorDoor(BlockEntity blockentity) : base(blockentity)
        {
            boxesClosed = blockentity.Block.CollisionBoxes;

            doorBh = blockentity.Block.GetBehavior<BlockBehaviorDoor>();
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            SetupRotationsAndColSelBoxes(false);

            if (opened && animUtil != null && !animUtil.activeAnimationsByAnimCode.ContainsKey("opened"))
            {
                ToggleDoorWing(true);
            }
        }


        public BlockPos getAdjacentPosition(int right, int back = 0, int up = 0)
        {
            return Blockentity.Pos.AddCopy(getAdjacentOffset(right, back, up, RotateYRad, invertHandles));
        }

        public Vec3i getAdjacentOffset(int right, int back = 0, int up = 0)
        {
            return getAdjacentOffset(right, back, up, RotateYRad, invertHandles);
        }

        public static Vec3i getAdjacentOffset(int right, int back, int up, float rotateYRad, bool invertHandles)
        {
            if (invertHandles) right = -right;
            return new Vec3i(
                right * (int)Math.Round(Math.Sin(rotateYRad + GameMath.PIHALF)) - back * (int)Math.Round(Math.Sin(rotateYRad)),
                up,
                right * (int)Math.Round(Math.Cos(rotateYRad + GameMath.PIHALF)) - back * (int)Math.Round(Math.Cos(rotateYRad))
            );
        }

        internal void SetupRotationsAndColSelBoxes(bool initalSetup)
        {
            int width = doorBh.width;
            if (initalSetup)
            {
                BlockPos leftPos = Blockentity.Pos.AddCopy(width * (int)Math.Round(Math.Sin(RotateYRad - GameMath.PIHALF)), 0, width * (int)Math.Round(Math.Cos(RotateYRad - GameMath.PIHALF)));
                leftDoor = BlockBehaviorDoor.getDoorAt(Api.World, leftPos);
            }

            if (leftDoor != null && !leftDoor.invertHandles && invertHandles)
            {
                leftDoor.rightDoor = this;
            }

            if (initalSetup)
            {
                if (leftDoor != null && !leftDoor.invertHandles)
                {
                    invertHandles = true;
                    leftDoor.rightDoor = this;
                    Blockentity.MarkDirty(true);
                }

                BlockPos rightPos = Blockentity.Pos.AddCopy(width * (int)Math.Round(Math.Sin(RotateYRad + GameMath.PIHALF)), 0, width * (int)Math.Round(Math.Cos(RotateYRad + GameMath.PIHALF)));
                BlockPos rightrightPos = Blockentity.Pos.AddCopy(width * 2 * (int)Math.Round(Math.Sin(RotateYRad + GameMath.PIHALF)), 0, width * 2 * (int)Math.Round(Math.Cos(RotateYRad + GameMath.PIHALF)));
                var rightDoor = BlockBehaviorDoor.getDoorAt(Api.World, rightPos);
                var rightrightDoor = BlockBehaviorDoor.getDoorAt(Api.World, rightrightPos);

                if (leftDoor == null && rightDoor != null && !rightDoor.invertHandles && (rightrightDoor?.invertHandles != true))
                {
                    if (width > 1 && Api.Side == EnumAppSide.Server)
                    {
                        Api.World.BlockAccessor.SetBlock(0, rightDoor.Blockentity.Pos);
                        BlockPos rightDoorPos = Blockentity.Pos.AddCopy((2 * width - 1) * (int)Math.Round(Math.Sin(RotateYRad + GameMath.PIHALF)), 0, (2 * width - 1) * (int)Math.Round(Math.Cos(RotateYRad + GameMath.PIHALF)));
                        Api.World.BlockAccessor.SetBlock(Block.Id, rightDoorPos);
                        rightDoor = Block.GetBEBehavior<BEBehaviorDoor>(rightDoorPos);
                        rightDoor.RotateYRad = RotateYRad;
                        rightDoor.invertHandles = true;
                        Block.GetBehavior<BlockBehaviorDoor>().placeMultiblockParts(Api.World, rightDoorPos);
                        this.rightDoor = rightDoor;
                        rightDoor.SetupRotationsAndColSelBoxes(true);
                        rightDoor.leftDoor = this;
                        rightDoor.Blockentity.MarkDirty(true);
                    }
                }
            }

            if (Api.Side == EnumAppSide.Client)
            {
                if (doorBh.animatableOrigMesh == null)
                {
                    string animkey = "door-" + Blockentity.Block.Variant["style"];
                    doorBh.animatableOrigMesh = animUtil.CreateMesh(animkey, null, out Shape shape, null);
                    doorBh.animatableShape = shape;
                    doorBh.animatableDictKey = animkey;
                }
                if (doorBh.animatableOrigMesh != null)
                {
                    animUtil.InitializeAnimator(doorBh.animatableDictKey, doorBh.animatableOrigMesh, doorBh.animatableShape, null);
                    UpdateMeshAndAnimations();
                }
            }

            UpdateHitBoxes();
        }

        protected virtual void UpdateMeshAndAnimations()
        {
            mesh = doorBh.animatableOrigMesh.Clone();
            if (RotateYRad != 0)
            {
                float rot = invertHandles ? -RotateYRad : RotateYRad;
                mesh = mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, rot, 0);
                animUtil.renderer.rotationDeg.Y = rot * GameMath.RAD2DEG;
            }

            if (invertHandles)
            {
                // We need a full matrix transform for this to update the normals as well
                Matrixf matf = new Matrixf();
                matf.Translate(0.5f, 0.5f, 0.5f).Scale(-1, 1, 1).Translate(-0.5f, -0.5f, -0.5f);
                mesh.MatrixTransform(matf.Values);

                animUtil.renderer.backfaceCulling = false;
                animUtil.renderer.ScaleX = -1;
            }
        }

        protected virtual void UpdateHitBoxes()
        {
            if (RotateYRad != 0)
            {
                boxesClosed = Blockentity.Block.CollisionBoxes;
                var boxes = new Cuboidf[boxesClosed.Length];
                for (int i = 0; i < boxesClosed.Length; i++)
                {
                    boxes[i] = boxesClosed[i].RotatedCopy(0, RotateYRad * GameMath.RAD2DEG, 0, new Vec3d(0.5, 0.5, 0.5));
                }

                boxesClosed = boxes;
            }

            var boxesopened = new Cuboidf[boxesClosed.Length];
            for (int i = 0; i < boxesClosed.Length; i++)
            {
                boxesopened[i] = boxesClosed[i].RotatedCopy(0, invertHandles ? 90 : -90, 0, new Vec3d(0.5, 0.5, 0.5));
            }

            this.boxesOpened = boxesopened;
        }

        public virtual void OnBlockPlaced(ItemStack byItemStack, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byItemStack == null) return; // Placed by worldgen

            RotateYRad = getRotateYRad(byPlayer, blockSel);
            SetupRotationsAndColSelBoxes(true);
        }

        public static float getRotateYRad(IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
            double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
            double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
            float angleHor = (float)Math.Atan2(dx, dz);

            float deg90 = GameMath.PIHALF;
            return ((int)Math.Round(angleHor / deg90)) * deg90;
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
            float breakChance = Block.Attributes["breakOnTriggerChance"].AsFloat(0);
            if (Api.Side == EnumAppSide.Server && Api.World.Rand.NextDouble() < breakChance && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                Api.World.BlockAccessor.BreakBlock(Pos, byPlayer);
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), Pos, 0, null);
                return;
            }

            this.opened = opened;
            ToggleDoorWing(opened);

            var be = Blockentity;
            float pitch = opened ? 1.1f : 0.9f;

            var bh = Blockentity.Block.GetBehavior<BlockBehaviorDoor>();
            var sound = opened ? bh?.OpenSound : bh?.CloseSound;

            Api.World.PlaySoundAt(sound, be.Pos.X + 0.5f, be.Pos.InternalY + 0.5f, be.Pos.Z + 0.5f, byPlayer, EnumSoundType.Sound, pitch);

            if (leftDoor != null && invertHandles) leftDoor.ToggleDoorWing(opened);
            if (rightDoor != null) rightDoor.ToggleDoorWing(opened);

            be.MarkDirty(true);

            if (Api.Side == EnumAppSide.Server)
            {
                BlockPos tempPos = new BlockPos();
                tempPos.dimension = Pos.dimension;
                for (int y = 0; y < doorBh.height; y++)
                {
                    tempPos.Set(Pos).Add(0, y, 0);
                    BlockFacing sideMove;
                    int face = ((int)(RotateYRad / (GameMath.PIHALF - 0.00001F)) % 4 + 4) % 4;
                    switch (face)
                    {
                        case 0:
                            sideMove = BlockFacing.EAST;
                            break;
                        case 1:
                            sideMove = BlockFacing.NORTH;
                            break;
                        case 2:
                            sideMove = BlockFacing.WEST;
                            break;
                        case 3:
                            sideMove = BlockFacing.SOUTH;
                            break;
                        default:
                            sideMove = BlockFacing.EAST;
                            break;
                    }
                    if (invertHandles) sideMove = sideMove.Opposite;

                    for (int x = 0; x < doorBh.width; x++)
                    {
                        Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(tempPos);
                        tempPos.Add(sideMove);
                    }
                }
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
            bool skipMesh = base.OnTesselation(mesher, tessThreadTesselator);
            if (!skipMesh)
            {
                mesher.AddMeshData(mesh);
            }
            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            bool beforeOpened = opened;

            RotateYRad = tree.GetFloat("rotateYRad");
            opened = tree.GetBool("opened");
            invertHandles = tree.GetBool("invertHandles");
            leftDoorOffset = tree.GetVec3i("leftDoorPos");
            rightDoorOffset = tree.GetVec3i("rightDoorPos");

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

            tree.SetFloat("rotateYRad", RotateYRad);
            tree.SetBool("opened", opened);
            tree.SetBool("invertHandles", invertHandles);
            if (leftDoorOffset != null) tree.SetVec3i("leftDoorPos", leftDoorOffset);
            if (rightDoorOffset != null) tree.SetVec3i("rightDoorPos", rightDoorOffset);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (Api is ICoreClientAPI capi)
            {
                if (capi.Settings.Bool["extendedDebugInfo"] == true)
                {
                    dsc.AppendLine("" + facingWhenClosed + (invertHandles ? "-inv " : " ") + (opened ? "open" : "closed"));
                    dsc.AppendLine("" + doorBh.height + "x" + doorBh.width + (leftDoorOffset != null ? " leftdoor at:" + leftDoorOffset : " ") + (rightDoorOffset != null ? " rightdoor at:" + rightDoorOffset : " "));
                    EnumHandling h = EnumHandling.PassThrough;
                    if (doorBh.GetLiquidBarrierHeightOnSide(BlockFacing.NORTH, Pos, ref h) > 0) dsc.AppendLine("Barrier to liquid on side: North");
                    if (doorBh.GetLiquidBarrierHeightOnSide(BlockFacing.EAST, Pos, ref h) > 0) dsc.AppendLine("Barrier to liquid on side: East");
                    if (doorBh.GetLiquidBarrierHeightOnSide(BlockFacing.SOUTH, Pos, ref h) > 0) dsc.AppendLine("Barrier to liquid on side: South");
                    if (doorBh.GetLiquidBarrierHeightOnSide(BlockFacing.WEST, Pos, ref h) > 0) dsc.AppendLine("Barrier to liquid on side: West");
                }
            }
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            RotateYRad = tree.GetFloat("rotateYRad");
            RotateYRad = (RotateYRad - degreeRotation * GameMath.DEG2RAD) % GameMath.TWOPI;
            tree.SetFloat("rotateYRad", RotateYRad);
        }
    }
}
