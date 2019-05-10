﻿using EarTrumpet.DataModel.Storage;
using EarTrumpet.DataModel.WindowsAudio;
using EarTrumpet.Diagnosis;
using EarTrumpet.Extensibility.Hosting;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.Services;
using EarTrumpet.UI.Themes;
using EarTrumpet.UI.Tray;
using EarTrumpet.UI.ViewModels;
using EarTrumpet.UI.Views;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace EarTrumpet
{
    public partial class App
    {
        public FlyoutViewModel FlyoutViewModel { get; private set; }
        public TrayViewModel TrayViewModel { get; private set; }
        public FlyoutWindow FlyoutWindow { get; private set; }
        public DeviceCollectionViewModel PlaybackDevicesViewModel { get; private set; }
        public bool IsShuttingDown { get; private set; }

        private TrayIcon _trayIcon;
        private WindowHolder _mixerWindow;
        private WindowHolder _settingsWindow;
        private ErrorReporter _errorReporter;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Must be first to register
            App.Current.Exit += (_, __) => IsShuttingDown = true;

            _errorReporter = new ErrorReporter();

            Trace.WriteLine("App Application_Startup");

            if (!SingleInstanceAppMutex.TakeExclusivity())
            {
                Trace.WriteLine("App Application_Startup TakeExclusivity failed");
                Current.Shutdown();
                return;
            }

            DoAppStartup();
            Trace.WriteLine($"App Application_Startup Exit");
        }

        private void DoAppStartup()
        {
            ((Manager)Resources["ThemeManager"]).Load();

            _mixerWindow = new WindowHolder(CreateMixerExperience);
            _settingsWindow = new WindowHolder(CreateSettingsExperience);

            PlaybackDevicesViewModel = new DeviceCollectionViewModel(WindowsAudioFactory.Create(AudioDeviceKind.Playback));
            PlaybackDevicesViewModel.Ready += OnMainViewModelReady;

            FlyoutViewModel = new FlyoutViewModel(PlaybackDevicesViewModel);
            FlyoutViewModel.StateChanged += OnFlyoutViewModelStateChanged;
            FlyoutWindow = new FlyoutWindow(FlyoutViewModel);

            TrayViewModel = new TrayViewModel(PlaybackDevicesViewModel);
            TrayViewModel.LeftClick = new RelayCommand(() => FlyoutViewModel.OpenFlyout(InputType.Mouse));
            TrayViewModel.OpenMixer = new RelayCommand(_mixerWindow.OpenOrBringToFront);
            TrayViewModel.OpenSettings = new RelayCommand(_settingsWindow.OpenOrBringToFront);
            FlyoutWindow.DpiChanged += (_, __) => TrayViewModel.Refresh();

            _trayIcon = new TrayIcon(TrayViewModel);

            SettingsService.RegisterHotkeys();
            HotkeyManager.Current.KeyPressed += OnHotKeyPressed;

            MaybeShowFirstRunExperience();
        }

        private void OnFlyoutViewModelStateChanged(object sender, object e)
        {
            if (FlyoutViewModel.State == FlyoutViewModel.ViewState.Closing_Stage1 &&
                FlyoutViewModel.LastInput == InputType.Keyboard &&
                !FlyoutViewModel.IsExpandingOrCollapsing)
            {
                _trayIcon.SetFocus();
            }
        }

        private void OnHotKeyPressed(HotkeyData hotkey)
        {
            if (hotkey.Equals(SettingsService.FlyoutHotkey))
            {
                FlyoutViewModel.OpenFlyout(InputType.Keyboard);
            }
            else if (hotkey.Equals(SettingsService.SettingsHotkey))
            {
                _settingsWindow.OpenOrBringToFront();
            }
            else if (hotkey.Equals(SettingsService.MixerHotkey))
            {
                _mixerWindow.OpenOrClose();
            }
        }

        private void OnMainViewModelReady(object sender, System.EventArgs e)
        {
            Trace.WriteLine("App MainViewModel_Ready");
            _trayIcon.IsVisible = true;
            Trace.WriteLine("App MainViewModel_Ready Before Load");
            AddonManager.Current.Load();
            Trace.WriteLine("App MainViewModel_Ready After Load");
        }

        private void MaybeShowFirstRunExperience()
        {
            const string firstRunKey = "hasShownFirstRun";
            var settings = StorageFactory.GetSettings();

            if (!settings.HasKey(firstRunKey))
            {
                Trace.WriteLine($"App Application_Startup Showing welcome dialog");
                settings.Set(firstRunKey, true);

                var dialog = new DialogWindow();
                var viewModel = new WelcomeViewModel();
                dialog.DataContext = viewModel;
                viewModel.Close = new RelayCommand(() => dialog.SafeClose());
                dialog.Show();
            }
        }

        private Window CreateMixerExperience()
        {
            var viewModel = new FullWindowViewModel(PlaybackDevicesViewModel);
            var window = new FullWindow { DataContext = viewModel };
            window.Closing += (_, __) =>
            {
                _mixerWindow.Destroyed();
                viewModel.Close();
            };
            return window;
        }

        private Window CreateSettingsExperience()
        {
            var defaultCategory = new SettingsCategoryViewModel(EarTrumpet.Properties.Resources.SettingsCategoryTitle, "\xE71D",
                EarTrumpet.Properties.Resources.SettingsDescriptionText,
                null,
                new SettingsPageViewModel[] {
                        new EarTrumpetShortcutsPageViewModel(),
                        new EarTrumpetLegacySettingsPageViewModel(),
                        new EarTrumpetAboutPageViewModel(_errorReporter.DisplayDiagnosticData)
                }.ToList());

            var allCategories = new List<SettingsCategoryViewModel>();
            allCategories.Add(defaultCategory);
            if (SettingsViewModel.AddonItems != null)
            {
                allCategories.AddRange(SettingsViewModel.AddonItems.Select(a => a.Get()));
            }

            bool canClose = false;
            var viewModel = new SettingsViewModel(EarTrumpet.Properties.Resources.SettingsWindowText, allCategories);
            var window = new SettingsWindow();
            window.CloseClicked += () => viewModel.OnClosing();
            viewModel.Close += () =>
            {
                canClose = true;
                window.SafeClose();
            };
            window.DataContext = viewModel;
            window.Closing += (_, e) =>
            {
                if (canClose)
                {
                    _settingsWindow.Destroyed();
                }
                else
                {
                    e.Cancel = true;
                    viewModel.OnClosing();
                }
            };
            return window;
        }
    }
}