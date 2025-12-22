using FBS.Transport;

namespace Dorisoy.Mediasoup
{
    public class PlainTransportSettings
    {
        public ListenInfoT ListenInfo { get; set; }

        public uint MaxSctpMessageSize { get; set; }
    }
}
