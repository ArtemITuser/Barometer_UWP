using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;
using Barometer_UWP.Services;

namespace Barometer_UWP.ViewModels
{
    public class GraphicsViewModel : INotifyPropertyChanged
    {
        private readonly DataService _dataService;
        private ObservableCollection<PressureRecord> _pressureData;
        private DateTime _startDate;
        private DateTime _endDate;
        private bool _isLiveMode;
        private UnitMode _unitMode;

        public GraphicsViewModel()
        {
            _dataService = App.Current.Services.DataService;
            _pressureData = new ObservableCollection<PressureRecord>();
            _startDate = DateTime.Now.AddDays(-1); // Default to last 24 hours
            _endDate = DateTime.Now;
            _unitMode = UnitMode.MmHg;
            _isLiveMode = true;
            
            InitializeAsync();
            
            // Subscribe to sensor readings for live updates
            App.Current.Services.SensorService.OnReading += OnPressureReading;
            
            // Initialize commands
            ToggleLiveModeCommand = new RelayCommand(ToggleLiveMode);
        }

        private async void InitializeAsync()
        {
            await _dataService.InitializeAsync();
            await LoadDataAsync();
        }

        private void OnPressureReading(PressureRecord record)
        {
            if (_isLiveMode)
            {
                // Add new record to the collection for live updates
                _pressureData.Add(record);
                
                // Remove old records if needed to maintain performance
                if (_pressureData.Count > 10000) // Keep max 10,000 points
                {
                    _pressureData.RemoveAt(0);
                }
            }
        }

        public async Task LoadDataAsync()
        {
            var allRecords = await _dataService.LoadAsync();
            var filteredRecords = new ObservableCollection<PressureRecord>();
            
            foreach (var record in allRecords)
            {
                if (record.Timestamp >= _startDate && record.Timestamp <= _endDate)
                {
                    filteredRecords.Add(record);
                }
            }
            
            _pressureData = filteredRecords;
            OnPropertyChanged(nameof(PressureData));
        }

        public ObservableCollection<PressureRecord> PressureData
        {
            get => _pressureData;
            private set
            {
                _pressureData = value;
                OnPropertyChanged();
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged();
                _ = LoadDataAsync();
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                _endDate = value;
                OnPropertyChanged();
                _ = LoadDataAsync();
            }
        }

        public bool IsLiveMode
        {
            get => _isLiveMode;
            set
            {
                _isLiveMode = value;
                OnPropertyChanged();
            }
        }

        public UnitMode UnitMode
        {
            get => _unitMode;
            set
            {
                _unitMode = value;
                OnPropertyChanged();
            }
        }

        public ICommand ToggleLiveModeCommand { get; }

        private void ToggleLiveMode(object parameter)
        {
            IsLiveMode = !IsLiveMode;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            App.Current.Services.SensorService.OnReading -= OnPressureReading;
        }
    }
}