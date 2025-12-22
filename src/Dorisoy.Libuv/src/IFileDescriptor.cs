using System;

namespace Dorisoy.Libuv
{
    public interface IFileDescriptor
    {
        void Open(IntPtr socket);

        IntPtr FileDescriptor { get; }
    }
}
