using System;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.CallingConvention;

namespace Mjcheetham.Git.IndexViewer.Cli
{
    internal static class UnixNative
    {
        public static unsafe string GetUserName(uint uid)
        {
            passwd *pwd = getpwuid(uid);
            return Marshal.PtrToStringAnsi((IntPtr)pwd->pw_name);
        }

        public static unsafe string GetGroupName(in uint gid)
        {
            group *grp = getgrgid(gid);
            return Marshal.PtrToStringAnsi((IntPtr)grp->gr_name);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct passwd
        {
            public unsafe byte* pw_name;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct group
        {
            public unsafe byte* gr_name;
        }

        [DllImport("libc", CallingConvention = Cdecl)]
        private static extern unsafe passwd* getpwuid(uint uid);

        [DllImport("libc", CallingConvention = Cdecl)]
        private static extern unsafe group* getgrgid(uint gid);
    }
}
