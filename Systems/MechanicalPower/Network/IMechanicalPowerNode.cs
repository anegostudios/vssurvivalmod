using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{

    /// <summary>
    /// The most basic element of a mechanical power network - could be part of a block
    /// </summary>
    public interface IMechanicalPowerNode
    {
        /// <summary>
        /// Amount of torque produced for given speed of the mechanical network it is in. 
        /// Return a positive number if the torque is in clockwise direction when looking north or east, and negative if counter clockwise
        /// If not a power producer but a consumer, return an above zero resistance value
        /// </summary>
        /// <returns></returns>
        float GetTorque(long tick, float speed, out float resistance);

        /// <summary>
        /// Bounds the combined change in driving torque and opposing drag as the shaft moves from speed towards nextSpeed.
        /// Return the largest change per unit of speed from the starting speed to any point in that interval.
        /// Both speeds are on the same side of zero. Return zero if neither torque nor drag depends on speed.
        /// Called after GetTorque() and MUST not update the producer again.
        /// </summary>
        float GetTorqueResponse(float speed, float nextSpeed) => 0f;

        /// <summary>
        /// Local shaft inertia normalized to the mechanical solver's 0.1-second torque step.
        /// Must be positive. The network reflects it through the square of the geared ratio.
        /// </summary>
        float GetRotationalInertia() => 1f;

        void LeaveNetwork();
        BlockPos GetPosition();
        float GearedRatio { get; set; }
        float OverheatValue { get; set; }
    }
}
