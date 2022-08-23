using System.Runtime.InteropServices;

namespace RemoteMath
{
    public static class RemoteMathInterface
    {
        [DllImport("ktanemod-remote-math-interface", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RemoteMathInterfaceEntry();

        public static void Entry()
        {
            RemoteMathInterfaceEntry();
        }
    }
}