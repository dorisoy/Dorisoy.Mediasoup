using System.Collections.Generic;
using Dorisoy.Mediasoup;

namespace Dorisoy.Meeting.Server
{
    public class PeerPullResult
    {
        public Producer[] ExistsProducers { get; init; }

        public HashSet<string> ProduceSources { get; init; }
    }
}
