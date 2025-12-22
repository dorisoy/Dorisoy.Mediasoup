using System;

namespace Dorisoy.Libuv
{
    public interface IConnectable<TType, TEndPoint>
    {
        void Connect(TEndPoint endPoint, Action<Exception?>? callback);
    }
}
