using System;

namespace Dorisoy.Libuv
{
    public interface IMessageReceiver<TMessage>
    {
        event Action<TMessage> Message;
    }
}
