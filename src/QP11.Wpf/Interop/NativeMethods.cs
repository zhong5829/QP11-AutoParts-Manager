using System;
using System.Runtime.InteropServices;

namespace QP11.Wpf.Interop;

/// <summary>Win32 原生 API 调用</summary>
internal static class NativeMethods
{
    private const string User32 = "user32.dll";

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int MessageBoxW(
        IntPtr hWnd,
        [MarshalAs(UnmanagedType.LPWStr)] string text,
        [MarshalAs(UnmanagedType.LPWStr)] string caption,
        uint type);
}
