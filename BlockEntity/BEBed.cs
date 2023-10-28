using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class BlockEntityBed : BlockEntity, IMountable
    {
        float sleepEfficiency = 0.5f;
        BlockFacing facing;
        
        float y2 = 0.5f;

        double hoursTotal;

        public EntityAgent MountedBy;


        bool blockBroken;
        long mountedByEntityId;
        string mountedByPlayerUid;

        EntityPos mountPos = new EntityPos();
        public EntityPos MountPosition
        {
            get {
                BlockFacing facing = this.facing.Opposite;

                mountPos.SetPos(Pos);
                mountPos.Yaw = this.facing.HorizontalAngleIndex * GameMath.PIHALF;

                if (facing == BlockFacing.NORTH) return mountPos.Add(0.5, y2, 1);
                if (facing == BlockFacing.EAST) return mountPos.Add(0, y2, 0.5);
                if (facing == BlockFacing.SOUTH) return mountPos.Add(0.5, y2, 0);
                if (facing == BlockFacing.WEST) return mountPos.Add(1, y2, 0.5);

                return null;
            }
        }

        public string SuggestedAnimation
        {
            get { return "sleep"; }
        }

        EntityControls controls = new EntityControls();
        public EntityControls Controls
        {
            get {
                return controls; 
            }
        }


        public IMountableSupplier MountSupplier => null;
        public EnumMountAngleMode AngleMode => EnumMountAngleMode.FixateYaw;

        static Vec3f eyePos = new Vec3f(0, 0.3f, 0);
        public Vec3f LocalEyePos => eyePos;
        Entity IMountable.MountedBy => MountedBy;

        public bool CanControl => false;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            controls.OnAction = onControls;
            if (Block.Attributes != null) sleepEfficiency = Block.Attributes["sleepEfficiency"].AsFloat(0.5f);

            Cuboidf[] collboxes = Block.GetCollisionBoxes(api.World.BlockAccessor, Pos);
            if (collboxes!=null && collboxes.Length > 0) y2 = collboxes[0].Y2;

            facing = BlockFacing.FromCode(Block.LastCodePart());


            if (MountedBy == null && (mountedByEntityId != 0 || mountedByPlayerUid != null))
            {
                var entity = mountedByPlayerUid != null ? api.World.PlayerByUid(mountedByPlayerUid)?.Entity : api.World.GetEntityById(mountedByEntityId) as EntityAgent;
                if (entity != null)
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
                foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
                {
                    Vec3d placepos = Pos.ToVec3d().AddCopy(facing).Add(0.5, 0.001, 0.5);
                    if (!Api.World.CollisionTester.IsColliding(Api.World.BlockAccessor, entityAgent.SelectionBox, placepos, false))
                    {
                        entityAgent.TeleportTo(placepos);
                        break;
                    }
                }
            }

            mountedByEntityId = 0;
            mountedByPlayerUid = null;

            base.OnBlockRemoved();
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

            if (Api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(RestPlayer, 200);
                hoursTotal = Api.World.Calendar.TotalHours;
            }

            EntityBehaviorTiredness ebt = MountedBy?.GetBehavior("tiredness") as EntityBehaviorTiredness;
            if (ebt != null) ebt.IsSleeping = true;
        }
        
    }
}
