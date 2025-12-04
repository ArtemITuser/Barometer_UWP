using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.System.UserProfile;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;
using Barometer_UWP.Services;

namespace Barometer_UWP.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private ThemeMode _themeMode;
        private string _selectedLanguage;
        private bool _autoBackupEnabled;
        private bool _oneDriveBackupEnabled;
        private string _selectedTilePeriod;
        private bool _tileGraphEnabled;
        private readonly LocationService _locationService;
        private readonly AuthService _authService;
        private readonly OneDriveService _oneDriveService;

        public SettingsViewModel()
        {
            _themeMode = ThemeMode.Auto;
            _selectedLanguage = "ru-RU";
            _autoBackupEnabled = true;
            _oneDriveBackupEnabled = false;
            _selectedTilePeriod = "24h";
            _tileGraphEnabled = true;
            
            _locationService = new LocationService();
            _authService = new AuthService();
            _oneDriveService = new OneDriveService(_authService);
            
            InitializeAsync();
            
            // Initialize commands
            SignInToOneDriveCommand = new RelayCommand(SignInToOneDrive);
            SignOutFromOneDriveCommand = new RelayCommand(SignOutFromOneDrive);
            ExecuteBackupCommand = new RelayCommand(ExecuteBackup);
        }

        private async void InitializeAsync()
        {
            await _locationService.RequestLocationPermissionAsync();
        }

        public ThemeMode ThemeMode
        {
            get => _themeMode;
            set
            {
                _themeMode = value;
                OnPropertyChanged();
                
                // Apply theme change
                ApplyTheme();
            }
        }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                
                // Set the application language
                ApplicationLanguages.PrimaryLanguageOverride = _selectedLanguage;
            }
        }

        public bool AutoBackupEnabled
        {
            get => _autoBackupEnabled;
            set
            {
                _autoBackupEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool OneDriveBackupEnabled
        {
            get => _oneDriveBackupEnabled;
            set
            {
                _oneDriveBackupEnabled = value;
                OnPropertyChanged();
            }
        }

        public string SelectedTilePeriod
        {
            get => _selectedTilePeriod;
            set
            {
                _selectedTilePeriod = value;
                OnPropertyChanged();
            }
        }

        public bool TileGraphEnabled
        {
            get => _tileGraphEnabled;
            set
            {
                _tileGraphEnabled = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AvailableLanguages { get; } = new ObservableCollection<string>
        {
            "ru-RU",
            "en-US"
        };

        public ObservableCollection<string> TilePeriods { get; } = new ObservableCollection<string>
        {
            "1h", "6h", "24h", "7d"
        };

        public ICommand SignInToOneDriveCommand { get; }
        public ICommand SignOutFromOneDriveCommand { get; }
        public ICommand ExecuteBackupCommand { get; }

        private async void SignInToOneDrive(object parameter)
        {
            var success = await _authService.SignInAsync();
            if (success)
            {
                OneDriveBackupEnabled = true;
            }
        }

        private async void SignOutFromOneDrive(object parameter)
        {
            await _authService.SignOutAsync();
            OneDriveBackupEnabled = false;
        }

        private async void ExecuteBackup(object parameter)
        {
            var backupTask = new BackgroundTasks.BackupTask();
            // This would trigger a backup, but for now just call it directly
            // In a real scenario, this would be a background task registration
        }

        private void ApplyTheme()
        {
            // Apply theme based on ThemeMode
            switch (_themeMode)
            {
                case ThemeMode.Light:
                    // Set light theme
                    break;
                case ThemeMode.Dark:
                    // Set dark theme
                    break;
                case ThemeMode.Auto:
                    // Set theme based on sunrise/sunset
                    break;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}