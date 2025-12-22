using FBS.Transport;

namespace Dorisoy.Mediasoup
{
    public class PlainTransportData : TransportBaseData
    {
        public bool? RtcpMux { get; set; }

        public bool? Comedia { get; set; }

        public TupleT Tuple { get; set; }

        public TupleT? RtcpTuple { get; set; }

        public TupleT? SrtpParameters { get; set; }
    }
}
