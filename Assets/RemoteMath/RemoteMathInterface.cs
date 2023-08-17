using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using KeepCoding;

namespace RemoteMath
{
    public static class RemoteMathInterface
    {
        public static void Entry(bool useProdAddressInDev)
        {
            string asmPath = GetAssemblyPath("ktanemod-remote-math-interface");
            if (Application.isEditor)
            {
                Experimental.UnmanagedExternalMethod(asmPath, "RemoteMathIsEditor").Invoke();
                if (useProdAddressInDev)
                    Experimental.UnmanagedExternalMethod(asmPath, "RemoteMathNotEditor").Invoke();
            }

            Experimental.UnmanagedExternalMethod(asmPath, "RemoteMathInterfaceEntry").Invoke();
        }

        // Get CPU architecture
        private static String GetArch()
        {
            string arch;
            bool _64;

            switch (IntPtr.Size)
            {
                case 4:
                    _64 = false;
                    break;
                case 8:
                    _64 = true;
                    break;
                default:
                    throw new PlatformNotSupportedException(
                        "IntPtr's size is not 4 or 8. Only 32-bit and 64-bit devices are supported.");
            }

            if (CultureInfo.InvariantCulture.CompareInfo.IndexOf(SystemInfo.processorType, "ARM",
                    CompareOptions.IgnoreCase) >= 0)
                arch = _64 ? "arm64" : "arm";
            else
                arch = _64 ? "amd64" : "386";

            return arch;
        }

        // Windows: <path>/dlls/<file>-windows-<arch>.dll
        // OSX: <path>/dlls/<file>-darwin-<arch>.dylib
        // Linux: <path>/dlls/lib<file>-linux-<arch>.so
        private static String GetAssemblyPath(String file)
        {
            bool editor = Application.isEditor;
            string path = editor ? Path.Combine(Application.dataPath, "Plugins") : PathManager.GetDirectory();
            const string target = "dlls";
            string arch = GetArch();

            string p;
            switch (Application.platform)
            {
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    p = string.Format("{0}/{1}/lib{2}-linux-{3}.{4}", path, target, file, arch, "so");
                    break;
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    p = string.Format(@"{0}\{1}\{2}-windows-{3}.{4}", path, target, file, arch, "dll");
                    break;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    p = string.Format("{0}/{1}/{2}-darwin-{3}.{4}", path, target, file, arch, "dylib");
                    break;
                default:
                    throw new PlatformNotSupportedException("The platform \"" + Application.platform +
                                                            "\" is unsupported. The operating systems supported are Windows, Mac, and Linux.");
            }

            Debug.Log("Loading in editor: " + Application.isEditor);
            Debug.Log("Path: " + path);
            Debug.Log("Using DLL: " + p);

            if (!File.Exists(p))
            {
                throw new PlatformNotSupportedException("The platform \"" + Application.platform +
                                                        "\" does not support the architecture \"" + arch +
                                                        "\", please notify the developer.");
            }

            return p;
        }
    }
}