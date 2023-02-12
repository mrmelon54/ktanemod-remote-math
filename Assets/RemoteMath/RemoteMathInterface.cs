using System;
using System.Runtime.InteropServices;
using UnityEngine;
using KeepCoding;

namespace RemoteMath
{
    public static class RemoteMathInterface
    {
        [DllImport("ktanemod-remote-math-interface", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RemoteMathInterfaceEntry();

		[DllImport("ktanemod-remote-math-interface", CallingConvention = CallingConvention.Cdecl)]
		private static extern void RemoteMathIsEditor();

        public static void Entry()
        {
			if (Application.isEditor) {
				RemoteMathIsEditor();
				RemoteMathInterfaceEntry();
            } else Experimental.UnmanagedExternalMethod(GetAssemblyPath("ktanemod-remote-math-interface"), "RemoteMathInterfaceEntry").Invoke();
        }

		// Windows: <path>/dlls/<arch>/<file>.dll
        // OSX: <path>/dlls/<file>.dylib
        // Linux: <path>/dlls/lib<file>.so
        private static String GetAssemblyPath(String file)
        {
            string path = PathManager.GetDirectory();
            const string target = "dlls";
            string arch;
            switch (IntPtr.Size)
            {
                case 4:
                    arch = "x86";
                    break;
                case 8:
                    arch = "x86_64";
                    break;
                default:
                    throw new PlatformNotSupportedException("IntPtr's size is not 4 or 8. Only 32-bit and 64-bit devices are supported.");
            }

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                    return string.Format(@"{0}\{1}\{2}\{3}.{4}", path, target, arch, file, "dll");
                case RuntimePlatform.OSXPlayer:
                    return string.Format("{0}/{1}/{2}.{3}", path, target, file, "dylib");
                case RuntimePlatform.LinuxPlayer:
                    return string.Format("{0}/{1}/lib{2}.{3}", path, target, file, "so");
                default:
                    throw new PlatformNotSupportedException("The platform \"" + Application.platform + "\" is unsupported. The operating systems supported are Windows, Mac, and Linux.");
            }
        }
    }
}