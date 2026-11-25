using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BellowsLinkageProperties
    {
        public string AnimatedElement;
        public Vec3f Hinge;
        public Vec3f LowerAnchor;
        public Vec3f RearHandleAnchor;
        // How far a linkage eye may slide on its attachment, in shape units.
        public float AttachmentPlay;
        // Resistance per block of crank radius at the lower attachment.
        public float Resistance = 0.8f;
    }

    /// <summary>
    /// Plate rotation in degrees, sampled from the bellowing animation for both server travel and client playback.
    /// </summary>
    public class BellowsPlateCurve
    {
        public float[] AngleByFrame;
        public int PeakFrame;
        public float MinAngle;
        public float MaxAngle;

        public float AngleAt(float frame)
        {
            int first = (int)frame % AngleByFrame.Length;
            int second = (first + 1) % AngleByFrame.Length;
            return AngleByFrame[first] + (AngleByFrame[second] - AngleByFrame[first]) * (frame - (int)frame);
        }

        public float FindFrame(float angle, int fromFrame, int toFrame)
        {
            for (int frame = fromFrame; frame < toFrame; frame++)
            {
                float from = AngleByFrame[frame];
                float to = AngleByFrame[(frame + 1) % AngleByFrame.Length];
                if (angle < GameMath.Min(from, to) || angle > GameMath.Max(from, to)) continue;

                return from == to ? frame : frame + (angle - from) / (to - from);
            }

            return fromFrame;
        }

        public static BellowsPlateCurve FromAnimation(Animation animation, string element)
        {
            if (animation.KeyFrames == null || animation.QuantityFrames <= 0) return null;

            List<int> frames = [];
            List<float> angles = [];
            foreach (AnimationKeyFrame keyFrame in animation.KeyFrames)
            {
                if (keyFrame.Elements == null
                    || !keyFrame.Elements.TryGetValue(element, out AnimationKeyFrameElement keyFrameElement)
                    || keyFrameElement.RotationZ == null)
                {
                    continue;
                }

                frames.Add(keyFrame.Frame);
                angles.Add((float)keyFrameElement.RotationZ);
            }
            if (frames.Count < 2) return null;

            float[] angleByFrame = new float[animation.QuantityFrames];
            int peakFrame = 0;
            float minAngle = float.MaxValue;
            for (int frame = 0; frame < angleByFrame.Length; frame++)
            {
                angleByFrame[frame] = interpolate(frames, angles, frame, animation.QuantityFrames);
                if (angleByFrame[frame] > angleByFrame[peakFrame]) peakFrame = frame;
                minAngle = GameMath.Min(minAngle, angleByFrame[frame]);
            }

            return new BellowsPlateCurve {
                AngleByFrame = angleByFrame,
                PeakFrame = peakFrame,
                MinAngle = minAngle,
                MaxAngle = angleByFrame[peakFrame]
            };
        }

        // The animation loops, so the span after the last keyframe runs back into the first one.
        private static float interpolate(List<int> frames, List<float> angles, int frame, int frameCount)
        {
            if (frame <= frames[0]) return angles[0];

            for (int i = 0; i < frames.Count - 1; i++)
            {
                if (frame > frames[i + 1]) continue;

                return angles[i] + (angles[i + 1] - angles[i]) * (frame - frames[i]) / (frames[i + 1] - frames[i]);
            }

            int last = frames.Count - 1;
            return angles[last] + (angles[0] - angles[last]) * (frame - frames[last]) / (frameCount - frames[last] + frames[0]);
        }
    }

    public class BlockEntityMechPoweredBellows : BlockEntityBellows, IRenderer, ILinkageConsumer
    {
        public const int LowerLinkagePoint = 0;
        public const int RearHandleLinkagePoint = 1;

        const float animationSpeedMultiplier = 2f;
        const float minPoweredSpeed = 0.001f;
        const double linkageDistanceTolerance = 0.0000001;

        readonly Vec3d drivePin = new();
        readonly Vec3d driveRestPin = new();

        ICoreClientAPI capi;
        ModSystemLinkage linkageSystem = null!;
        BlockEntityAnimationUtil animUtil;
        MeshData ownMesh;
        public ItemStack PlacedStack { get; private set; }
        AnimationSound[] animationSounds;
        BlockFacing facing;
        BlockPos linkagePos;
        BlockPos airReceiverPos;
        BellowsPlateCurve plateCurve;
        string animatedElement;
        Vec3f hinge;
        Vec3f[] linkageAnchors = [];
        float[] linkageRadii = [];
        float[] linkageStrokes = [];
        float attachmentPlay;
        float linkageResistance;
        AnimatorBase jointAnimator;
        int animatedElementOffset = -1;
        float cosRotY;
        float sinRotY;
        bool rendererRegistered;
        bool animationRunning;
        float? lastPlateAngle;
        float? lastClientAngle;
        int animationDirection;

        public double RenderOrder => 0.1;
        public int RenderRange => 999;
        public BlockPos LinkagePosition => linkagePos ??= Pos.AddCopy((facing ??= BlockFacing.FromCode(Block.Variant["side"])).Opposite);
        public float LinkageAngleRad => getDrive()?.LinkageAngleRad ?? 0;
        public float LinkageStroke => getLinkageStroke(getLinkagePoint());

        public override void Initialize(ICoreAPI api)
        {
            linkageSystem = api.ModLoader.GetModSystem<ModSystemLinkage>()
                ?? throw new InvalidOperationException("The linkage mod system is not loaded.");
            base.Initialize(api);
            if (animation == null) throw new InvalidOperationException($"Missing bellowAnimation for {Block.Code}.");

            facing = BlockFacing.FromCode(Block.Variant["side"]);
            linkagePos = Pos.AddCopy(facing.Opposite);
            airReceiverPos = Pos.AddCopy(facing);
            initLinkageAnchors();
            plateCurve = loadPlateCurve();
            linkageSystem.ReconnectEndpoint(this);

            if (api is ICoreClientAPI clientApi)
            {
                capi = clientApi;
                animUtil = GetBehavior<BEBehaviorAnimatable>()?.animUtil;
                initMaterialMesh();
                if (animation.AnimationSounds != null)
                {
                    animationSounds = animation.AnimationSounds;
                    animation = animation.Clone();
                    animation.AnimationSounds = null;
                }
                capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "mechpoweredbellows");
                rendererRegistered = true;
            }
            // Both sides blow as a hand pumped bellows does
            RegisterGameTickListener(onTick, 50);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (byItemStack == null) return;

            PlacedStack = byItemStack.Clone();
            PlacedStack.StackSize = 1;
            initMaterialMesh();
            MarkDirty(true);
        }

        private void initMaterialMesh()
        {
            if (capi == null || animUtil == null) return;

            BlockMechanicalBellows block = (BlockMechanicalBellows)Block;
            Shape shape = Shape.TryGet(capi, block.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
            ownMesh = animUtil.InitializeAnimator("bellows" + block.Code, shape, block.GetMaterialTextureSource(capi, shape, block.Code + " bellows", PlacedStack), new Vec3f(0, block.Shape.rotateY, 0));
            animationRunning = false;
            lastClientAngle = null;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (base.OnTesselation(mesher, tesselator)) return true;
            if (ownMesh == null) return false;

            mesher.AddMeshData(ownMesh.Clone().Rotate(0, Block.Shape.rotateY * GameMath.DEG2RAD, 0));
            return true;
        }

        private void onTick(float _)
        {
            ILinkageProducer drive = getDrive();
            if (drive?.Network == null || Math.Abs(drive.LinkageSpeed) < minPoweredSpeed)
            {
                lastPlateAngle = null;
                return;
            }

            float plateAngle = getPlateAngle(drive, plateCurve, getAnimationAngle(drive) / GameMath.TWOPI * plateCurve.AngleByFrame.Length);
            float travel = plateCurve.MaxAngle - plateCurve.MinAngle;
            if (plateAngle < lastPlateAngle && travel > 0)
            {
                blowAir(airPerStroke * (lastPlateAngle.Value - plateAngle) / travel);
            }

            lastPlateAngle = plateAngle;
        }

        private void initLinkageAnchors()
        {
            BellowsLinkageProperties properties = Block.Attributes?["linkage"].AsObject<BellowsLinkageProperties>();
            if (properties?.LowerAnchor == null || properties.RearHandleAnchor == null || properties.Hinge == null
                || string.IsNullOrEmpty(properties.AnimatedElement))
            {
                throw new InvalidOperationException($"Missing or incomplete linkage properties for {Block.Code}.");
            }

            animatedElement = properties.AnimatedElement;
            hinge = properties.Hinge / 16f;
            attachmentPlay = properties.AttachmentPlay / 16f;
            linkageResistance = properties.Resistance;
            linkageAnchors = [properties.LowerAnchor / 16f, properties.RearHandleAnchor / 16f];
            linkageRadii = [hingeRadius(hinge, linkageAnchors[LowerLinkagePoint]), hingeRadius(hinge, linkageAnchors[RearHandleLinkagePoint])];

            float rearHandleRadius = linkageRadii[RearHandleLinkagePoint];
            linkageStrokes = [1f, rearHandleRadius == 0 ? 1f : linkageRadii[LowerLinkagePoint] / rearHandleRadius];

            // The animator draws the shape turned around the block centre, see BlockEntityBellows.OnTesselation()
            float rotY = Block.Shape.rotateY * GameMath.DEG2RAD;
            cosRotY = GameMath.Cos(rotY);
            sinRotY = GameMath.Sin(rotY);
        }

        // The bellowing animation only rotates the plate around Z, so the swept radius lies in the model's XY plane
        private static float hingeRadius(Vec3f hinge, Vec3f anchor)
        {
            float dx = anchor.X - hinge.X;
            float dy = anchor.Y - hinge.Y;
            return GameMath.Sqrt(dx * dx + dy * dy);
        }

        public bool IsValidLinkagePoint(int pointIndex)
        {
            return pointIndex >= 0 && pointIndex < linkageAnchors.Length;
        }

        // Both attachments sit in the body cell, which keeps a selection box over each of them.
        public int GetLinkagePointAt(BlockSelection blockSel)
        {
            return Api.World.BlockAccessor.GetBlock(blockSel.Position) is BlockMPMultiblockBellows body
                ? body.GetLinkagePoint(blockSel.SelectionBoxIndex)
                : -1;
        }

        public bool CanAttachLinkage(ItemLinkage linkage, int point)
        {
            return linkage.PinSize == 1;
        }

        public BlockFacing GetLinkageFacing(int point)
        {
            return (facing ??= BlockFacing.FromCode(Block.Variant["side"])).Axis == EnumAxis.X
                ? BlockFacing.NORTH
                : BlockFacing.WEST;
        }

        public void SetLinkagePin(Vec3d pin, float angle, int point)
        {
            Vec3f anchor = linkageAnchors[point];
            float x = anchor.X;
            float y = anchor.Y;
            float z = anchor.Z;
            float[] matrices = animUtil?.animator?.Matrices;
            if (matrices != null && tryGetAnimatedElementOffset(animUtil.animator, out int i))
            {
                x = matrices[i] * anchor.X + matrices[i + 4] * anchor.Y + matrices[i + 8] * anchor.Z + matrices[i + 12];
                y = matrices[i + 1] * anchor.X + matrices[i + 5] * anchor.Y + matrices[i + 9] * anchor.Z + matrices[i + 13];
                z = matrices[i + 2] * anchor.X + matrices[i + 6] * anchor.Y + matrices[i + 10] * anchor.Z + matrices[i + 14];
            }
            else if (getDrive() is ILinkageProducer drive
                && tryGetLinkagePlateAngle(drive, angle, point, out float plateAngle))
            {
                setRotatedLinkagePin(pin, anchor, plateAngle);
                return;
            }

            setModelPin(pin, x, y, z);
        }

        public void SetLinkagePinRestingPose(Vec3d pin, int point)
        {
            Vec3f anchor = linkageAnchors[point];
            setModelPin(pin, anchor.X, anchor.Y, anchor.Z);
        }

        public bool TrySetLinkagePinAtAngle(Vec3d pin, LinkageEndpointSelection drive, float driveAngle, float rodLength, int point)
        {
            if (!tryGetPlateAngle(drive, driveAngle, rodLength, point, out float plateAngle)) return false;

            setRotatedLinkagePin(pin, linkageAnchors[point], plateAngle);
            return true;
        }

        public bool CanFollowLinkage(LinkageEndpointSelection other, int point)
        {
            float radius = linkageRadii[point];
            if (radius <= 0) return false;

            float rodLength = linkageSystem.GetRodLength(new(this, point), other);

            float lowest = float.MaxValue;
            float highest = float.MinValue;
            for (int sample = 0; sample < LinkageGeometry.SweepSamples; sample++)
            {
                if (!tryGetPlateAngle(other, sample * GameMath.TWOPI / LinkageGeometry.SweepSamples, rodLength, point, out float angle)) return false;

                lowest = GameMath.Min(lowest, angle);
                highest = GameMath.Max(highest, angle);
            }

            float angularPlay = attachmentPlay / radius * GameMath.RAD2DEG;
            return lowest >= plateCurve.MinAngle - angularPlay
                && highest <= plateCurve.MaxAngle + angularPlay;
        }

        public virtual float? GetLinkageResistance(float crankRadius, int pointIndex)
        {
            return linkageResistance * getLinkageStroke(pointIndex) * crankRadius;
        }

        private void setRotatedLinkagePin(Vec3d pin, Vec3f anchor, float plateAngle)
        {
            double radians = GameMath.Clamp(plateAngle, plateCurve.MinAngle, plateCurve.MaxAngle) * GameMath.DEG2RAD;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double dx = anchor.X - hinge.X;
            double dy = anchor.Y - hinge.Y;
            double x = hinge.X + dx * cos - dy * sin;
            double y = hinge.Y + dx * sin + dy * cos;
            setModelPin(pin, x, y, anchor.Z);
        }

        // Turns a point of the drawn shape into a world position, matching how AnimatableRenderer places the model.
        private void setModelPin(Vec3d pin, double x, double y, double z)
        {
            double dx = x - 0.5;
            double dz = z - 0.5;
            pin.SetWithDimension(Pos).Add(
                0.5 + dx * cosRotY + dz * sinRotY,
                y,
                0.5 - dx * sinRotY + dz * cosRotY
            );
        }

        private bool tryGetAnimatedElementOffset(AnimatorBase animator, out int offset)
        {
            if (!ReferenceEquals(animator, jointAnimator))
            {
                jointAnimator = animator;
                ElementPose pose = animator.GetPosebyName(animatedElement);
                animatedElementOffset = pose == null ? -1 : pose.ForElement.JointId * 16;
                if (animatedElementOffset + 16 > animator.Matrices.Length) animatedElementOffset = -1;
            }

            offset = animatedElementOffset;
            return offset >= 0;
        }

        private ILinkageProducer getDrive()
        {
            return linkageSystem.GetConnectedEndpoint(this) as ILinkageProducer;
        }

        private float getAnimationAngle(ILinkageProducer drive)
        {
            return GameMath.Mod(drive.LinkageAngleRad + drive.GetLinkageCrankPhase(linkageSystem.GetLinkagePoint(drive)), GameMath.TWOPI);
        }

        private int getLinkagePoint()
        {
            return linkageSystem.GetLinkagePoint(this);
        }

        private float getLinkageStroke(int point)
        {
            return point >= 0 && point < linkageStrokes.Length ? linkageStrokes[point] : 1f;
        }

        private float getAnimationFrame(ILinkageProducer drive, float driveAngle)
        {
            float frame = driveAngle / GameMath.TWOPI * plateCurve.AngleByFrame.Length;
            float angle = getPlateAngle(drive, plateCurve, frame);
            return frame <= plateCurve.PeakFrame
                ? plateCurve.FindFrame(angle, 0, plateCurve.PeakFrame)
                : plateCurve.FindFrame(angle, plateCurve.PeakFrame, plateCurve.AngleByFrame.Length);
        }

        private float getPlateAngle(ILinkageProducer drive, BellowsPlateCurve curve, float frame)
        {
            // Scaling the curve by the stroke is the approximation to fall back on while the linkage cannot be measured.
            float angle = tryGetLinkagePlateAngle(drive, drive.LinkageAngleRad, getLinkagePoint(), out float solvedAngle)
                ? solvedAngle
                : LinkageStroke * curve.AngleAt(frame);
            return GameMath.Clamp(angle, curve.MinAngle, curve.MaxAngle);
        }

        private bool tryGetLinkagePlateAngle(ILinkageProducer drive, float driveAngle, int point, out float angle)
        {
            angle = 0;
            float rodLength = linkageSystem.GetRodLength(this);
            return rodLength > 0
                && tryGetPlateAngle(new(drive, linkageSystem.GetLinkagePoint(drive)), driveAngle, rodLength, point, out angle);
        }

        // Solves how far the plate has swung, from the crank pin and the rigid rod that reaches it.
        private bool tryGetPlateAngle(LinkageEndpointSelection drive, float driveAngle, float rodLength, int point, out float angle)
        {
            angle = 0;
            Vec3f anchor = linkageAnchors[point];
            double anchorX = anchor.X - hinge.X;
            double anchorY = anchor.Y - hinge.Y;
            double radius = Math.Sqrt(anchorX * anchorX + anchorY * anchorY);

            drive.Endpoint.SetLinkagePin(drivePin, driveAngle, drive.Point);
            toModelSpace(drivePin);
            double pinX = drivePin.X - hinge.X;
            double pinY = drivePin.Y - hinge.Y;
            double pinDistance = Math.Sqrt(pinX * pinX + pinY * pinY);

            double axialGap = anchor.Z - drivePin.Z;
            double planeLengthSquared = rodLength * rodLength - axialGap * axialGap;
            if (planeLengthSquared <= 0 || radius <= 0 || pinDistance <= 0) return false;

            double cosSweep = (radius * radius + pinDistance * pinDistance - planeLengthSquared) / (2 * radius * pinDistance);
            if (cosSweep < -1 - linkageDistanceTolerance || cosSweep > 1 + linkageDistanceTolerance) return false;
            cosSweep = GameMath.Clamp(cosSweep, -1, 1);

            // Of the two ways the circles meet, keep the side the mechanism was assembled on.
            drive.Endpoint.SetLinkagePinRestingPose(driveRestPin, drive.Point);
            toModelSpace(driveRestPin);
            double restSide = (driveRestPin.X - hinge.X) * anchorY - (driveRestPin.Y - hinge.Y) * anchorX;
            double sweep = restSide < 0 ? -Math.Acos(cosSweep) : Math.Acos(cosSweep);

            double swung = Math.Atan2(pinY, pinX) + sweep - Math.Atan2(anchorY, anchorX);
            angle = (float)(GameMath.Mod(swung + Math.PI, GameMath.TWOPI) - Math.PI) * GameMath.RAD2DEG;
            return true;
        }

        // Reverses setModelPin(), turning a world position back into a point of the drawn shape.
        private void toModelSpace(Vec3d pin)
        {
            double x = pin.X - Pos.X - 0.5;
            double z = pin.Z - Pos.Z - 0.5;
            pin.Set(x * cosRotY - z * sinRotY + 0.5, pin.Y - Pos.InternalY, x * sinRotY + z * cosRotY + 0.5);
        }

        // Read from the shape rather than the animator, because the server has to hold placement to the same travel.
        private BellowsPlateCurve loadPlateCurve()
        {
            return ObjectCacheUtil.GetOrCreate(Api, "bellowsPlateCurve-" + Block.Code, () =>
            {
                Shape shape = Shape.TryGet(Api, Block.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                if (shape?.Animations == null || shape.GetElementByName(animatedElement) == null)
                {
                    throw new InvalidOperationException($"Missing animated shape or element '{animatedElement}' for {Block.Code}.");
                }

                foreach (Animation shapeAnimation in shape.Animations)
                {
                    if (shapeAnimation.Code != animation.Code) continue;

                    return BellowsPlateCurve.FromAnimation(shapeAnimation, animatedElement)
                        ?? throw new InvalidOperationException($"Missing plate rotation keyframes for '{animatedElement}' in animation '{animation.Code}' for {Block.Code}.");
                }
                throw new InvalidOperationException($"Missing animation '{animation.Code}' for {Block.Code}.");
            });
        }

        private static bool crossesPhase(float startAngle, float angleDelta, float phase)
        {
            if (angleDelta == 0) return false;

            bool forward = angleDelta > 0;
            float distance = GameMath.Mod(forward ? phase - startAngle : startAngle - phase, GameMath.TWOPI);
            return distance > 0 && distance <= Math.Abs(angleDelta);
        }

        private void blowAir(float amount)
        {
            Block receiverBlock = Api.World.BlockAccessor.GetBlock(airReceiverPos);
            receiverBlock.GetInterface<IBellowsAirReceiver>(Api.World, airReceiverPos)?.BlowAirInto(Api.World, airReceiverPos, amount, facing);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            ILinkageProducer drive = getDrive();
            if (drive == null)
            {
                stopAnimation();
                return;
            }

            float driveAngle = getAnimationAngle(drive);
            updateClientPhase(drive, driveAngle);
            float frame = getAnimationFrame(drive, driveAngle);
            if (!animationRunning)
            {
                if (animUtil == null) return;

                animation.AnimationSpeed = 0;
                animation.StartFrameOnce = frame;
                animUtil.StartAnimation(animation);
                animationRunning = true;
            }

            RunningAnimation animationState = animUtil.animator?.GetAnimationState(animation.Code);
            if (animationState == null) return;

            animation.AnimationSpeed = drive.Network == null ? 0 : Math.Abs(drive.LinkageSpeed) * animationSpeedMultiplier * animationDirection;
            animationState.CurrentFrame = frame;
        }

        private void updateClientPhase(ILinkageProducer drive, float driveAngle)
        {
            if (drive.Network == null || Math.Abs(drive.LinkageSpeed) < minPoweredSpeed)
            {
                lastClientAngle = null;
                animationDirection = 0;
                return;
            }

            if (lastClientAngle.HasValue)
            {
                float angleDelta = GameMath.AngleRadDistance(lastClientAngle.Value, driveAngle);
                if (angleDelta != 0)
                {
                    animationDirection = angleDelta < 0 ? -1 : 1;
                    playCrossedSounds(lastClientAngle.Value, angleDelta);
                }
            }

            lastClientAngle = driveAngle;
        }

        private void playCrossedSounds(float startAngle, float angleDelta)
        {
            if (animationSounds == null || animUtil == null) return;

            float radiansPerFrame = GameMath.TWOPI / plateCurve.AngleByFrame.Length;
            foreach (AnimationSound sound in animationSounds)
            {
                float soundPhase = sound.Frame * radiansPerFrame;
                if (crossesPhase(startAngle, angleDelta, soundPhase)) animUtil.ShouldPlaySound(animation.Code, sound);
            }
        }

        private void stopAnimation()
        {
            lastClientAngle = null;
            animationDirection = 0;
            if (!animationRunning) return;

            animation.AnimationSpeed = 0;
            animUtil?.StopAnimation(animation.Code);
            animationRunning = false;
        }

        public override bool Interact(IPlayer byPlayer)
        {
            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            BlockMechanicalBellows block = (BlockMechanicalBellows)Block;
            string previousMaterial = block.MaterialProperties?.GetMaterialKey(PlacedStack);
            PlacedStack = tree.GetItemstack("placedStack");
            PlacedStack?.ResolveBlockOrItem(worldAccessForResolve);
            if (capi != null && previousMaterial != block.MaterialProperties?.GetMaterialKey(PlacedStack))
            {
                initMaterialMesh();
                MarkDirty(true);
            }
            if (Api == null) return;

            linkageSystem.ReconnectEndpoint(this);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (PlacedStack == null) tree.RemoveAttribute("placedStack");
            else tree.SetItemstack("placedStack", PlacedStack);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            if (PlacedStack == null) return;
            if (!PlacedStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings))
            {
                PlacedStack = null;
                return;
            }
            PlacedStack.Collectible.OnLoadCollectibleMappings(worldForNewMappings, new DummySlot(PlacedStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            PlacedStack?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(PlacedStack), blockIdMapping, itemIdMapping);
        }

        public override void OnBlockUnloaded()
        {
            linkageSystem.UnregisterEndpoint(LinkagePosition);
            Dispose();
            base.OnBlockUnloaded();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            linkageSystem.DetachForBreak(this, byPlayer);
            Dispose();
            base.OnBlockBroken(byPlayer);
        }

        public override void OnBlockRemoved()
        {
            linkageSystem.DetachForBreak(this, null);
            linkageSystem.UnregisterEndpoint(LinkagePosition);
            Dispose();
            base.OnBlockRemoved();
        }

        public void Dispose()
        {
            stopAnimation();
            if (!rendererRegistered) return;

            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Before);
            rendererRegistered = false;
        }
    }
}
