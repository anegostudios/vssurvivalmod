
#nullable disable
namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// For nodes which can transfer power between 2 networks, for example Transmission or Large gear
    /// </summary>
    internal interface IMechanicalPowerTransfer
    {
        void TransferPower(MechanicalNetwork mechanicalNetwork);
    }
}