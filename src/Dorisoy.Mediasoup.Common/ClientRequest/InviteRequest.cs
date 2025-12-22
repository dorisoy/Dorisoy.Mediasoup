using System.Collections.Generic;

namespace Dorisoy.Mediasoup
{
    public class InviteRequest
    {
        public string PeerId { get; set; }

        public HashSet<string> Sources { get; set; }
    }

    public class DeinviteRequest
    {
        public string PeerId { get; set; }

        public HashSet<string> Sources { get; set; }
    }
}
