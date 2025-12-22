using System;

namespace Dorisoy.Libuv
{
    public interface IMessageSender<TMessage>
    {
        void Send(TMessage message, Action<Exception?>? callback);
    }
}
