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
    /// <summary>
    /// Settings page view model (v1.4.0.1). Rewritten against the RC1 service
    /// signatures: shared singletons from App.Current.Services, DataService local
    /// backups, AuthService.IsSignedIn / SignInAsync():Task&lt;bool&gt;,
    /// ScheduleService with ScheduleEntryInfo, OneDriveService name-based API.
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DataService _dataService;
        private readonly ExportService _exportService;
        private readonly OneDriveService _oneDriveService;
        private readonly AuthService _authService;
        private readonly UpdateService _updateService;
        private readonly ScheduleService _scheduleService;
        private readonly TileService _tileService;
        private readonly LocationService _locationService;

        // Theme
        private ThemeMode _selectedTheme = ThemeMode.Auto;
        private bool _useAutoTheme = true;
        private string _selectedLanguage = "ru-RU";

        // Backup
        private bool _enableLocalBackup = true;
        private bool _enableOneDriveBackup;
        private string _selectedBackupFrequency = "Daily";
        private string _oneDriveStatus = "Not signed in";

        // Collection schedule
        private int _collectionFrequency = 60;
        private bool _useNightMode;

        // Tile
        private string _selectedTilePeriod = "24h";
        private bool _showChartOnTile = true;
        private string _selectedTileStyle = "System";

        private bool _disposed;

        public SettingsViewModel()
        {
            var services = App.Current?.Services;
            _dataService = services?.DataService ?? new DataService();
            _exportService = services?.ExportService ?? new ExportService();
            _oneDriveService = services?.OneDriveService;
            _authService = services?.AuthService;
            _updateService = services?.UpdateService ?? new UpdateService();
            _scheduleService = services?.ScheduleService ?? new ScheduleService();
            _tileService = services?.TileService ?? new TileService();
            _locationService = services?.LocationService ?? new LocationService();

            if (_authService != null) _authService.AuthStateChanged += OnAuthStateChanged;
            if (_scheduleService != null) _scheduleService.Changed += OnScheduleChanged;

            LoadFromSettings();

            ExportCsvCommand = new RelayCommand(async _ => await ExportAsync("csv"));
            ExportXlsxCommand = new RelayCommand(async _ => await ExportAsync("xlsx"));
            ExportTxtCommand = new RelayCommand(async _ => await ExportAsync("txt"));
            ImportCsvCommand = new RelayCommand(async _ => await ImportAsync("csv"));
            ImportXlsxCommand = new RelayCommand(async _ => await ImportAsync("xlsx"));
            SignInToOneDriveCommand = new RelayCommand(async _ => await SignInAsync());
            SignOutFromOneDriveCommand = new RelayCommand(_ => SignOut());
            CheckForUpdatesCommand = new RelayCommand(async _ => await CheckForUpdatesAsync());
            BackupNowCommand = new RelayCommand(async _ => await BackupNowAsync());
            RestoreFromOneDriveCommand = new RelayCommand(async _ => await RestoreFromOneDriveAsync());
            AddScheduleCommand = new RelayCommand(_ => AddSchedule());
            RemoveScheduleCommand = new RelayCommand(p => RemoveSchedule(p));

            ThemeOptions = new ObservableCollection<string> { "Light", "Dark", "Auto" };
            LanguageOptions = new ObservableCollection<string> { "en-US", "ru-RU" };
            BackupFrequencyOptions = new ObservableCollection<string> { "Hourly", "Daily", "Weekly", "Monthly" };
            TilePeriodOptions = new ObservableCollection<string> { "1h", "6h", "24h", "7d" };
            TileStyleOptions = new ObservableCollection<string> { "System", "Custom" };
            ScheduleEntries = new ObservableCollection<ScheduleEntryInfo>();
            ReloadScheduleList();
        }

        // ---- persistence ------------------------------------------------------

        private static Windows.Storage.ApplicationDataContainer Local =>
            Windows.Storage.ApplicationData.Current.LocalSettings;

        private void LoadFromSettings()
        {
            try
            {
                object v;
                if (Local.Values.TryGetValue("SelectedTheme", out v) && v is int ti)
                    _selectedTheme = (ThemeMode)ti;
                if (Local.Values.TryGetValue("UseAutoTheme", out v) && v is bool aub)
                    _useAutoTheme = aub;
                if (Local.Values.TryGetValue("SelectedLanguage", out v) && v is string lgs)
                    _selectedLanguage = lgs;
                if (Local.Values.TryGetValue("EnableLocalBackup", out v) && v is bool lbb)
                    _enableLocalBackup = lbb;
                if (Local.Values.TryGetValue("EnableOneDriveBackup", out v) && v is bool obb)
                    _enableOneDriveBackup = obb;
                if (Local.Values.TryGetValue("SelectedBackupFrequency", out v) && v is string bfs)
                    _selectedBackupFrequency = bfs;
                if (Local.Values.TryGetValue("CollectionFrequency", out v) && v is int cfi)
                    _collectionFrequency = cfi;
                if (Local.Values.TryGetValue("UseNightMode", out v) && v is bool nmb)
                    _useNightMode = nmb;
                if (Local.Values.TryGetValue("SelectedTilePeriod", out v) && v is string tps)
                    _selectedTilePeriod = tps;
                if (Local.Values.TryGetValue("ShowChartOnTile", out v) && v is bool scb)
                    _showChartOnTile = scb;
                if (Local.Values.TryGetValue("SelectedTileStyle", out v) && v is string tss)
                    _selectedTileStyle = tss;
            }
            catch { }
        }

        public Task SaveSettingsAsync()
        {
            try
            {
                Local.Values["SelectedTheme"] = (int)_selectedTheme;
                Local.Values["UseAutoTheme"] = _useAutoTheme;
                Local.Values["SelectedLanguage"] = _selectedLanguage;
                Local.Values["EnableLocalBackup"] = _enableLocalBackup;
                Local.Values["EnableOneDriveBackup"] = _enableOneDriveBackup;
                Local.Values["SelectedBackupFrequency"] = _selectedBackupFrequency;
                Local.Values["CollectionFrequency"] = _collectionFrequency;
                Local.Values["UseNightMode"] = _useNightMode;
                Local.Values["SelectedTilePeriod"] = _selectedTilePeriod;
                Local.Values["ShowChartOnTile"] = _showChartOnTile;
                Local.Values["SelectedTileStyle"] = _selectedTileStyle;
            }
            catch { }
            return Task.CompletedTask;
        }

        public void Initialize()
        {
            RefreshAuthStatus();
        }

        // ---- properties ---------------------------------------------------------

        #region Theme Properties
        public ObservableCollection<string> ThemeOptions { get; }
        public ObservableCollection<string> LanguageOptions { get; }

        public ThemeMode SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                _selectedTheme = value;
                OnPropertyChanged();
                ApplyTheme();
                _ = SaveSettingsAsync();
            }
        }

        public bool UseAutoTheme
        {
            get => _useAutoTheme;
            set
            {
                _useAutoTheme = value;
                OnPropertyChanged();
                _ = SaveSettingsAsync();
                if (value) ApplyAutoTheme();
            }
        }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                ApplyLanguage();
                _ = SaveSettingsAsync();
            }
        }
        #endregion

        #region Backup Properties
        public bool EnableLocalBackup
        {
            get => _enableLocalBackup;
            set { _enableLocalBackup = value; OnPropertyChanged(); _ = SaveSettingsAsync(); }
        }

        public bool EnableOneDriveBackup
        {
            get => _enableOneDriveBackup;
            set { _enableOneDriveBackup = value; OnPropertyChanged(); _ = SaveSettingsAsync(); }
        }

        public ObservableCollection<string> BackupFrequencyOptions { get; }

        public string SelectedBackupFrequency
        {
            get => _selectedBackupFrequency;
            set { _selectedBackupFrequency = value; OnPropertyChanged(); _ = SaveSettingsAsync(); }
        }

        public string OneDriveStatus
        {
            get => _oneDriveStatus;
            private set { _oneDriveStatus = value; OnPropertyChanged(); }
        }
        #endregion

        #region Collection Properties
        public int CollectionFrequency
        {
            get => _collectionFrequency;
            set { _collectionFrequency = value; OnPropertyChanged(); _ = SaveSettingsAsync(); }
        }

        public bool UseNightMode
        {
            get => _useNightMode;
            set { _useNightMode = value; OnPropertyChanged(); _ = SaveSettingsAsync(); }
        }

        public ObservableCollection<ScheduleEntryInfo> ScheduleEntries { get; }

        private ScheduleEntryInfo _selectedScheduleEntry;
        public ScheduleEntryInfo SelectedScheduleEntry
        {
            get => _selectedScheduleEntry;
            set { _selectedScheduleEntry = value; OnPropertyChanged(); }
        }
        #endregion

        #region Tile Properties
        public ObservableCollection<string> TilePeriodOptions { get; }

        public string SelectedTilePeriod
        {
            get => _selectedTilePeriod;
            set { _selectedTilePeriod = value; OnPropertyChanged(); _ = SaveSettingsAsync(); }
        }

        public bool ShowChartOnTile
        {
            get => _showChartOnTile;
            set { _showChartOnTile = value; OnPropertyChanged(); _ = SaveSettingsAsync(); }
        }

        public ObservableCollection<string> TileStyleOptions { get; }

        public string SelectedTileStyle
        {
            get => _selectedTileStyle;
            set { _selectedTileStyle = value; OnPropertyChanged(); _ = SaveSettingsAsync(); }
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

        #region Command implementations
        private async Task ExportAsync(string kind)
        {
            var records = _dataService.GetAll();
            switch (kind)
            {
                case "csv":
                    await _exportService.ExportCsvAsync(null, records, CultureInfo.CurrentUICulture);
                    break;
                case "txt":
                    await _exportService.ExportTxtAsync(null, records, CultureInfo.CurrentUICulture);
                    break;
                default:
                    await _exportService.ExportXlsxAsync(null, records, CultureInfo.CurrentUICulture);
                    break;
            }
        }

        private async Task ImportAsync(string kind)
        {
            if (kind == "csv")
                await _exportService.ImportCsvAsync(null, CultureInfo.CurrentUICulture);
            else
                await _exportService.ImportXlsxAsync(null);
        }

        private async Task SignInAsync()
        {
            if (_authService == null) return;
            bool ok = await _authService.SignInAsync();
            OneDriveStatus = ok ? "Signed in" : "Sign-in failed";
        }

        private void SignOut()
        {
            _authService?.SignOut();
            OneDriveStatus = "Not signed in";
        }

        private void OnAuthStateChanged()
        {
            var d = Windows.UI.Xaml.Window.Current?.Dispatcher;
            if (d != null && !d.HasThreadAccess)
            {
                _ = d.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, RefreshAuthStatus);
                return;
            }
            RefreshAuthStatus();
        }

        private void RefreshAuthStatus()
        {
            OneDriveStatus = (_authService != null && _authService.IsSignedIn)
                ? "Signed in" : "Not signed in";
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var release = await _updateService.GetLatestReleaseAsync();
                if (release != null)
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(release.Url));
            }
            catch { }
        }

        private async Task BackupNowAsync()
        {
            byte[] bytes = null;
            string name = null;

            if (_enableLocalBackup)
            {
                var res = await _dataService.CreateLocalBackupAsync();
                if (res != null) { name = res.Name; bytes = res.Bytes; }
            }
            else if (_enableOneDriveBackup)
            {
                await _dataService.SaveAsync();
                var file = await Windows.Storage.ApplicationData.Current.LocalFolder
                    .GetFileAsync(DataService.FileName);
                var json = await Windows.Storage.FileIO.ReadTextAsync(file);
                name = DataService.MakeBackupFileName(DateTime.UtcNow);
                bytes = System.Text.Encoding.UTF8.GetBytes(json);
            }

            if (bytes != null && _enableOneDriveBackup &&
                _oneDriveService != null && _oneDriveService.IsAvailable)
            {
                await _oneDriveService.UploadBackupAsync(name, bytes);
            }
        }

        private async Task RestoreFromOneDriveAsync()
        {
            if (_oneDriveService == null || !_oneDriveService.IsAvailable) return;
            var backups = await _oneDriveService.ListBackupsAsync();
            if (backups.Count == 0) return;
            var latest = backups[backups.Count - 1];
            var data = await _oneDriveService.DownloadBackupAsync(latest.Name);
            var json = System.Text.Encoding.UTF8.GetString(data, 0, data.Length);
            await _dataService.RestoreFromJsonAsync(json);
        }

        private void AddSchedule()
        {
            _scheduleService.Add(new ScheduleEntryInfo
            {
                StartMinutes = 8 * 60,
                EndMinutes = 22 * 60 - 1,
                IntervalSeconds = Math.Max(5, _collectionFrequency),
                Enabled = true
            });
        }

        private void RemoveSchedule(object parameter)
        {
            var entry = parameter as ScheduleEntryInfo ?? SelectedScheduleEntry;
            if (entry != null) _scheduleService.Remove(entry.Id);
            else if (ScheduleEntries.Count > 0)
                _scheduleService.Remove(ScheduleEntries[ScheduleEntries.Count - 1].Id);
        }

        private void OnScheduleChanged()
        {
            var d = Windows.UI.Xaml.Window.Current?.Dispatcher;
            if (d != null && !d.HasThreadAccess)
            {
                _ = d.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, ReloadScheduleList);
                return;
            }
            ReloadScheduleList();
        }

        private void ReloadScheduleList()
        {
            ScheduleEntries.Clear();
            foreach (var e in _scheduleService.GetEntries())
                ScheduleEntries.Add(e);
        }
        #endregion

        #region Helper methods
        private void ApplyTheme()
        {
            // RequestedTheme is fixed at launch; the choice is persisted and applied
            // by ThemeHelper on the next app start.
            _ = SaveSettingsAsync();
        }

        private async void ApplyAutoTheme()
        {
            // BUGFIX v1.4.0.3: раньше «карта неба»/авто-тема использовала приближённый
            // расчёт восхода-заката без учёта долготы и часового пояса (ошибка до |lon|/15 ч),
            // а сравнение TimeOfDay давало неверную ночь при переходе через полночь
            // (например, 23:30 считалось днём, т.к. Sunrise.TimeOfDay="06:xx" > now).
            // Теперь: полноценный NOAA-итерационный алгоритм SkyMap.GetSunTimes +
            // корректная граница ночи [закат .. рассвет] с оборотом через полночь.
            try
            {
                var pos = await _locationService.GetCurrentLocationAsync();
                double lat, lon;
                if (pos != null)
                {
                    lat = pos.Coordinate.Point.Position.Latitude;
                    lon = pos.Coordinate.Point.Position.Longitude;
                }
                else
                {
                    // Fallback: сохранённые координаты или Москва (нет GPS — тема всё равно должна работать)
                    lat = (double)(Local.Values["LastLat"] ?? 55.7558);
                    lon = (double)(Local.Values["LastLon"] ?? 37.6173);
                }

                var localNow = DateTime.Now;
                double tzOffset = TimeZoneInfo.Local.GetUtcOffset(localNow).TotalHours;
                var sunTimes = Barometer_UWP.Helpers.SkyMap.GetSunTimes(lat, lon, localNow.Date, tzOffset);

                bool night;
                if (sunTimes.Rise.HasValue && sunTimes.Set.HasValue)
                {
                    DateTime rise = sunTimes.Rise.Value, set = sunTimes.Set.Value;
                    // Ночь = после заката ИЛИ до рассвета (корректно оборачивается через полночь)
                    night = localNow >= set || localNow < rise;
                }
                else
                {
                    // Полярный день/ночь: определяем по высоте Солнца
                    var eq = Barometer_UWP.Helpers.SkyMap.SunPosition(localNow.ToUniversalTime());
                    var hor = Barometer_UWP.Helpers.SkyMap.EquatorialToHorizontal(eq.RaDeg, eq.DecDeg, lat, lon, localNow.ToUniversalTime());
                    night = hor.Altitude < -6.0; // гражданские сумерки
                }

                Local.Values["LastLat"] = lat;
                Local.Values["LastLon"] = lon;
                Local.Values["AutoThemeNight"] = night;
                if (sunTimes.Rise.HasValue) Local.Values["LastSunrise"] = sunTimes.Rise.Value.ToString("o");
                if (sunTimes.Set.HasValue) Local.Values["LastSunset"] = sunTimes.Set.Value.ToString("o");
            }
            catch (Exception ex)
            {
                _ = Barometer_UWP.Helpers.Logger.LogErrorAsync("ApplyAutoTheme failed", ex);
            }
        }

        private void ApplyLanguage()
        {
            try { ApplicationLanguages.PrimaryLanguageOverride = _selectedLanguage; }
            catch { }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_authService != null) _authService.AuthStateChanged -= OnAuthStateChanged;
            if (_scheduleService != null) _scheduleService.Changed -= OnScheduleChanged;
            _ = SaveSettingsAsync();
        }
    }
}
