namespace Vintagestory.GameContent.Mechanics
{
    public class MechanicalPowerPart(BEBehaviorMPBase owner, int index)
    {
        public BEBehaviorMPBase Owner { get; } = owner;
        public int Index { get; set; } = index;
        public float Torque { get; set; }
        public float Resistance { get; set; }
        public float Inertia { get; set; }
        public float Load { get; set; }

        public double GetRatio(double angle) => Owner.GearedRatio * (Owner.AngleTransform?.GetSpeedFactor(angle) ?? 1);
    }
}
