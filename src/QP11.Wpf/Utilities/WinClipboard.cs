using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace QP11.Wpf.Utilities;

/// <summary>Win32剪贴板封装，避免WPF OLE剪贴板超时卡顿</summary>
internal static class WinClipboard
{
    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();
    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();
    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 2;

    public static void SetText(string text)
    {
        for (int i = 0; i < 3; i++)
        {
            if (TrySetText(text)) return;
            Thread.Sleep(50);
        }
    }

    private static bool TrySetText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero)) return false;
        try
        {
            EmptyClipboard();
            var bytes = (text.Length + 1) * 2;
            var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hMem == IntPtr.Zero) return false;
            var ptr = GlobalLock(hMem);
            if (ptr == IntPtr.Zero) return false;
            try
            {
                Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                Marshal.WriteInt16(ptr, text.Length * 2, 0);
            }
            finally { GlobalUnlock(hMem); }
            if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero) return false;
            return true;
        }
        finally { CloseClipboard(); }
    }
}
