using Vintagestory.API.MathTools;

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

        void LeaveNetwork();
        BlockPos GetPosition();
        float GearedRatio { get; set; }
    }
}
