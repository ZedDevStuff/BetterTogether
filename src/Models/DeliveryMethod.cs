namespace BetterTogetherCore.Models
{
    /// <summary>
    /// Those are based on LiteNetLib's DeliveryMethods. If your transport does not support these, use Unsupported in any case.
    /// </summary>
    public enum DeliveryMethod
    {
        /// <summary>
        /// Reliable. Packets won't be dropped, won't be duplicated, can arrive without order.
        /// </summary>
        ReliableUnordered,
        /// <summary>
        /// 
        /// </summary>
        Sequenced,
        /// <summary>
        /// 
        /// </summary>
        ReliableOrdered,
        /// <summary>
        /// 
        /// </summary>
        ReliableSequenced,
        /// <summary>
        /// 
        /// </summary>
        Unreliable,
        /// <summary>
        /// Used for transports that do not support delivery methods
        /// </summary>
        Unsupported,
    }
}
