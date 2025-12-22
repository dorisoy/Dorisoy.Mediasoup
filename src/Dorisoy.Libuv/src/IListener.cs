using System;

namespace Dorisoy.Libuv
{
    public interface IListener<TStream>
    {
        void Listen();

        event Action? Connection;

        TStream? Accept();
    }
}
