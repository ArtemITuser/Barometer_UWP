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
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SensorService _sensorService;
        private readonly DataService _dataService;
        private readonly TileService _tileService;
        private UnitMode _unitMode;
        private double _currentPressure;
        private string _currentPressureFormatted;
        private ObservableCollection<PressureRecord> _recentRecords;

        public MainViewModel()
        {
            _sensorService = App.Current.Services.SensorService;
            _dataService = App.Current.Services.DataService;
            _tileService = new TileService();
            
            _recentRecords = new ObservableCollection<PressureRecord>();
            _unitMode = UnitMode.MmHg; // Default to mmHg
            
            InitializeAsync();
            
            // Subscribe to sensor readings
            _sensorService.OnReading += OnPressureReading;
            
            // Initialize commands
            SwitchUnitCommand = new RelayCommand(SwitchUnit);
        }

        private async void InitializeAsync()
        {
            await _dataService.InitializeAsync();
            await _sensorService.InitializeAsync();
            _sensorService.SetReportInterval(1000); // Update every second
        }

        private void OnPressureReading(PressureRecord record)
        {
            // Update current pressure
            CurrentPressure = _unitMode == UnitMode.MmHg ? record.PressureMmHg : record.PressureHpa;
            
            // Add to recent records (limit to 10 most recent)
            if (_recentRecords.Count >= 10)
            {
                _recentRecords.RemoveAt(_recentRecords.Count - 1);
            }
            _recentRecords.Insert(0, record);
            
            // Add to data service
            _ = _dataService.AddAsync(record);
            
            // Update live tile
            _ = _tileService.UpdateTileAsync(record.PressureHpa);
        }

        public double CurrentPressure
        {
            get => _currentPressure;
            private set
            {
                _currentPressure = value;
                OnPropertyChanged();
                CurrentPressureFormatted = $"{_currentPressure:F2}";
            }
        }

        public string CurrentPressureFormatted
        {
            get => _currentPressureFormatted;
            private set
            {
                _currentPressureFormatted = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PressureRecord> RecentRecords
        {
            get => _recentRecords;
            private set
            {
                _recentRecords = value;
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
                
                // Update current pressure display with new unit
                if (_sensorService.LastReading != null)
                {
                    CurrentPressure = _unitMode == UnitMode.MmHg ? 
                        _sensorService.LastReading.PressureMmHg : 
                        _sensorService.LastReading.PressureHpa;
                }
            }
        }

        public ICommand SwitchUnitCommand { get; }

        private void SwitchUnit(object parameter)
        {
            UnitMode = UnitMode == UnitMode.Hpa ? UnitMode.MmHg : UnitMode.Hpa;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            _sensorService.OnReading -= OnPressureReading;
        }
    }
}