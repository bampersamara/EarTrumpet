using EarTrumpet.Extensions;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EarTrumpet.Interop.Helpers
{
    class WindowSizeHelper
    {
        public static void RestrictSizeToWorkArea(Window window)
        {
            ((HwndSource)PresentationSource.FromVisual(window)).AddHook(WndProc);
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case User32.WM_GETMINMAXINFO:
                    HandleGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        }

        private static void HandleGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var monitor = User32.MonitorFromWindow(hwnd, User32.MONITOR_DEFAULT.MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO { Size = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (User32.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var window = (Window)HwndSource.FromHwnd(hwnd).RootVisual;
                    var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    minMaxInfo.MaxPosition = new POINT { X = 0, Y = 0 };
                    minMaxInfo.MaxSize = new POINT { X = monitorInfo.WorkArea.Width(), Y = monitorInfo.WorkArea.Height() };
                    minMaxInfo.MinTrackSize = new POINT { X = (int)(window.MinWidth * window.DpiWidthFactor()), Y = (int)(window.MinHeight * window.DpiHeightFactor()) };

                    Marshal.StructureToPtr(minMaxInfo, lParam, true);
                }
            }
        }
    }
}
