using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class Rift
    {
        public float Size = 1f;
        public Vec3d Position;
        public double SpawnedTotalHours;
        public double DieAtTotalHours;

        public bool Visible = true;

        public float GetNowSize(ICoreAPI api)
        {
            float smoothDie = (float)GameMath.Clamp((DieAtTotalHours - api.World.Calendar.TotalHours) * 10, 0, 1);
            float smoothGrow = (float)GameMath.Serp(0, 1, (float)GameMath.Clamp((api.World.Calendar.TotalHours - SpawnedTotalHours) * 20, 0, 1));

            return Size * smoothDie * smoothGrow;
        }
    }
}