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
        Block block;
        float y2 = 0.5f;

        double hoursTotal;

        public EntityAgent MountedBy;

        public Vec3d MountPosition
        {
            get {
                BlockFacing facing = this.facing.GetOpposite();

                if (facing == BlockFacing.NORTH) return pos.ToVec3d().Add(0.5, y2, 1);
                if (facing == BlockFacing.EAST) return pos.ToVec3d().Add(0, y2, 0.5);
                if (facing == BlockFacing.SOUTH) return pos.ToVec3d().Add(0.5, y2, 0);
                if (facing == BlockFacing.WEST) return pos.ToVec3d().Add(1, y2, 0.5);

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
                return facing.HorizontalAngleIndex * GameMath.PIHALF;
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            block = api.World.BlockAccessor.GetBlock(pos);
            if (block.Attributes != null) sleepEfficiency = block.Attributes["sleepEfficiency"].AsFloat(0.5f);

            

            Cuboidf[] collboxes = block.GetCollisionBoxes(api.World.BlockAccessor, pos);
            if (collboxes!=null && collboxes.Length > 0) y2 = collboxes[0].Y2;

            facing = BlockFacing.FromCode(block.LastCodePart());
            
            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
            }
        }


        private void RestPlayer(float dt)
        {
            double hoursPassed = api.World.Calendar.TotalHours - hoursTotal;

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

                hoursTotal = api.World.Calendar.TotalHours;
            }
        }



        

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }
        

        public void MountableToTreeAttributes(TreeAttribute tree)
        {
            tree.SetString("className", "bed");
            tree.SetInt("posx", pos.X);
            tree.SetInt("posy", pos.Y);
            tree.SetInt("posz", pos.Z);
        }

        public void DidUnmount(EntityAgent entityAgent)
        {
            EntityBehaviorTiredness ebt = MountedBy?.GetBehavior("tiredness") as EntityBehaviorTiredness;
            if (ebt != null) ebt.IsSleeping = false;
            MountedBy = null;

            base.OnBlockRemoved();
        }

        public void DidMount(EntityAgent entityAgent)
        {
            if (MountedBy != null) return;

            MountedBy = entityAgent;
            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(RestPlayer, 200);
                hoursTotal = api.World.Calendar.TotalHours;
            }

            EntityBehaviorTiredness ebt = MountedBy?.GetBehavior("tiredness") as EntityBehaviorTiredness;
            if (ebt != null) ebt.IsSleeping = true;
        }
        
    }
}
