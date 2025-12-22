using FBS.Consumer;

namespace Dorisoy.Mediasoup
{
    public class SetConsumerPriorityRequest : SetPriorityRequestT
    {
        public string ConsumerId { get; set; }
    }
}
