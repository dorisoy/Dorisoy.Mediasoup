using FBS.Consumer;

namespace Dorisoy.Mediasoup
{
    public class SetConsumerPreferredLayersRequest : SetPreferredLayersRequestT
    {
        public string ConsumerId { get; set; }
    }
}
