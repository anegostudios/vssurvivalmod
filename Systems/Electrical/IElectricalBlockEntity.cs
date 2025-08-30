﻿
namespace Vintagestory.GameContent.Electrical
{
    /// <summary>
    /// Universal Power Interface for Electrical Block Entities
    /// </summary>
    public interface IElectricalBlockEntity
    {
        /// <summary>
        /// What is the max power this machine can hold?
        /// <br>Type : Unsigned Long (ulong)</br>
        /// <br>Max Value : 18,446,744,073,709,551,615</br>
        /// </summary>
        ulong MaxPower { get; }

        /// What is the MAX Power per second this machine can give or accept
        /// <br>Type : Unsigned Long (ulong)</br>
        /// <br>Max Value : 18,446,744,073,709,551,615</br>
        /// </summary>
        ulong MaxPPS { get; }

        /// <summary>
        /// Current Power in Entity
        /// </summary>
        ulong CurrentPower { get; }

        /// <summary>
        /// What sort of Machine is this?
        /// <br>Valid Types: Consumer, Producer, Storage, Transformer, Toggle, Relay, Other</br>
        /// </summary>
        EnumElectricalEntityType ElectricalEntityType { get; }

        /// <summary>
        /// Can Receive Power?
        /// </summary>
        bool CanReceivePower { get; }

        /// <summary>
        /// Can power be extracted?
        /// </summary>
        bool CanExtractPower { get; }

        /// <summary>
        /// Is Power Full? i.e. Can simply be MaxPower == CurrentPower
        /// </summary>
        bool IsPowerFull { get; }

        /// <summary>
        /// Is this block sleeping?<br/>
        /// For example, if a machine is ON but NOT Crafting it is sleeping.<br/>
        /// A sleeping machine should tick at a slower rate to preserve update time.
        /// </summary>
        bool IsSleeping { get; }

        /// <summary>
        /// Is this Machine Enabled? (On/Off)
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// What is the Priority of this Entity?
        /// <br>1 = highest priority.</br>
        /// <br>If every entity is the same priority, then the priority system is negated.</br>
        /// <br>Example, higher priority generators are first to empty, machines are first to fill, etc.</br>
        /// <br>Can be hard-coded or set via GUI.</br>
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Power Needed/Available Rated to a machines MaxPPS given the deltaTime.<br/>
        /// Used by the Electric Network to quickly determine power for a given tick time.<br/>
        /// Should not actually alter a machines power.
        /// </summary>
        /// <param name="dt">DeltaTime (time betwen ticks in decimal seconds)</param>
        /// <param name="isInsert">True for inserting power, false for extracting.</param>
        /// <returns>Power for this deltatime</returns>
        ulong RatedPower(float dt, bool isInsert = false);

        /// <summary>
        /// Takes powerOffered and removes any power needed and returns power left over.<br/>
        /// It's up to the implementing block entity to track it's internal power and PPS limits.
        /// </summary>
        /// <param name="powerOffered">Power offered to this Entity</param>
        /// <param name="dt">Delta Time; Time elapsed since last update.</param>
        /// <param name="simulate">[Optional] Whether to simulate function and not actually give power.</param>
        /// <returns>Power left over (0 if all power was consumed)</returns>
        ulong ReceivePower(ulong powerOffered, float dt, bool simulate = false);

        /// <summary>
        /// Reduces powerWanted by power held in this entity.<br/>
        /// It's up to the implementing block entity to track it's internal power and PPS limits.
        /// </summary>
        /// <param name="powerWanted">How much total power is needed</param>
        /// <param name="dt">Delta Time; Time elapsed since last update.</param>
        /// <param name="simulate">[Optional] Whether to just simulate function and not actually take power.</param>
        /// <returns>Unfulfilled amount of powerWanted (0 if all wanted power was satisfied)</returns>
        ulong ExtractPower(ulong powerWanted, float dt, bool simulate = false);

        /// <summary>
        /// Completely fill (or drain) power buffer.<br/>
        /// A fast way for Electrical Networks to process power for this entity.<br/>
        /// </summary>
        /// <param name="drain">[Optional] Drain power to 0 if true.</param>
        void CheatPower(bool drain = false);

        /// <summary>
        /// True if the Block is loaded, false if the block is unloaded.
        /// </summary>
        bool IsLoaded { get; }

		/// <summary>
        /// Returns the IElectricalBlockEntity for the BlockEntity or one of its behaviors at given position.
        /// </summary>
        /// <param name="blockAccessor">The accessor for the world</param>
        /// <param name="pos">The position of the block</param>
        /// <returns>The interface, or null if the block at that position does not implement it.</returns>
        static IElectricalBlockEntity GetAtPos(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity entity = blockAccessor.GetBlockEntity(pos);
            if (entity is IElectricalBlockEntity converted) {
                return converted;
            }
            return entity?.GetBehavior<IElectricalBlockEntity>();
        }
    }
}
