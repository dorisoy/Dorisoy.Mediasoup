using System.Collections.Generic;

namespace Dorisoy.Mediasoup
{
    public class PullRequest
    {
        public string PeerId { get; set; }

        public HashSet<string> Sources { get; set; }
    }
}
