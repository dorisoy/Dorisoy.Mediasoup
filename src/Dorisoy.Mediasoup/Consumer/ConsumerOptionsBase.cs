using System.Collections.Generic;

namespace Dorisoy.Mediasoup
{
    public class ConsumerOptionsBase
    {
        /// <summary>
        /// The id of the Producer to consume.
        /// </summary>
        public string ProducerId { get; init; }

        /// <summary>
        /// Custom application data.
        /// </summary>
        public Dictionary<string, object>? AppData { get; set; }
    }
}
