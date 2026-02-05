using OpenTK.Graphics.ES11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityGrindingWheel : BlockEntity, IMechanicalPowerRenderable
    {
        public BlockFacing Facing => BlockFacing.FromCode(Block.Variant["side"]);
        public virtual BlockPos Position { get { return Pos; } }
        public virtual Vec4f LightRgba { get; set; } = new Vec4f();
        public virtual int[] AxisSign { get; set; }
        public virtual float AngleRad => GetBehavior<BEBehaviorMPConsumer>().AngleRad;

        protected CompositeShape shape = null;
        protected MechanicalPowerMod manager;
        protected ICoreClientAPI capi;
        protected ILoadedSound grindSound;
        protected ILoadedSound turnSound;

        public Dictionary<string, float> PlayersGrinding = new Dictionary<string, float>();
        public bool Grinding;

        

        public virtual CompositeShape Shape
        {
            get { return shape; }
            set
            {
                CompositeShape prev = Shape;
                if (prev != null && manager != null)
                {
                    manager.RemoveDeviceForRender(this);
                    this.shape = value;
                    manager.AddDeviceForRender(this);
                }
                else
                {
                    this.shape = value;
                }
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.Shape = this.Block.Shape;
            manager = Api.ModLoader.GetModSystem<MechanicalPowerMod>();
            manager.AddDeviceForRender(this);

            AxisSign = new int[3] { 0, 0, 0 };
            switch (Facing.Index)
            {
                case 0:
                    AxisSign[0] = -1;
                    break;
                case 1:
                    AxisSign[2] = -1;
                    break;
                case 2:
                    AxisSign[0] = -1;
                    break;
                default:
                    AxisSign[2] = -1;
                    break;
            }

            capi = api as ICoreClientAPI;

            RegisterGameTickListener(checkIsGrinding, 100);
        }

        long sparkTickListener;
        float turnSpeed => GetBehavior<BEBehaviorMPConsumer>().TrueSpeed;

        private void checkIsGrinding(float dt)
        {
            if (Api.Side == EnumAppSide.Server)
            {
                if (Grinding && PlayersGrinding.Count == 0)
                {
                    Grinding = false;
                    MarkDirty(true);
                }
                if (!Grinding && PlayersGrinding.Count > 0)
                {
                    Grinding = true;
                    MarkDirty(true);
                }

                if (Api.World.Rand.NextDouble() > 0.05 * turnSpeed) return;

                foreach (var uid in new List<string>(PlayersGrinding.Keys))
                {
                    var plr = Api.World.PlayerByUid(uid);
                    var slot = plr.InventoryManager.ActiveHotbarSlot;
                    float maxdura = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);
                    if (slot != null && slot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorSharpenable>())
                    {
                        slot.Itemstack.Collectible.DamageItem(Api.World, plr.Entity, slot, (int)Math.Max(1, 0.003f * maxdura));
                        
                    }
                    PlayersGrinding[uid] += dt;
                    
                    var bhb = slot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorBuffable>();
                    var curBuff = bhb.GetItemBuffs(slot.Itemstack).FirstOrDefault((buff) => buff.Code == "sharpened");
                    if (curBuff == null || curBuff.Multiplier < 1.099)
                    {
                        bhb.AddBuff(slot.Itemstack, new AppliedCollectibleBuff()
                        {
                            Code = "sharpened",
                            RemainingDurability = curBuff == null ? (int)(maxdura / 4) : 0,
                            Multiplier = 1.01f,
                            StatCode = "critchance"
                        }, true);
                    }
                    
                }

                return;
            }


            if (turnSound == null && turnSpeed > 0.05)
            {
                turnSound = capi.World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/block/grindingwheel.ogg"),
                    ShouldLoop = true,
                    DisposeOnFinish = false,
                    Position = Pos.ToVec3f(),
                    RelativePosition = false,
                    Range = 16,
                    SoundType = EnumSoundType.Sound,
                    Volume = 1
                });

                turnSound.Start();
            }

            if (Math.Abs(turnSpeed) > 0.05)
            {
                if (!turnSound.IsPlaying) turnSound.Start();
                turnSound.SetPitch(Math.Abs(turnSpeed));
            } else
            {
                if (turnSound != null && turnSound.IsPlaying) turnSound.Stop();
            }

            if (grindSound == null) return;

            if (Grinding)
            {
                grindSound.SetPitch(Math.Abs(turnSpeed));

                if (sparkTickListener == 0)
                {
                    grindSound.Start();
                    grindSound.FadeIn(0.32f, null);
                    sparkTickListener = RegisterGameTickListener(onParticleTick, 20);
                }
            } else
            {
                if (sparkTickListener != 0)
                {
                    grindSound.FadeOut(0.32f, (s) => grindSound.Stop());
                    UnregisterGameTickListener(sparkTickListener);
                    sparkTickListener = 0;
                }
            }
        }

        private void onParticleTick(float dt)
        {
            var facing = BlockFacing.FromCode(Block.Variant["side"]).Normalf.Clone();

            if (AxisSign[0] < 0)
            {
            //    facing *= -1;
            }

            var swipe = Math.Sin(Api.World.ElapsedMilliseconds / 300d) / 12.0;
            var ws = -turnSpeed / 2;

            SimpleParticleProperties props = new SimpleParticleProperties()
            {
                MinQuantity = 1,
                AddQuantity = 3,
                VertexFlags = 255,
                Color = ColorUtil.ColorFromRgba(0, 255, 255, 255),
                LifeLength = 0.05f,
                MinSize = 0.02f,
                MaxSize = 0.05f,
                ParticleModel = EnumParticleModel.Quad,
                MinVelocity = new Vec3f(-0.25f - facing.X * 2 * ws, -0.25f, -0.25f - facing.Z * 2 * ws),
                AddVelocity = new Vec3f(0.5f - facing.X * 4 * ws, -0.5f, 0.5f - facing.Z * 4 * ws),
            };

            props.MinPos = Pos.ToVec3d().Add(0.5, 0.9, 0.5);
            props.AddPos = new Vec3d(facing.Z * swipe, 0, facing.X * swipe);
            Api.World.SpawnParticles(props);
        }

        protected MeshData StandMesh => ObjectCacheUtil.GetOrCreate(capi, "grindingwheel-standmesh" + Facing.Code, () => getMesh(["stand1/*", "stand2/*"]));
        protected MeshData getMesh(string[] selEles)
        {
            Shape shape = API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/grindingwheel.json");
            float rotateY = 0f;
            switch (Facing.Index)
            {
                case 0:
                    rotateY = 180;
                    break;
                case 1:
                    rotateY = 90;
                    break;
                case 3:
                    rotateY = 270;
                    break;
                default:
                    break;
            }
            capi.Tesselator.TesselateShape(Block, shape, out MeshData mesh, new Vec3f(0, rotateY, 0), null, selEles);
            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            LightRgba = Api.World.BlockAccessor.GetLightRGBs(Pos);
            mesher.AddMeshData(StandMesh);
            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            if (worldAccessForResolve.Side == EnumAppSide.Client)
            {
                Grinding = tree.GetBool("grinding");
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("grinding", Grinding);
        }

        public bool OnInteractStart(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!canBuff(byPlayer.InventoryManager.ActiveHotbarSlot)) return false;
            if (Math.Abs(turnSpeed) < 0.1) return false;

            PlayersGrinding[byPlayer.PlayerUID]=0;
            if (grindSound == null) loadGrindSound();
            return true;
        }

        private bool canBuff(ItemSlot slot)
        {
            if (slot.Empty) return false;
            var bhb = slot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorBuffable>();
            if (bhb == null) return false;
            var curBuff = bhb.GetItemBuffs(slot.Itemstack).FirstOrDefault((buff) => buff.Code == "sharpened");
            return curBuff == null || curBuff.Multiplier < 1.099;
        }

        public bool OnInteractStep(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!canBuff(byPlayer.InventoryManager.ActiveHotbarSlot)) return false;

            return true;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            grindSound?.Dispose();
            turnSound?.Dispose();
        }
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            grindSound?.Dispose();
            turnSound?.Dispose();
        }

        private void loadGrindSound()
        {
            if (capi == null) return;

            grindSound = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/effect/metalgrinding.ogg"),
                ShouldLoop = true,
                DisposeOnFinish = false,
                Position = Pos.ToVec3f(),
                RelativePosition = false,
                Range = 16,
                SoundType = EnumSoundType.Sound,
                Volume = 1
            });
        }

        public void OnInteractStop(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            PlayersGrinding.Remove(byPlayer.PlayerUID);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (turnSpeed > 0 && turnSpeed < 0.1)
            {
                dsc.AppendLine(Lang.Get("Turns too slowly to grind"));
            }

            base.GetBlockInfo(forPlayer, dsc);
        }
    }
}
