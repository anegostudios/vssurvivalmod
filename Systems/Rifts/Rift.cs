using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class Rift
    {
        [ProtoMember(1)]
        public int RiftId;
        [ProtoMember(2)]
        public float Size = 1f;
        [ProtoMember(3)]
        public Vec3d Position;
        [ProtoMember(4)]
        public double SpawnedTotalHours;
        [ProtoMember(5)]
        public double DieAtTotalHours;

        public bool Visible = true;

        public bool HasLineOfSight;
        public float VolumeMul = 0;
        public float accum = 2;

        public float GetNowSize(ICoreAPI api)
        {
            float smoothDie = (float)GameMath.Clamp((DieAtTotalHours - api.World.Calendar.TotalHours) * 10, 0, 1);
            float smoothGrow = (float)GameMath.Serp(0, 1, (float)GameMath.Clamp((api.World.Calendar.TotalHours - SpawnedTotalHours) * 20, 0, 1));

            return Size * smoothDie * smoothGrow;
        }


        public void OnNearTick(ICoreClientAPI capi, float dt)
        {
            if (Size <= 0) return;

            accum += dt;

            if (accum > 2)
            {
                accum = 0;

                Vec3d plrPos = capi.World.Player.Entity.Pos.XYZ.Add(capi.World.Player.Entity.LocalEyePos);
                double dist = Position.DistanceTo(plrPos);

                if (dist < 24)
                {
                    var sele = capi.World.InteresectionTester.GetSelectedBlock(plrPos, Position, (pos, block) => block.CollisionBoxes != null && block.CollisionBoxes.Length != 0 && block.BlockMaterial != EnumBlockMaterial.Leaves);

                    HasLineOfSight = sele == null;
                }
            }

            VolumeMul = GameMath.Clamp(VolumeMul + dt * (HasLineOfSight ? 0.5f : -0.5f), 0.15f, 1);
        }

        internal void SetFrom(Rift rift)
        {
            this.Size = rift.Size;
            this.Position = rift.Position;
            this.SpawnedTotalHours = rift.SpawnedTotalHours;
            this.DieAtTotalHours = rift.DieAtTotalHours;
        }
    }
}