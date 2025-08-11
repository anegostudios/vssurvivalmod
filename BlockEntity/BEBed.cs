using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BlockEntityBed : BlockEntity, IMountableSeat, IMountable
    {
        long restingListener;

        static Vec3f eyePos = new Vec3f(0, 0.3f, 0);

        float sleepEfficiency = 0.5f;
        BlockFacing facing;
        float y2 = 0.5f;
        double hoursTotal;
        public EntityAgent MountedBy;
        bool blockBroken;
        long mountedByEntityId;
        string mountedByPlayerUid;
        EntityControls controls = new EntityControls();
        EntityPos mountPos = new EntityPos();
        public bool DoTeleportOnUnmount { get; set; } = true;

        public EntityPos SeatPosition => Position; // Since we have only one seat, it can be the same as the base position
        public double StepPitch => 0;
        public EntityPos Position
        {
            get
            {
                BlockFacing facing = this.facing.Opposite;

                mountPos.SetPos(Pos);
                mountPos.Yaw = this.facing.HorizontalAngleIndex * GameMath.PIHALF + GameMath.PIHALF;

                if (facing == BlockFacing.NORTH) return mountPos.Add(0.5, y2, 1);
                if (facing == BlockFacing.EAST) return mountPos.Add(0, y2, 0.5);
                if (facing == BlockFacing.SOUTH) return mountPos.Add(0.5, y2, 0);
                if (facing == BlockFacing.WEST) return mountPos.Add(1, y2, 0.5);

                return null;
            }
        }

        AnimationMetaData meta = new AnimationMetaData() { Code = "sleep", Animation = "lie" }.Init();
        public AnimationMetaData SuggestedAnimation => meta;
        public EntityControls Controls => controls;
        public IMountable MountSupplier => this;
        public EnumMountAngleMode AngleMode => EnumMountAngleMode.FixateYaw;
        public Vec3f LocalEyePos => eyePos;
        Entity IMountableSeat.Passenger => MountedBy;
        public bool CanControl => false;
        public Entity Entity => null;
        public Matrixf RenderTransform => null;
        public IMountableSeat[] Seats => new IMountableSeat[] { this };

        public bool SkipIdleAnimation => false;

        public float FpHandPitchFollow => 1;

        public string SeatId { get => "bed-0"; set { } }

        public SeatConfig Config { get => null; set { } }
        public long PassengerEntityIdForInit { get => mountedByEntityId; set => mountedByEntityId = value; }

        public Entity Controller => MountedBy;

        public Entity OnEntity => null;

        public EntityControls ControllingControls => null;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            controls.OnAction = onControls;
            if (Block.Attributes != null) sleepEfficiency = Block.Attributes["sleepEfficiency"].AsFloat(0.5f);

            Cuboidf[] collboxes = Block.GetCollisionBoxes(api.World.BlockAccessor, Pos);
            if (collboxes != null && collboxes.Length > 0) y2 = collboxes[0].Y2;

            facing = BlockFacing.FromCode(Block.LastCodePart());


            if (MountedBy == null && (mountedByEntityId != 0 || mountedByPlayerUid != null))
            {
                var entity = mountedByPlayerUid != null ? api.World.PlayerByUid(mountedByPlayerUid)?.Entity : api.World.GetEntityById(mountedByEntityId) as EntityAgent;
                if (entity?.SidedProperties != null) // Player entity might not be initialized if we load a sleeping player from spawnchunks
                {
                    entity.TryMount(this);
                }
            }
        }

        private void onControls(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            if (action == EnumEntityAction.Sneak && on)
            {
                MountedBy?.TryUnmount();
                controls.StopAllMovement();
                handled = EnumHandling.PassThrough;
            }
        }

        private void RestPlayer(float dt)
        {
            double hoursPassed = Api.World.Calendar.TotalHours - hoursTotal;

            // Since waking up takes an hour, we take away one hour from the sleepEfficiency
            float sleepEff = sleepEfficiency - 1f / 12;

            if (hoursPassed > 0)
            {
                int tempStormSleep = Api.World.Config.GetString("temporalStormSleeping", "0").ToInt();
                if (tempStormSleep == 0 && Api.ModLoader.GetModSystem<SystemTemporalStability>().StormStrength > 0)
                {
                    MountedBy.TryUnmount();
                    return;
                }

                EntityBehaviorTiredness ebt = MountedBy?.GetBehavior("tiredness") as EntityBehaviorTiredness;
                if (ebt != null)
                {
                    float newval = Math.Max(0, ebt.Tiredness - (float)hoursPassed / sleepEff);
                    ebt.Tiredness = newval;
                    if (newval <= 0)
                    {
                        MountedBy.TryUnmount();
                    }
                }

                hoursTotal = Api.World.Calendar.TotalHours;
            }
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);

            blockBroken = true;
            MountedBy?.TryUnmount();
        }




        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            mountedByEntityId = tree.GetLong("mountedByEntityId");
            mountedByPlayerUid = tree.GetString("mountedByPlayerUid");
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("mountedByEntityId", mountedByEntityId);
            tree.SetString("mountedByPlayerUid", mountedByPlayerUid);
        }


        public void MountableToTreeAttributes(TreeAttribute tree)
        {
            tree.SetString("className", "bed");
            tree.SetInt("posx", Pos.X);
            tree.SetInt("posy", Pos.InternalY);
            tree.SetInt("posz", Pos.Z);
        }

        public void DidUnmount(EntityAgent entityAgent)
        {
            EntityBehaviorTiredness ebt = MountedBy?.GetBehavior("tiredness") as EntityBehaviorTiredness;
            if (ebt != null) ebt.IsSleeping = false;
            MountedBy = null;

            if (!blockBroken)
            {
                BlockFacing blockFacing = BlockFacing.FromCode(Block.LastCodePart()).Opposite;
                var feetPos = Pos.AddCopy(facing);

                foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                {
                    Vec3d placePos = Pos.ToVec3d().AddCopy(facing).Add(0.5, 0.001, 0.5);
                    Vec3d placePosFeet = feetPos.ToVec3d().AddCopy(facing).Add(0.5, 0.001, 0.5);

                    if (!Api.World.CollisionTester.IsColliding(Api.World.BlockAccessor, entityAgent.SelectionBox, placePos, false))
                    {
                        entityAgent.TeleportTo(placePos);
                        break;
                    }
                    if (!Api.World.CollisionTester.IsColliding(Api.World.BlockAccessor, entityAgent.SelectionBox, placePosFeet, false))
                    {
                        entityAgent.TeleportTo(placePosFeet);
                        break;
                    }
                }
            }

            mountedByEntityId = 0;
            mountedByPlayerUid = null;

            UnregisterGameTickListener(restingListener);
            restingListener = 0;
        }

        public void DidMount(EntityAgent entityAgent)
        {
            if (MountedBy != null && MountedBy != entityAgent)
            {
                entityAgent.TryUnmount();
                return;
            }

            if (MountedBy == entityAgent)
            {
                // Already mounted
                return;
            }

            MountedBy = entityAgent;
            mountedByPlayerUid = (entityAgent as EntityPlayer)?.PlayerUID;
            mountedByEntityId = MountedBy.EntityId;

            if (entityAgent.Api?.Side == EnumAppSide.Server)
            {
                if (restingListener == 0)
                {
                    var oldapi = this.Api;
                    this.Api = entityAgent.Api;   // in case this.Api is currently null if this is called by LoadEntity method for entityAgent; a null Api here would cause RegisterGameTickListener to throw an exception
                    restingListener = RegisterGameTickListener(RestPlayer, 200);
                    this.Api = oldapi;
                }
                hoursTotal = entityAgent.Api.World.Calendar.TotalHours;
            }

            if (MountedBy != null)
            {
                entityAgent.Api.Event.EnqueueMainThreadTask(() => // Might not be initialized yet if this is loaded from spawnchunks
                {
                    if (MountedBy != null)
                    {
                        EntityBehaviorTiredness ebt = MountedBy.GetBehavior("tiredness") as EntityBehaviorTiredness;
                        if (ebt != null) ebt.IsSleeping = true;
                    }
                }, "issleeping");
            }


            MarkDirty(false);
        }

        public bool IsMountedBy(Entity entity) => this.MountedBy == entity;
        public bool IsBeingControlled() => false;
        public bool CanUnmount(EntityAgent entityAgent) => true;
        public bool CanMount(EntityAgent entityAgent) => !AnyMounted();

        public bool AnyMounted() => MountedBy != null;
    }
}
