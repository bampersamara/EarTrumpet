using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace EarTrumpet.UI.Controls
{
    class ImageEx : Image
    {
        public IconLoadInfo SourceEx { get => (IconLoadInfo)GetValue(SourceExProperty); set => SetValue(SourceExProperty, value); }
        public static readonly DependencyProperty SourceExProperty = DependencyProperty.Register(
          "SourceEx", typeof(IconLoadInfo), typeof(ImageEx), new PropertyMetadata(null, new PropertyChangedCallback(OnSourceExChanged)));

        private uint _dpi;

        public ImageEx()
        {
            DpiChanged += OnDpiChanged;
        }

        private void OnDpiChanged(object sender, DpiChangedEventArgs e)
        {
            var nextDpi = GetWindowDpi();
            if (nextDpi != _dpi)
            {
                _dpi = nextDpi;
                OnSourceExChanged();
            }
        }

        private void OnSourceExChanged()
        {
            if (SourceEx != null)
            {
                Source = GetIconFromFileImpl(SourceEx.IconPath, SourceEx.IsDesktopApp);
            }
        }

        private ImageSource GetIconFromFileImpl(string path, bool isDesktopApp)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            ImageSource ret = null;
            try
            {
                var scale = GetWindowDpi() / (double)96;

                var iconPath = new StringBuilder(path);
                int iconIndex = Shlwapi.PathParseIconLocationW(iconPath);
                if (iconIndex != 0)
                {
                    ret = IconHelper.LoadIcon(iconPath.ToString(), iconIndex, (int)(Width * scale), (int)(Height * scale)).ToImageSource();
                }
                else
                {
                    ret = IconHelper.LoadShellIcon(path, isDesktopApp, (int)(Width * scale), (int)(Height * scale));
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to load icon: {ex}");
            }
            return ret;
        }

        private uint GetWindowDpi() => User32.GetDpiForWindow(new WindowInteropHelper(Window.GetWindow(this)).Handle);
        private static void OnSourceExChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ImageEx)d).OnSourceExChanged();
    }
}
