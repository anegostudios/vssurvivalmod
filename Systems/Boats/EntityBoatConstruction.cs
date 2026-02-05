using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class EntityBoatConstruction : Entity
    {
        public override double FrustumSphereRadius => base.FrustumSphereRadius * 2;
        protected Vec3f launchStartPos = new Vec3f();
        protected RightClickConstruction rcc;

        int CurrentStage
        {
            get { return WatchedAttributes.GetInt("currentStage", 0); }
            set { WatchedAttributes.SetInt("currentStage", value); }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            requirePosesOnServer = true;
            WatchedAttributes.RegisterModifiedListener("currentStage", stagedChanged);
            WatchedAttributes.RegisterModifiedListener("wildcards", ()=>rcc.FromTreeAttributes(WatchedAttributes));
            base.Initialize(properties, api, InChunkIndex3d);

            var stages = properties.Attributes["stages"].AsArray<ConstructionStage>();
            rcc.LateInit(stages, Api, () => Pos.XYZ, "entity " + Code);
            this.CurrentStage = rcc.CurrentCompletedStage;
        }

        private void stagedChanged()
        {
            if (World.Side == EnumAppSide.Server) return;

            rcc.CurrentCompletedStage = CurrentStage;
            MarkShapeModified();
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            var esr = Properties.Client.Renderer as EntityShapeRenderer;
            if (esr != null)
            {
                esr.OverrideSelectiveElements = rcc.getShapeElements();
            }

            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (capi != null) {
                setTexture("debarked", new AssetLocation(string.Format("block/wood/debarked/{0}", rcc.StoredWildCards["wood"])));
                setTexture("planks", new AssetLocation(string.Format("block/wood/planks/{0}1", rcc.StoredWildCards["wood"])));
            }

            base.OnTesselation(ref entityShape, shapePathForLogging);
        }

        private void setTexture(string code, AssetLocation assetLocation)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            var ctex = Properties.Client.Textures[code] = new CompositeTexture(assetLocation);
            capi.EntityTextureAtlas.GetOrInsertTexture(ctex, out int tui, out _);
            ctex.Baked.TextureSubId = tui;
        }

        EntityAgent launchingEntity;

        public EntityBoatConstruction()
        {
            rcc = new RightClickConstruction();
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot handslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            base.OnInteract(byEntity, handslot, hitPosition, mode);

            if (this.CurrentStage != rcc.CurrentCompletedStage) this.CurrentStage = rcc.CurrentCompletedStage;
            if (CurrentStage >= rcc.Stages.Length - 1) return;

            if (CurrentStage == 0 && handslot.Empty && byEntity.Controls.ShiftKey)
            {
                byEntity.TryGiveItemStack(new ItemStack(Api.World.GetItem(new AssetLocation("roller")), 5));
                Die();
                return;
            }

            if (rcc.OnInteract(byEntity, handslot))
            {
                this.CurrentStage = rcc.CurrentCompletedStage;
                rcc.ToTreeAttributes(WatchedAttributes);
                WatchedAttributes.MarkPathDirty("wildcards");
                WatchedAttributes.MarkPathDirty("currentStage");
            }

            if (CurrentStage >= rcc.Stages.Length - 2 && !AnimManager.IsAnimationActive("launch"))
            {
                launchingEntity = byEntity;
                launchStartPos = getCenterPos();
                StartAnimation("launch");
            }
        }

        private Vec3f getCenterPos()
        {
            AttachmentPointAndPose apap = AnimManager.Animator?.GetAttachmentPointPose("Center");
            if (apap != null)
            {
                var mat = new Matrixf();
                mat.RotateY(Pos.Yaw + GameMath.PIHALF);
                apap.Mul(mat);
                return mat.TransformVector(new Vec4f(0, 0, 0, 1)).XYZ;
            }

            return null;
        }




        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            var prg = AnimManager.Animator?.GetAnimationState("launch").AnimProgress ?? 0;
            if (prg >= 0.99)
            {
                AnimManager.StopAnimation("launch");
                CurrentStage = 0;
                MarkShapeModified();
                if (World.Side == EnumAppSide.Server) Spawn();
            }
        }


        private void Spawn()
        {
            var nowOff = getCenterPos();
            Vec3f offset = nowOff == null ? new Vec3f() : nowOff - launchStartPos;

            EntityProperties type = World.GetEntityType(new AssetLocation("boat-sailed-" + rcc.StoredWildCards["wood"]));
            var entity = World.ClassRegistry.CreateEntity(type);

            if ((int)Math.Abs(Pos.Yaw * GameMath.RAD2DEG) == 90 || (int)Math.Abs(Pos.Yaw * GameMath.RAD2DEG) == 270) {
                offset.X *= 1.1f;
            }

            offset.Y = 0.5f;

            entity.Pos.Add(offset);
            entity.Pos.Motion.Add(offset.X / 50.0, 0, offset.Z / 50.0);

            var plr = (launchingEntity as EntityPlayer)?.Player;
            if (plr != null)
            {
                entity.WatchedAttributes.SetString("createdByPlayername", plr.PlayerName);
                entity.WatchedAttributes.SetString("createdByPlayerUID", plr.PlayerUID);
            }

            World.SpawnEntity(entity);
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            var wis = base.GetInteractionHelp(world, es, player);

            var ncwis = rcc.GetInteractionHelp(world, player);
            if (ncwis == null) return wis;
            wis = wis.Append(ncwis);

            if (CurrentStage == 0)
            {
                wis = wis.Append(new WorldInteraction()
                {
                    HotKeyCode = "sneak",
                    RequireFreeHand = true,
                    MouseButton = EnumMouseButton.Right,
                    ActionLangCode = "rollers-deconstruct"
                });
            }

            return wis;
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            rcc.ToTreeAttributes(WatchedAttributes);
            base.ToBytes(writer, forClient);
        }


        public override void FromBytes(BinaryReader reader, bool isSync)
        {
            base.FromBytes(reader, isSync);
            rcc.FromTreeAttributes(WatchedAttributes);
        }

        public override string GetInfoText()
        {
            return base.GetInfoText() + "\n" + Lang.Get("Material: {0}", Lang.Get("material-" + rcc.StoredWildCards["wood"]));
        }

    }
}
