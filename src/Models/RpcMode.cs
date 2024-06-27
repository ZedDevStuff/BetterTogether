namespace BetterTogetherCore.Models
{
    /// <summary>
    /// The mode of an RPC
    /// </summary>
    public enum RpcMode
    {
        /// <summary>
        /// The RPC is sent to a specific peer
        /// </summary>
        Target,
        /// <summary>
        /// The RPC is sent to all peers except the sender
        /// </summary>
        Others,
        /// <summary>
        /// The RPC is sent to all peers including the sender
        /// </summary>
        All,
        /// <summary>
        /// The RPC is sent to the server then back. Why would you use this? Feel free to enlighten me.
        /// </summary>
        Host,
        /// <summary>
        /// The RPC is sent to the server
        /// </summary>
        Server
    }
}
