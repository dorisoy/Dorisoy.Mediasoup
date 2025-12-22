using System.Collections.Generic;
using Dorisoy.Mediasoup;

namespace Dorisoy.Meeting.Server
{
    public class PullResult
    {
        public Peer ConsumePeer { get; init; }

        public Peer ProducePeer { get; init; }

        public Producer[] ExistsProducers { get; init; }

        public HashSet<string> Sources { get; init; }
    }
}
