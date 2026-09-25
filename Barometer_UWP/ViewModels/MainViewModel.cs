using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Xaml;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;
using Barometer_UWP.Services;

namespace Barometer_UWP.ViewModels
{
    /// <summary>
    /// Main page view model: live pressure display (mmHg / hPa switch),
    /// recent readings list, schedule-throttled persistence and tile updates.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private const int MaxRecent = 50;       // items shown in the recent list
        private const int TileThrottleSec = 60; // min seconds between tile updates

        private readonly SensorService _sensorService;
        private readonly DataService _dataService;
        private readonly TileService _tileService;
        private readonly ScheduleService _scheduleService;

        private UnitMode _unitMode = UnitMode.MmHg;
        private double _currentPressure;
        private string _currentPressureFormatted = "--";
        private string _trendText = "";
        private DateTime _lastTileUpdate = DateTime.MinValue;
        private DateTime? _lastSavedAt = null;
        private bool _disposed;

        public ObservableCollection<PressureRecord> RecentRecords { get; }
            = new ObservableCollection<PressureRecord>();

        public MainViewModel()
        {
            var services = App.Current?.Services;
            _sensorService = services?.SensorService;
            _dataService = services?.DataService;
            _scheduleService = services?.ScheduleService;
            _tileService = services?.TileService ?? new TileService();

            SwitchUnitCommand = new RelayCommand(_ => ToggleUnit());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());

            if (_sensorService != null) _sensorService.OnReading += OnPressureReading;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (_dataService != null)
                {
                    await _dataService.InitializeAsync();
                    var all = _dataService.GetAll();
                    foreach (var r in all.Skip(Math.Max(0, all.Count - MaxRecent)))
                        RecentRecords.Add(r);
                    if (all.Count > 0)
                        CurrentPressure = GetDisplay(all[all.Count - 1]);
                }
            }
            catch { /* storage may be momentarily busy */ }

            try
            {
                if (_sensorService != null)
                {
                    await _sensorService.InitializeAsync();
                    _sensorService.SetReportInterval(1000);
                    var last = _sensorService.LastReading;
                    if (last != null) CurrentPressure = GetDisplay(last);
                }
            }
            catch { /* sensor may be absent on desktop emulator */ }
        }

        private double GetDisplay(PressureRecord r) =>
            _unitMode == UnitMode.MmHg ? r.PressureMmHg : r.PressureHpa;

        private void OnPressureReading(PressureRecord record)
        {
            if (record == null || _disposed) return;

            // Marshal onto the UI thread (sensor callback arrives on a worker thread).
            var dispatcher = Window.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                _ = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () => HandleReading(record));
                return;
            }
            HandleReading(record);
        }

        private void HandleReading(PressureRecord record)
        {
            if (_disposed) return;

            CurrentPressure = GetDisplay(record);

            // Persistence throttle: honor night-time schedule when configured.
            bool allowSave = true;
            if (_scheduleService != null)
                allowSave = _scheduleService.ShouldCollectNow(DateTime.UtcNow);
            if (allowSave && (_lastSavedAt == null ||
                (DateTime.Now - _lastSavedAt.Value).TotalSeconds >= 5))
            {
                _lastSavedAt = DateTime.Now;
                _ = SaveAndTrackAsync(record);
            }

            UpdateTrend(record);
            MaybeUpdateTile(record);
        }

        private async Task SaveAndTrackAsync(PressureRecord record)
        {
            try
            {
                if (_dataService != null) await _dataService.AddAsync(record);
            }
            catch { }

            RecentRecords.Add(record);
            while (RecentRecords.Count > MaxRecent) RecentRecords.RemoveAt(0);
        }

        private void UpdateTrend(PressureRecord record)
        {
            int n = RecentRecords.Count;
            if (n < 10) { TrendText = ""; return; }
            double newest = GetDisplay(RecentRecords[n - 1]);
            double older = GetDisplay(RecentRecords[Math.Max(0, n - 31)]);
            double delta = newest - older;
            TrendText = delta > 0.75 ? "\u25B2 растёт" : delta < -0.75 ? "\u25BC падает" : "\u2192 стабильно";
        }

        private void MaybeUpdateTile(PressureRecord record)
        {
            if ((DateTime.Now - _lastTileUpdate).TotalSeconds < TileThrottleSec) return;
            _lastTileUpdate = DateTime.Now;
            _ = _tileService.UpdateTileAsync(record);
        }

        public double CurrentPressure
        {
            get => _currentPressure;
            private set
            {
                _currentPressure = value;
                OnPropertyChanged();
                CurrentPressureFormatted = value > 0 ? value.ToString("F1") : "--";
            }
        }

        public string CurrentPressureFormatted
        {
            get => _currentPressureFormatted;
            private set { _currentPressureFormatted = value; OnPropertyChanged(); }
        }

        public string TrendText
        {
            get => _trendText;
            private set { _trendText = value; OnPropertyChanged(); }
        }

        public string UnitLabel => _unitMode == UnitMode.MmHg ? "мм рт. ст." : "гПа";

        public UnitMode UnitMode
        {
            get => _unitMode;
            set
            {
                _unitMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UnitLabel));
                var last = _sensorService?.LastReading ??
                    (RecentRecords.Count > 0 ? RecentRecords[RecentRecords.Count - 1] : null);
                if (last != null) CurrentPressure = GetDisplay(last);
            }
        }

        public ICommand SwitchUnitCommand { get; }
        public ICommand RefreshCommand { get; }

        private void ToggleUnit()
        {
            UnitMode = _unitMode == UnitMode.MmHg ? UnitMode.Hpa : UnitMode.MmHg;
        }

        public async Task RefreshAsync()
        {
            try
            {
                if (_dataService != null)
                {
                    var all = await _dataService.LoadAsync();
                    RecentRecords.Clear();
                    foreach (var r in all.Skip(Math.Max(0, all.Count - MaxRecent)))
                        RecentRecords.Add(r);
                    if (all.Count > 0) CurrentPressure = GetDisplay(all[all.Count - 1]);
                }
            }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_sensorService != null) _sensorService.OnReading -= OnPressureReading;
        }
    }
}
