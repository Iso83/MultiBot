using System;

namespace FoE.Farmer.Library.Events
{
    public class ResourceUpdateEventArgs : EventArgs
    {
        public (string, int)[] Values { get; set; }
    }
}
