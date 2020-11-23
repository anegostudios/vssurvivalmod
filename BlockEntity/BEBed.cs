using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityBed : BlockEntity, IMountable
    {
        float sleepEfficiency = 0.5f;
        Dictionary<string, long> playerSittingMs = new Dictionary<string, long>();
        BlockFacing facing;
        
        float y2 = 0.5f;

        double hoursTotal;

        public EntityAgent MountedBy;

        public Vec3d MountPosition
        {
            get {
                BlockFacing facing = this.facing.Opposite;

                if (facing == BlockFacing.NORTH) return Pos.ToVec3d().Add(0.5, y2, 1);
                if (facing == BlockFacing.EAST) return Pos.ToVec3d().Add(0, y2, 0.5);
                if (facing == BlockFacing.SOUTH) return Pos.ToVec3d().Add(0.5, y2, 0);
                if (facing == BlockFacing.WEST) return Pos.ToVec3d().Add(1, y2, 0.5);

                return null;
            }
        }

        public string SuggestedAnimation
        {
            get { return "sleep"; }
        }

        public EntityControls Controls
        {
            get { return null; }
        }

        public float? MountYaw
        {
            get
            {
                if (facing == null) return null;

                return facing.HorizontalAngleIndex * GameMath.PIHALF;
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Block.Attributes != null) sleepEfficiency = Block.Attributes["sleepEfficiency"].AsFloat(0.5f);

            

            Cuboidf[] collboxes = Block.GetCollisionBoxes(api.World.BlockAccessor, Pos);
            if (collboxes!=null && collboxes.Length > 0) y2 = collboxes[0].Y2;

            facing = BlockFacing.FromCode(Block.LastCodePart());
        }


        private void RestPlayer(float dt)
        {
            double hoursPassed = Api.World.Calendar.TotalHours - hoursTotal;

            // Since waking up takes an hour, we take away one hour from the sleepEfficiency
            float sleepEff = sleepEfficiency - 1f / 12;

            if (hoursPassed > 0)
            {
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



        

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }
        

        public void MountableToTreeAttributes(TreeAttribute tree)
        {
            tree.SetString("className", "bed");
            tree.SetInt("posx", Pos.X);
            tree.SetInt("posy", Pos.Y);
            tree.SetInt("posz", Pos.Z);
        }

        public void DidUnmount(EntityAgent entityAgent)
        {
            EntityBehaviorTiredness ebt = MountedBy?.GetBehavior("tiredness") as EntityBehaviorTiredness;
            if (ebt != null) ebt.IsSleeping = false;
            MountedBy = null;
            
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                Vec3d placepos = Pos.ToVec3d().AddCopy(facing).Add(0.5, 0.001, 0.5);
                if (!Api.World.CollisionTester.IsColliding(Api.World.BlockAccessor, entityAgent.CollisionBox, placepos, false))
                {
                    entityAgent.TeleportTo(placepos);
                    break;
                }
            }

            base.OnBlockRemoved();
        }

        public void DidMount(EntityAgent entityAgent)
        {
            if (MountedBy != null) return;

            MountedBy = entityAgent;
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
