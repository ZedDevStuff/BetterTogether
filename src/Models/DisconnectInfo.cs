using MemoryPack;

namespace BetterTogetherCore.Models
{
    /// <summary>
    /// This struct is used to store the reason and an optional message for a disconnection.
    /// </summary>
    [MemoryPackable]
    public partial class DisconnectInfo
    {
        public string Reason { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// Main constructor
        /// </summary>
        /// <param name="reason">The reason for the disconnection</param>
        public DisconnectInfo(string reason)
        {
            Reason = reason;
            Message = "";
        }
        /// <summary>
        /// Secondary constructor
        /// </summary>
        /// <param name="reason">The reason for the disconnection</param>
        /// <param name="message">The message for the disconnection</param>
        [MemoryPackConstructor]
        public DisconnectInfo(string reason, string message)
        {
            Reason = reason;
            Message = message;
        }
    }
}
