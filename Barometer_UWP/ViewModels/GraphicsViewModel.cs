using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;
using Barometer_UWP.Services;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.UWP;

namespace Barometer_UWP.ViewModels
{
    public class GraphicsViewModel : INotifyPropertyChanged
    {
        private readonly DataService _dataService;
        private readonly ExportService _exportService;
        private readonly ObservableCollection<PressureRecord> _pressureData;
        private SeriesCollection _seriesCollection;
        private List<string> _dateTimeLabels;
        private string[] _periodOptions;
        private string _selectedPeriod;
        private bool _isLiveMode;
        private UnitMode _unitMode;
        private Func<double, string> _yFormatter;

        public GraphicsViewModel()
        {
            _dataService = App.Current.Services.DataService;
            _exportService = new ExportService();
            _pressureData = new ObservableCollection<PressureRecord>();
            _isLiveMode = true;
            _unitMode = UnitMode.MmHg;
            
            InitializeAsync();
            
            // Subscribe to sensor readings for live updates
            App.Current.Services.SensorService.OnReading += OnPressureReading;
            
            // Initialize commands
            ToggleLiveModeCommand = new RelayCommand(ToggleLiveMode);
            ExportPngCommand = new RelayCommand(ExportPng);
            ExportXlsxCommand = new RelayCommand(ExportXlsx);
            ShareCommand = new RelayCommand(ShareData);
            
            // Initialize chart data
            InitializeChartData();
            
            // Set up period options
            _periodOptions = new[] { 
                "Hour", "2 Hours", "6 Hours", "Day", "2 Days", "Week", 
                "Month", "6 Months", "Year" 
            };
            _selectedPeriod = "Day";
        }

        private async void InitializeAsync()
        {
            await _dataService.InitializeAsync();
            await LoadDataAsync(DateTime.Now.AddDays(-1), DateTime.Now); // Default to last 24 hours
        }

        private void InitializeChartData()
        {
            _seriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Pressure",
                    Values = new ChartValues<ObservablePoint>(),
                    PointGeometrySize = 10,
                    LineSmoothness = 0.5,
                    Fill = System.Windows.Media.Brushes.Transparent
                }
            };
            
            _dateTimeLabels = new List<string>();
            
            // Initialize Y-axis formatter
            _yFormatter = value => $"{value:F2}";
            
            OnPropertyChanged(nameof(SeriesCollection));
            OnPropertyChanged(nameof(DateTimeLabels));
            OnPropertyChanged(nameof(YFormatter));
        }

        private void OnPressureReading(PressureRecord record)
        {
            if (_isLiveMode)
            {
                // Add new record to the collection for live updates
                _pressureData.Add(record);
                
                // Update chart with new data
                UpdateChart();
                
                // Remove old records if needed to maintain performance
                if (_pressureData.Count > 10000) // Keep max 10,000 points
                {
                    _pressureData.RemoveAt(0);
                }
            }
        }

        public async Task LoadDataAsync(DateTime startDate, DateTime endDate)
        {
            var allRecords = await _dataService.LoadAsync();
            var filteredRecords = new List<PressureRecord>();
            
            foreach (var record in allRecords)
            {
                if (record.Timestamp >= startDate && record.Timestamp <= endDate)
                {
                    filteredRecords.Add(record);
                }
            }
            
            // Clear current data
            _pressureData.Clear();
            foreach (var record in filteredRecords.OrderBy(r => r.Timestamp))
            {
                _pressureData.Add(record);
            }
            
            UpdateChart();
        }

        private void UpdateChart()
        {
            var series = _seriesCollection[0];
            var values = (ChartValues<ObservablePoint>)series.Values;
            values.Clear();
            
            _dateTimeLabels.Clear();
            
            foreach (var record in _pressureData.OrderBy(r => r.Timestamp))
            {
                var pressureValue = _unitMode == UnitMode.MmHg ? record.PressureMmHg : record.PressureHpa;
                var point = new ObservablePoint(record.Timestamp.ToOADate(), pressureValue);
                values.Add(point);
                _dateTimeLabels.Add(record.Timestamp.ToString("HH:mm"));
            }
            
            OnPropertyChanged(nameof(DateTimeLabels));
        }

        public SeriesCollection SeriesCollection
        {
            get => _seriesCollection;
            private set
            {
                _seriesCollection = value;
                OnPropertyChanged();
            }
        }

        public List<string> DateTimeLabels
        {
            get => _dateTimeLabels;
            private set
            {
                _dateTimeLabels = value;
                OnPropertyChanged();
            }
        }

        public Func<double, string> YFormatter
        {
            get => _yFormatter;
            set
            {
                _yFormatter = value;
                OnPropertyChanged();
            }
        }

        public string[] PeriodOptions
        {
            get => _periodOptions;
        }

        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                _selectedPeriod = value;
                OnPropertyChanged();
                
                // Calculate date range based on selected period
                var endDate = DateTime.Now;
                var startDate = endDate;
                
                switch (_selectedPeriod)
                {
                    case "Hour":
                        startDate = endDate.AddHours(-1);
                        break;
                    case "2 Hours":
                        startDate = endDate.AddHours(-2);
                        break;
                    case "6 Hours":
                        startDate = endDate.AddHours(-6);
                        break;
                    case "Day":
                        startDate = endDate.AddDays(-1);
                        break;
                    case "2 Days":
                        startDate = endDate.AddDays(-2);
                        break;
                    case "Week":
                        startDate = endDate.AddDays(-7);
                        break;
                    case "Month":
                        startDate = endDate.AddMonths(-1);
                        break;
                    case "6 Months":
                        startDate = endDate.AddMonths(-6);
                        break;
                    case "Year":
                        startDate = endDate.AddYears(-1);
                        break;
                }
                
                _ = LoadDataAsync(startDate, endDate);
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
                UpdateChart();
            }
        }

        public ICommand ToggleLiveModeCommand { get; }
        public ICommand ExportPngCommand { get; }
        public ICommand ExportXlsxCommand { get; }
        public ICommand ShareCommand { get; }

        private void ToggleLiveMode(object parameter)
        {
            IsLiveMode = !IsLiveMode;
        }

        private async void ExportPng(object parameter)
        {
            // Placeholder for PNG export functionality
            // This would require additional implementation to render chart to image
        }

        private async void ExportXlsx(object parameter)
        {
            await _exportService.ExportXlsxAsync(null, _pressureData.ToList(), System.Globalization.CultureInfo.CurrentUICulture);
        }

        private async void ShareData(object parameter)
        {
            // Share the chart data
            var dataTransferManager = Windows.ApplicationModel.DataTransfer.DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += OnDataRequested;
            Windows.ApplicationModel.DataTransfer.DataTransferManager.ShowShareUI();
        }

        private void OnDataRequested(Windows.ApplicationModel.DataTransfer.DataTransferManager sender, Windows.ApplicationModel.DataTransfer.DataRequestedEventArgs args)
        {
            var request = args.Request;
            request.Data.Properties.Title = "Pressure Chart Data";
            request.Data.SetText($"Pressure chart with {(_pressureData?.Count ?? 0)} data points");
            request.Data.Properties.Description = "Pressure chart data from Barometer UWP app";
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