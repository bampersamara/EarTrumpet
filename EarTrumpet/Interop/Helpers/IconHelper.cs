using EarTrumpet.Extensions;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EarTrumpet.Interop.Helpers
{
    public class IconHelper
    {
        public static Icon LoadSmallIcon(string path)
        {
            var dpi = WindowsTaskbar.Dpi;
            Icon icon = null;
            if (path.StartsWith("pack://"))
            {
                using (var stream = System.Windows.Application.GetResourceStream(new Uri(path)).Stream)
                {
                    icon = new Icon(stream, new Size(
                        User32.GetSystemMetricsForDpi(User32.SystemMetrics.SM_CXICON, dpi),
                        User32.GetSystemMetricsForDpi(User32.SystemMetrics.SM_CYICON, dpi)));
                }
            }
            else
            {
                var iconPath = new StringBuilder(path);
                int iconIndex = Shlwapi.PathParseIconLocationW(iconPath);
                icon = LoadIcon(iconPath.ToString(), iconIndex,
                    User32.GetSystemMetricsForDpi(User32.SystemMetrics.SM_CXSMICON, dpi),
                    User32.GetSystemMetricsForDpi(User32.SystemMetrics.SM_CYSMICON, dpi));
            }
            return icon;
        }

        public static Icon LoadIcon(string path, int iconOrdinal, int cx, int cy)
        {
            var hModule = Kernel32.LoadLibraryEx(path, IntPtr.Zero, Kernel32.LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE | Kernel32.LoadLibraryFlags.LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            try
            {
                var groupResInfo = Kernel32.FindResourceW(hModule, new IntPtr(iconOrdinal), Kernel32.RT_GROUP_ICON);
                var groupResData = Kernel32.LockResource(Kernel32.LoadResource(hModule, groupResInfo));
                var iconId = User32.LookupIconIdFromDirectoryEx(groupResData, true, cx, cy, User32.LoadImageFlags.LR_DEFAULTCOLOR);

                var iconResInfo = Kernel32.FindResourceW(hModule, new IntPtr(iconId), Kernel32.RT_ICON);
                var iconResData = Kernel32.LockResource(Kernel32.LoadResource(hModule, iconResInfo));
                var iconResSize = Kernel32.SizeofResource(hModule, iconResInfo);
                var iconHandle = User32.CreateIconFromResourceEx(iconResData, iconResSize, true, User32.IconCursorVersion.Default, cx, cy, User32.LoadImageFlags.LR_DEFAULTCOLOR);
                var icon = Icon.FromHandle(iconHandle).AsDisposableIcon();

                Trace.WriteLine($"IconHelper LoadIcon {icon?.Size.Width}x{icon?.Size.Height} {path}");
                return icon;
            }
            finally
            {
                Kernel32.FreeLibrary(hModule);
            }
        }

        public static ImageSource LoadShellIcon(string path, bool isDesktopApp, int cx, int cy)
        {
            IShellItem2 item;
            if (isDesktopApp)
            {
                item = Shell32.SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItem2).GUID);
            }
            else
            {
                item = Shell32.SHCreateItemInKnownFolder(ref FolderIds.AppsFolder, Shell32.KF_FLAG_DONT_VERIFY, path, typeof(IShellItem2).GUID);
            }

            ((IShellItemImageFactory)item).GetImage(new SIZE { cx = cx, cy = cy }, SIIGBF.SIIGBF_RESIZETOFIT, out var bmp);
            try
            {
                var ret = Imaging.CreateBitmapSourceFromHBitmap(
                    bmp,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                Trace.WriteLine($"IconHelper LoadAppIcon {cx}x{cy} {path}");
                return ret;
            }
            finally
            {
                Gdi32.DeleteObject(bmp);
            }
        }

        public static Icon ColorIcon(Icon originalIcon, double fillPercent, System.Windows.Media.Color newColor)
        {
            using (var bitmap = originalIcon.ToBitmap())
            {
                originalIcon.Dispose();

                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width * fillPercent; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);

                        if (pixel.R > 220)
                        {
                            bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(pixel.A, newColor.R, newColor.G, newColor.B));
                        }
                    }
                }

                return Icon.FromHandle(bitmap.GetHicon()).AsDisposableIcon();
            }
        }
    }
}
