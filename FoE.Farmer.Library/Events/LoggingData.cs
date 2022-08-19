using System;

namespace FoE.Farmer.Library.Events
{
    public class LoggingDataEventArgs : EventArgs
    {
        public string Message { get; set; }
        public LogMessageType Type { get; set; }
    }
}
