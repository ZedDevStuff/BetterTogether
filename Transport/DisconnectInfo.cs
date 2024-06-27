using System;
using System.Collections.Generic;
using System.Text;

namespace BetterTogetherCore.Transport
{
    /// <summary>
    /// This struct is used to store the reason and an optional message for a disconnection.
    /// </summary>
    public struct DisconnectInfo
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
        public DisconnectInfo(string reason, string message)
        {
            Reason = reason;
            Message = message;
        }
    }
}
