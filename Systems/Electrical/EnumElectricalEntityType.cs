namespace Vintagestory.GameContent.Electrical
{
    /// <summary>
    /// The purpose of an Electrical Block Entity
    /// </summary>
    public enum EnumElectricalEntityType
    {
        /// <summary>
        /// A consumer of power
        /// </summary>
        Consumer,
        /// <summary>
        /// A producer/generator of power
        /// </summary>
        Producer,
        /// <summary>
        /// Storage, it stores power. Can both send and receive.
        /// </summary>
        Storage,
        /// <summary>        
        /// A bridge between different tiers of networks.
        /// </summary>
        Transformer,
        /// <summary>
        /// An interactable switch between two networks.
        /// </summary>
        Toggle,
        /// <summary>
        /// A Relay is a dummy node that simply allows wires to connect to it. Like the nodes 
        /// of a Telephone pole.
        /// </summary>
        Relay,
        /// <summary>
        /// Some other type of entity not covered in any other given types.
        /// </summary>
        Other
    }
}
