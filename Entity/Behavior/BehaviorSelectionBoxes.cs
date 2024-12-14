using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace Vintagestory.GameContent
{

    public class EntityBehaviorSelectionBoxes : EntityBehavior, IRenderer
    {
        ICoreClientAPI capi;
        Matrixf mvmat = new Matrixf();
        bool debug = false;
        bool rendererRegistered=false;

        public AttachmentPointAndPose[] selectionBoxes = new AttachmentPointAndPose[0];
        string[] selectionBoxCodes;

        public EntityBehaviorSelectionBoxes(Entity entity) : base(entity) { }
        public double RenderOrder => 1;
        public int RenderRange => 24;
        public void Dispose() { }

        public WireframeCube BoxWireframe;


        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            capi = entity.Api as ICoreClientAPI;
            if (capi != null)
            {
                debug = capi.Settings.Bool["debugEntitySelectionBoxes"];
            }
            
            setupWireframe();
            entity.trickleDownRayIntersects = true;
            entity.requirePosesOnServer = true;

            selectionBoxCodes = attributes["selectionBoxes"].AsStringArray(new string[0]);
            if (selectionBoxCodes.Length == 0)
            {
                capi.World.Logger.Warning("EntityBehaviorSelectionBoxes, missing selectionBoxes property. Will ignore.");
            }
        }

        public override void OnTesselated()
        {
            loadSelectionBoxes();
        }

        private void loadSelectionBoxes()
        {
            List<AttachmentPointAndPose> list = new List<AttachmentPointAndPose>();

            foreach (var code in selectionBoxCodes)
            {
                var apap = entity.AnimManager?.Animator?.GetAttachmentPointPose(code);
                if (apap == null)
                {
                    //capi.World.Logger.Warning("EntityBehaviorSelectionBoxes, selection box with code " + code + " defined, but the shape file does not contain such attachment point. Will ignore.");
                    continue;
                }

                var dapap = new AttachmentPointAndPose()
                {
                    AnimModelMatrix = apap.AnimModelMatrix,
                    AttachPoint = apap.AttachPoint,
                    CachedPose = apap.CachedPose
                };

                list.Add(dapap);
            }

            selectionBoxes = list.ToArray();
        }

        float accum = 0;
        public override void OnGameTick(float deltaTime)
        {
            if (capi != null && (accum += deltaTime) >= 1)
            {
                accum = 0;
                debug = capi.Settings.Bool["debugEntitySelectionBoxes"];
                setupWireframe();
            }
            base.OnGameTick(deltaTime);
        }

        private void setupWireframe()
        {
            if (!rendererRegistered)
            {
                if (capi != null)
                {
                    capi.Event.RegisterRenderer(this, EnumRenderStage.AfterFinalComposition, "selectionboxesbhdebug");
                    BoxWireframe = WireframeCube.CreateUnitCube(capi, ColorUtil.WhiteArgb);
                }
                rendererRegistered = true;
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi.HideGuis) return;

            int hitindex = getHitIndex();
            if (hitindex < 0 && capi.World.Player.CurrentEntitySelection?.Entity != this.entity) return;
            var eplr = capi.World.Player.Entity;

            if (debug)
            {
                for (int i = 0; i < selectionBoxes.Length; i++)
                {
                    if (hitindex != i) Render(eplr, i, ColorUtil.WhiteArgbVec);
                }

                // Render selected cube last to be on top
                if (hitindex >= 0)
                {
                    Render(eplr, hitindex, new Vec4f(1, 0, 0, 1));
                }
            } else
            {
                if (hitindex >= 0)
                {
                    Render(eplr, hitindex, new Vec4f(0,0,0,0.5f));
                }
            }
        }

        private void Render(EntityPlayer eplr, int i, Vec4f color)
        {
            var apap = selectionBoxes[i];
            var pos = entity.Pos;

            mvmat.Identity();
            mvmat.Set(capi.Render.CameraMatrixOrigin);

            IMountable ims;
            ims = entity.GetInterface<IMountable>();
            IMountableSeat seat;
            if (ims != null && (seat = ims.GetSeatOfMountedEntity(eplr)) != null)
            {
                var offset = seat.SeatPosition.XYZ - seat.MountSupplier.Position.XYZ;
                mvmat.Translate(-(float)offset.X, -(float)offset.Y, -(float)offset.Z);
            }
            else
            {
                mvmat.Translate(pos.X - eplr.CameraPos.X, pos.InternalY - eplr.CameraPos.Y, pos.Z - eplr.CameraPos.Z);
            }

            applyBoxTransform(mvmat, apap);

            BoxWireframe.Render(capi, mvmat, 1.6f, color);
        }

        private int getHitIndex()
        {
            var eplr = capi.World.Player.Entity;
            // Lets pretend our 0/0/0 is at the center point of our creatures feet
            // This means our picking ray starts at playerPos + eyeHeight - creaturePos
            var pickingray = Ray.FromAngles(eplr.SidedPos.XYZ + eplr.LocalEyePos - entity.SidedPos.XYZ, eplr.SidedPos.Pitch, eplr.SidedPos.Yaw, capi.World.Player.WorldData.PickingRange);
            return getHitIndex(pickingray);
        }

        private void applyBoxTransform(Matrixf mvmat, AttachmentPointAndPose apap)
        {
            var esr = entity.Properties.Client.Renderer as EntityShapeRenderer;

            mvmat.RotateY(GameMath.PIHALF + entity.SidedPos.Yaw);

            if (esr != null)
            {
                mvmat.Translate(0, entity.SelectionBox.Y2 / 2, 0);
                mvmat.RotateX(esr.xangle);
                mvmat.RotateY(esr.yangle);
                mvmat.RotateZ(esr.zangle);
                mvmat.Translate(0, -entity.SelectionBox.Y2 / 2, 0f);
            }


            mvmat.Translate(0, 0.7f, 0);
            mvmat.RotateX(esr?.nowSwivelRad ?? 0);
            mvmat.Translate(0, -0.7f, 0);

            float s = entity.Properties.Client.Size;
            mvmat.Scale(s, s, s);

            mvmat.Translate(-0.5f, 0, -0.5f);  // Center the box around entity pos (entity pos = center position of the model)

            mvmat.Mul(apap.AnimModelMatrix);

            var selem = apap.AttachPoint.ParentElement;
            float sizex = (float)(selem.To[0] - selem.From[0]) / 16f;
            float sizey = (float)(selem.To[1] - selem.From[1]) / 16f;
            float sizez = (float)(selem.To[2] - selem.From[2]) / 16f;
            mvmat.Scale(sizex, sizey, sizez);
        }

        public override bool IntersectsRay(Ray ray, AABBIntersectionTest intersectionTester, out double intersectionDistance, ref int selectionBoxIndex, ref EnumHandling handled)
        {
            var pickingray = new Ray(ray.origin - entity.SidedPos.XYZ, ray.dir);

            int index = getHitIndex(pickingray);
            if (index >= 0)
            {
                intersectionDistance = hitPositionAABBSpace.Length();
                intersectionTester.hitPosition = hitPositionAABBSpace.AddCopy(entity.SidedPos.XYZ);
                selectionBoxIndex = 1 + index;
                handled = EnumHandling.PreventDefault;

                return true;
            }

            intersectionDistance = double.MaxValue;
            return false;
        }


        static Cuboidd standardbox = new Cuboidd(0, 0, 0, 1, 1, 1);
        private int getHitIndex(Ray pickingray)
        {
            int foundIndex = -1;
            double foundDistance = double.MaxValue;

            for (int i = 0; i < selectionBoxes.Length; i++)
            {
                var apap = selectionBoxes[i];

                mvmat.Identity();
                applyBoxTransform(mvmat, apap);
                var mvMatInv = mvmat.Clone().Invert();

                var obbSpaceOrigin = mvMatInv.TransformVector(new Vec4d(pickingray.origin.X, pickingray.origin.Y, pickingray.origin.Z, 1));
                var obbSpaceDirection = mvMatInv.TransformVector(new Vec4d(pickingray.dir.X, pickingray.dir.Y, pickingray.dir.Z, 0));
                Ray obbSpaceRay = new Ray(obbSpaceOrigin.XYZ, obbSpaceDirection.XYZ);

                if (Testintersection(standardbox, obbSpaceRay))
                {
                    var tf = mvmat.TransformVector(new Vec4d(hitPositionOBBSpace.X, hitPositionOBBSpace.Y, hitPositionOBBSpace.Z, 1));

                    double dist = (tf.XYZ - pickingray.origin).LengthSq();
                    if (foundIndex >= 0 && foundDistance < dist) continue;

                    hitPositionAABBSpace = tf.XYZ;
                    foundDistance = dist;
                    foundIndex = i;
                }
            }

            return foundIndex;
        }

        Vec3d hitPositionOBBSpace;
        Vec3d hitPositionAABBSpace;

        public bool Testintersection(Cuboidd b, Ray r)
        {
            double w = b.X2 - b.X1;
            double h = b.Y2 - b.Y1;
            double l = b.Z2 - b.Z1;

            for (int i = 0; i < BlockFacing.NumberOfFaces; i++)
            {
                BlockFacing blockSideFacing = BlockFacing.ALLFACES[i];
                Vec3i planeNormal = blockSideFacing.Normali;

                // Dot product of 2 vectors
                // If they are parallel the dot product is 1
                // At 90 degrees the dot product is 0
                double demon = planeNormal.X * r.dir.X + planeNormal.Y * r.dir.Y + planeNormal.Z * r.dir.Z;

                // Does intersect this plane somewhere (only negative because we are not interested in the ray leaving a face, negative because the ray points into the cube, the plane normal points away from the cube)
                if (demon < -0.00001)
                {
                    Vec3d planeCenterPosition = blockSideFacing.PlaneCenter
                        .ToVec3d()
                        .Mul(w, h, l)
                        .Add(b.X1, b.Y1, b.Z1)
                    ;

                    Vec3d pt = Vec3d.Sub(planeCenterPosition, r.origin);
                    double t = (pt.X * planeNormal.X + pt.Y * planeNormal.Y + pt.Z * planeNormal.Z) / demon;

                    if (t >= 0)
                    {
                        hitPositionOBBSpace = new Vec3d(r.origin.X + r.dir.X * t, r.origin.Y + r.dir.Y * t, r.origin.Z + r.dir.Z * t);
                        var lastExitedBlockFacePos = Vec3d.Sub(hitPositionOBBSpace, planeCenterPosition);

                        // Does intersect this plane within the block
                        if (Math.Abs(lastExitedBlockFacePos.X) <= w / 2 && Math.Abs(lastExitedBlockFacePos.Y) <= h / 2 && Math.Abs(lastExitedBlockFacePos.Z) <= l / 2)
                        {
                            //hitOnBlockFace = blockSideFacing;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            int hitindex = getHitIndex();
            if (hitindex >= 0)
            {
                if (capi.Settings.Bool["extendedDebugInfo"]) infotext.AppendLine("<font color=\"#bbbbbb\">looking at AP " + selectionBoxes[hitindex].AttachPoint.Code + "</font>");
                infotext.AppendLine(API.Config.Lang.GetMatching("creature-" + entity.Code.ToShortString() + "-selectionbox-" + selectionBoxes[hitindex].AttachPoint.Code));
            }

            base.GetInfoText(infotext);
        }


        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            capi?.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
            BoxWireframe?.Dispose();
        }


        public override string PropertyName() => "selectionboxes";
    }

}
