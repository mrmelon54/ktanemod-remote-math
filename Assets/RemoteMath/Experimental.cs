using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace RemoteMath
{
    public static class Experimental
    {
        public static Action UnmanagedExternalMethod(string path, string method)
        {
			Debug.Log("Loading "+path+" @ "+method+"...");
            var dynamicAsm = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName() { Name = method }, AssemblyBuilderAccess.Run);

            var dynamicMod = dynamicAsm.DefineDynamicModule(method);

            dynamicMod.DefinePInvokeMethod(
                method,
                path,
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.PinvokeImpl,
                CallingConventions.Standard,
                typeof(void),
                Type.EmptyTypes,
                CallingConvention.Cdecl,
                CharSet.Ansi);

            dynamicMod.CreateGlobalFunctions();

            MethodInfo mi = dynamicMod.GetMethod(method);
            return (Action) Delegate.CreateDelegate(typeof(Action), mi);
        }
    }
}