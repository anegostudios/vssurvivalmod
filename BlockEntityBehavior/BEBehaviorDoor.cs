using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BEBehaviorDoor : BEBehaviorAnimatable, IInteractable, IRotatable
    {
        public float RotateYRad;
        protected bool opened;
        protected bool invertHandles;
        protected MeshData mesh;
        protected Cuboidf[] boxesClosed, boxesOpened;

        public BlockFacing facingWhenClosed { get { return BlockFacing.HorizontalFromYaw(RotateYRad); } }
        public BlockFacing facingWhenOpened { get { return invertHandles? facingWhenClosed.GetCCW() : facingWhenClosed.GetCW(); } }

        /// <summary>
        /// A rather counter-intuitive property, setting this actually sets up an internal Vec3i giving the offset to the Pos of the supplied door
        /// </summary>
        public BEBehaviorDoor LeftDoor
        {
            get 
            {
                if (leftDoorOffset != null)
                {
                    var door = BlockBehaviorDoor.getDoorAt(Api.World, Pos.AddCopy(leftDoorOffset));
                    if (door == null) leftDoorOffset = null;
                    
                    return door;
                }

                return null;
            }
            protected set { leftDoorOffset = value == null ? null : value.Pos.SubCopy(Pos).ToVec3i(); }
        }
        /// <summary>
        /// A rather counter-intuitive property, setting this actually sets up an internal Vec3i giving the offset to the Pos of the supplied door
        /// </summary>
        public BEBehaviorDoor RightDoor
        {
            get
            {
                if (rightDoorOffset != null)
                {
                    var door = BlockBehaviorDoor.getDoorAt(Api.World, Pos.AddCopy(rightDoorOffset));
                    if (door == null) rightDoorOffset = null;
                    
                    return door;
                }

                return null;
            }
            protected set { rightDoorOffset = value == null ? null : value.Pos.SubCopy(Pos).ToVec3i(); }
        }

        protected Vec3i leftDoorOffset;
        protected Vec3i rightDoorOffset;

        public BlockBehaviorDoor doorBh;

        public Cuboidf[] ColSelBoxes => opened ? boxesOpened : boxesClosed;
        public bool Opened => opened;
        public bool InvertHandles => invertHandles;
        public string StoryLockedCode;

        public BEBehaviorDoor(BlockEntity blockentity) : base(blockentity)
        {
            boxesClosed = Block.CollisionBoxes;

            doorBh = Block.GetBehavior<BlockBehaviorDoor>();
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
            return Pos.AddCopy(getAdjacentOffset(right, back, up, RotateYRad, invertHandles));
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
            if (initalSetup)
            {

                if (BlockBehaviorDoor.HasCombinableLeftDoor(Api.World, RotateYRad, Pos, doorBh.width, out BEBehaviorDoor otherDoor, out int offset))
                {
                    if (otherDoor.LeftDoor == null && otherDoor.RightDoor == null && otherDoor.facingWhenClosed == facingWhenClosed)
                    {
                        if (otherDoor.invertHandles)
                        {
                            if (otherDoor.doorBh.width > 1)
                            {
                                Api.World.BlockAccessor.SetBlock(0, otherDoor.Pos);
                                BlockPos leftDoorPos = Pos.AddCopy(facingWhenClosed.GetCW(), (otherDoor.doorBh.width + doorBh.width - 1));
                                Api.World.BlockAccessor.SetBlock(otherDoor.Block.Id, leftDoorPos);
                                otherDoor = Block.GetBEBehavior<BEBehaviorDoor>(leftDoorPos);
                                otherDoor.RotateYRad = RotateYRad;
                                otherDoor.doorBh.placeMultiblockParts(Api.World, leftDoorPos);
                                LeftDoor = otherDoor;
                                LeftDoor.RightDoor = this;
                                LeftDoor.SetupRotationsAndColSelBoxes(true);
                            }
                            else
                            {
                                otherDoor.invertHandles = false;
                                LeftDoor = otherDoor;
                                LeftDoor.RightDoor = this;
                                LeftDoor.Blockentity.MarkDirty(true);
                                LeftDoor.SetupRotationsAndColSelBoxes(false);
                            }
                        }
                        else
                        {
                            LeftDoor = otherDoor;
                            LeftDoor.RightDoor = this;
                        }

                        invertHandles = true;
                        Blockentity.MarkDirty(true);
                    }
                }

                if (BlockBehaviorDoor.HasCombinableRightDoor(Api.World, RotateYRad, Pos, doorBh.width, out otherDoor, out offset))
                {
                    if (otherDoor.LeftDoor == null && otherDoor.RightDoor == null && otherDoor.facingWhenClosed == facingWhenClosed)
                    {
                        if (Api.Side == EnumAppSide.Server)
                        {
                            if (!otherDoor.invertHandles)
                            {
                                if (otherDoor.doorBh.width > 1)
                                {
                                    Api.World.BlockAccessor.SetBlock(0, otherDoor.Pos);
                                    BlockPos rightDoorPos = Pos.AddCopy(facingWhenClosed.GetCCW(), (otherDoor.doorBh.width + doorBh.width - 1));
                                    Api.World.BlockAccessor.SetBlock(otherDoor.Block.Id, rightDoorPos);
                                    otherDoor = Block.GetBEBehavior<BEBehaviorDoor>(rightDoorPos);
                                    otherDoor.RotateYRad = RotateYRad;
                                    otherDoor.invertHandles = true;
                                    otherDoor.doorBh.placeMultiblockParts(Api.World, rightDoorPos);
                                    RightDoor = otherDoor;
                                    RightDoor.LeftDoor = this;
                                    otherDoor.SetupRotationsAndColSelBoxes(true);
                                }
                                else
                                {
                                    otherDoor.invertHandles = true;
                                    RightDoor = otherDoor;
                                    RightDoor.LeftDoor = this;
                                    RightDoor.Blockentity.MarkDirty(true);
                                    RightDoor.SetupRotationsAndColSelBoxes(false);
                                }
                            }
                            else
                            {
                                RightDoor = otherDoor;
                                RightDoor.LeftDoor = this;
                            }
                        }
                    }
                }
            }

            if (Api.Side == EnumAppSide.Client)
            {
                if (doorBh.animatableOrigMesh == null)
                {
                    string animkey = "door-" + Block.Variant["style"];
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
                boxesClosed = Block.CollisionBoxes;
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
            float breakChance = Block.Attributes["breakOnTriggerChance"].AsFloat(0);
            if (Api.Side == EnumAppSide.Server && Api.World.Rand.NextDouble() < breakChance && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                Api.World.BlockAccessor.BreakBlock(Pos, byPlayer);
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), Pos, 0, null);
                return;
            }

            this.opened = opened;
            ToggleDoorWing(opened);

            float pitch = opened ? 1.1f : 0.9f;

            var sound = opened ? doorBh?.OpenSound : doorBh?.CloseSound;

            Api.World.PlaySoundAt(sound, Pos.X + 0.5f, Pos.InternalY + 0.5f, Pos.Z + 0.5f, byPlayer, EnumSoundType.Sound, pitch);

            if (LeftDoor != null && invertHandles)
            {
                LeftDoor.ToggleDoorWing(opened);
                LeftDoor.UpdateNeighbors();
            }
            else if (RightDoor != null)
            {
                RightDoor.ToggleDoorWing(opened);
                RightDoor.UpdateNeighbors();
            }

            Blockentity.MarkDirty(true);

            UpdateNeighbors();
        }

        private void UpdateNeighbors()
        {
            if (Api.Side == EnumAppSide.Server)
            {
                BlockPos tempPos = new BlockPos(Pos.dimension);
                for (int y = 0; y < doorBh.height; y++)
                {
                    tempPos.Set(Pos).Add(0, y, 0);
                    BlockFacing sideMove = BlockFacing.ALLFACES[Opened ? facingWhenClosed.HorizontalAngleIndex : facingWhenOpened.HorizontalAngleIndex];

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
                float easingSpeed = Block.Attributes?["easingSpeed"].AsFloat(10) ?? 10;
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
            StoryLockedCode = tree.GetString("storyLockedCode");

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
            if (StoryLockedCode != null)
            {
                tree.SetString("storyLockedCode", StoryLockedCode);
            }
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
