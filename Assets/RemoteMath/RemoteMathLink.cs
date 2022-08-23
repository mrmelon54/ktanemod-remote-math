using System.Runtime.InteropServices;

namespace RemoteMath
{
    public static class RemoteMathLink
    {
        [DllImport("ktanemod-remote-math-interface", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoteMathInterfaceEntry();
    }
}