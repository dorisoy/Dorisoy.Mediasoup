using System;
using System.Runtime.InteropServices;

namespace Dorisoy.Libuv
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_work_t
    {
        public IntPtr loop;
        public IntPtr work_cb;
        public IntPtr work_after_cb;
    }
}
