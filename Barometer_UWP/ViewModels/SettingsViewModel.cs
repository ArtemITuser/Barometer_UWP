using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;
using Barometer_UWP.Services;

namespace Barometer_UWP.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly DataService _dataService;
        private readonly ExportService _exportService;
        private readonly OneDriveService _oneDriveService;
        private readonly AuthService _authService;
        private readonly UpdateService _updateService;
        private readonly ScheduleService _scheduleService;
        private readonly TileService _tileService;
        private readonly LocationService _locationService;

        // Theme properties
        private ThemeMode _selectedTheme;
        private bool _useAutoTheme;
        private string _selectedLanguage;
        private bool _enableLocalBackup;
        private bool _enableOneDriveBackup;
        private string _selectedBackupFrequency;
        private string _oneDriveStatus;
        private int _collectionFrequency;
        private bool _useNightMode;
        private string _selectedTilePeriod;
        private bool _showChartOnTile;
        private string _selectedTileStyle;

        public SettingsViewModel()
        {
            _dataService = App.Current.Services.DataService;
            _exportService = new ExportService();
            _oneDriveService = App.Current.Services.OneDriveService;
            _authService = App.Current.Services.AuthService;
            _updateService = new UpdateService();
            _scheduleService = App.Current.Services.ScheduleService;
            _tileService = new TileService();
            _locationService = new LocationService();

            // Initialize properties
            _selectedTheme = ThemeMode.Auto;
            _useAutoTheme = true;
            _selectedLanguage = "ru-RU";
            _enableLocalBackup = true;
            _enableOneDriveBackup = false;
            _selectedBackupFrequency = "Weekly";
            _oneDriveStatus = "Not signed in";
            _collectionFrequency = 60; // Default to 60 seconds
            _useNightMode = false;
            _selectedTilePeriod = "24h";
            _showChartOnTile = true;
            _selectedTileStyle = "System";

            // Initialize commands
            ExportCsvCommand = new RelayCommand(ExportCsv);
            ExportXlsxCommand = new RelayCommand(ExportXlsx);
            ExportTxtCommand = new RelayCommand(ExportTxt);
            ImportCsvCommand = new RelayCommand(ImportCsv);
            ImportXlsxCommand = new RelayCommand(ImportXlsx);
            SignInToOneDriveCommand = new RelayCommand(SignInToOneDrive);
            SignOutFromOneDriveCommand = new RelayCommand(SignOutFromOneDrive);
            CheckForUpdatesCommand = new RelayCommand(CheckForUpdates);
            BackupNowCommand = new RelayCommand(BackupNow);
            RestoreFromOneDriveCommand = new RelayCommand(RestoreFromOneDrive);
            AddScheduleCommand = new RelayCommand(AddSchedule);
            RemoveScheduleCommand = new RelayCommand(RemoveSchedule);

            // Initialize collections
            ThemeOptions = new ObservableCollection<string> { "Light", "Dark", "Auto" };
            LanguageOptions = new ObservableCollection<string> { "en-US", "ru-RU" };
            BackupFrequencyOptions = new ObservableCollection<string> { "Hourly", "Daily", "Weekly", "Monthly" };
            TilePeriodOptions = new ObservableCollection<string> { "1h", "6h", "24h", "7d" };
            TileStyleOptions = new ObservableCollection<string> { "System", "Custom" };
            ScheduleEntries = new ObservableCollection<ScheduleEntry>();
        }

        public async void Initialize()
        {
            // Update OneDrive status
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            OneDriveStatus = isAuthenticated ? "Signed in" : "Not signed in";

            // Load schedule entries
            await LoadScheduleEntries();
        }

        private async Task LoadScheduleEntries()
        {
            var entries = await _scheduleService.GetAllEntriesAsync();
            ScheduleEntries.Clear();
            foreach (var entry in entries)
            {
                ScheduleEntries.Add(entry);
            }
        }

        public async Task SaveSettingsAsync()
        {
            // Save theme settings
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["SelectedTheme"] = (int)_selectedTheme;
            settings.Values["UseAutoTheme"] = _useAutoTheme;
            settings.Values["SelectedLanguage"] = _selectedLanguage;
            settings.Values["EnableLocalBackup"] = _enableLocalBackup;
            settings.Values["EnableOneDriveBackup"] = _enableOneDriveBackup;
            settings.Values["SelectedBackupFrequency"] = _selectedBackupFrequency;
            settings.Values["CollectionFrequency"] = _collectionFrequency;
            settings.Values["UseNightMode"] = _useNightMode;
            settings.Values["SelectedTilePeriod"] = _selectedTilePeriod;
            settings.Values["ShowChartOnTile"] = _showChartOnTile;
            settings.Values["SelectedTileStyle"] = _selectedTileStyle;
        }

        #region Theme Properties
        public ThemeMode SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                _selectedTheme = value;
                OnPropertyChanged();
                ApplyTheme();
            }
        }

        public bool UseAutoTheme
        {
            get => _useAutoTheme;
            set
            {
                _useAutoTheme = value;
                OnPropertyChanged();
                if (value)
                {
                    ApplyAutoTheme();
                }
            }
        }

        public ObservableCollection<string> ThemeOptions { get; }
        public ObservableCollection<string> LanguageOptions { get; }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                ApplyLanguage();
            }
        }
        #endregion

        #region Backup Properties
        public bool EnableLocalBackup
        {
            get => _enableLocalBackup;
            set
            {
                _enableLocalBackup = value;
                OnPropertyChanged();
            }
        }

        public bool EnableOneDriveBackup
        {
            get => _enableOneDriveBackup;
            set
            {
                _enableOneDriveBackup = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> BackupFrequencyOptions { get; }

        public string SelectedBackupFrequency
        {
            get => _selectedBackupFrequency;
            set
            {
                _selectedBackupFrequency = value;
                OnPropertyChanged();
            }
        }

        public string OneDriveStatus
        {
            get => _oneDriveStatus;
            private set
            {
                _oneDriveStatus = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Collection Properties
        public int CollectionFrequency
        {
            get => _collectionFrequency;
            set
            {
                _collectionFrequency = value;
                OnPropertyChanged();
            }
        }

        public bool UseNightMode
        {
            get => _useNightMode;
            set
            {
                _useNightMode = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ScheduleEntry> ScheduleEntries { get; }
        #endregion

        #region Tile Properties
        public ObservableCollection<string> TilePeriodOptions { get; }

        public string SelectedTilePeriod
        {
            get => _selectedTilePeriod;
            set
            {
                _selectedTilePeriod = value;
                OnPropertyChanged();
            }
        }

        public bool ShowChartOnTile
        {
            get => _showChartOnTile;
            set
            {
                _showChartOnTile = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> TileStyleOptions { get; }

        public string SelectedTileStyle
        {
            get => _selectedTileStyle;
            set
            {
                _selectedTileStyle = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Commands
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportXlsxCommand { get; }
        public ICommand ExportTxtCommand { get; }
        public ICommand ImportCsvCommand { get; }
        public ICommand ImportXlsxCommand { get; }
        public ICommand SignInToOneDriveCommand { get; }
        public ICommand SignOutFromOneDriveCommand { get; }
        public ICommand CheckForUpdatesCommand { get; }
        public ICommand BackupNowCommand { get; }
        public ICommand RestoreFromOneDriveCommand { get; }
        public ICommand AddScheduleCommand { get; }
        public ICommand RemoveScheduleCommand { get; }
        #endregion

        #region Command Implementations
        private async void ExportCsv(object parameter)
        {
            await _exportService.ExportCsvAsync(null, await _dataService.LoadAsync(), CultureInfo.CurrentUICulture);
        }

        private async void ExportXlsx(object parameter)
        {
            await _exportService.ExportXlsxAsync(null, await _dataService.LoadAsync(), CultureInfo.CurrentUICulture);
        }

        private async void ExportTxt(object parameter)
        {
            await _exportService.ExportTxtAsync(null, await _dataService.LoadAsync(), CultureInfo.CurrentUICulture);
        }

        private async void ImportCsv(object parameter)
        {
            await _exportService.ImportCsvAsync(null, CultureInfo.CurrentUICulture);
        }

        private async void ImportXlsx(object parameter)
        {
            await _exportService.ImportXlsxAsync(null);
        }

        private async void SignInToOneDrive(object parameter)
        {
            var success = await _authService.SignInAsync();
            if (success)
            {
                OneDriveStatus = "Signed in";
            }
        }

        private async void SignOutFromOneDrive(object parameter)
        {
            await _authService.SignOutAsync();
            OneDriveStatus = "Not signed in";
        }

        private async void CheckForUpdates(object parameter)
        {
            await _updateService.CheckForUpdatesAsync();
        }

        private async void BackupNow(object parameter)
        {
            // Perform backup to local storage
            if (EnableLocalBackup)
            {
                await _dataService.SaveAsync();
            }

            // Perform backup to OneDrive if enabled
            if (EnableOneDriveBackup)
            {
                var isAuthenticated = await _authService.IsAuthenticatedAsync();
                if (isAuthenticated)
                {
                    var data = await _dataService.LoadAsync();
                    var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    var jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                    await _oneDriveService.UploadBackupAsync(fileName, System.Text.Encoding.UTF8.GetBytes(jsonData));
                }
            }
        }

        private async void RestoreFromOneDrive(object parameter)
        {
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            if (isAuthenticated)
            {
                var backups = await _oneDriveService.ListBackupsAsync();
                if (backups.Count > 0)
                {
                    // For simplicity, restore the most recent backup
                    var latestBackup = backups[backups.Count - 1];
                    var data = await _oneDriveService.DownloadBackupAsync(latestBackup);
                    var jsonString = System.Text.Encoding.UTF8.GetString(data);
                    var records = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<PressureRecord>>(jsonString);
                    
                    await _dataService.ClearAsync();
                    foreach (var record in records)
                    {
                        await _dataService.AddAsync(record);
                    }
                }
            }
        }

        private void AddSchedule(object parameter)
        {
            // For simplicity, add a default schedule entry
            var newEntry = new ScheduleEntry
            {
                Id = Guid.NewGuid(),
                Interval = TimeSpan.FromMinutes(30),
                Start = TimeSpan.FromHours(8), // 8 AM
                End = TimeSpan.FromHours(22),   // 10 PM
                Enabled = true
            };
            ScheduleEntries.Add(newEntry);
        }

        private void RemoveSchedule(object parameter)
        {
            // For simplicity, remove the last entry
            if (ScheduleEntries.Count > 0)
            {
                ScheduleEntries.RemoveAt(ScheduleEntries.Count - 1);
            }
        }
        #endregion

        #region Helper Methods
        private void ApplyTheme()
        {
            // Apply the selected theme to the application
            switch (_selectedTheme)
            {
                case ThemeMode.Light:
                    Application.Current.RequestedTheme = ApplicationTheme.Light;
                    break;
                case ThemeMode.Dark:
                    Application.Current.RequestedTheme = ApplicationTheme.Dark;
                    break;
                case ThemeMode.Auto:
                    // Auto theme is handled separately
                    break;
            }
        }

        private async void ApplyAutoTheme()
        {
            // Calculate sunrise/sunset based on location and apply theme accordingly
            var location = await _locationService.GetLocationAsync();
            if (location != null)
            {
                var sunriseSunset = await _locationService.GetSunriseSunsetAsync(location.Latitude, location.Longitude, DateTime.Now);
                var now = DateTime.Now.TimeOfDay;

                if (now < sunriseSunset.Sunrise.TimeOfDay || now > sunriseSunset.Sunset.TimeOfDay)
                {
                    // Night time - use dark theme
                    Application.Current.RequestedTheme = ApplicationTheme.Dark;
                }
                else
                {
                    // Day time - use light theme
                    Application.Current.RequestedTheme = ApplicationTheme.Light;
                }
            }
        }

        private void ApplyLanguage()
        {
            // Apply the selected language to the application
            ApplicationLanguages.PrimaryLanguageOverride = _selectedLanguage;
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            _ = SaveSettingsAsync(); // Save settings when disposed
        }
    }
}