using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;
using Barometer_UWP.Services;

namespace Barometer_UWP.ViewModels
{
    public class GraphicsViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly string[] PeriodNames =
            { "1 час", "2 часа", "6 часов", "Сутки", "2 суток", "Неделя", "Месяц", "6 мес.", "Год" };
        private static readonly TimeSpan[] PeriodSpans =
            { TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(6),
              TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(7),
              TimeSpan.FromDays(30), TimeSpan.FromDays(182), TimeSpan.FromDays(365) };

        private readonly DataService _dataService;
        private readonly ExportService _exportService;
        private readonly SensorService _sensorService;

        private List<PressureRecord> _filtered = new List<PressureRecord>();
        private int _periodIndex = 3; // Сутки
        private bool _isLiveMode = true;
        private UnitMode _unitMode = UnitMode.MmHg;
        private WriteableBitmap _chartBitmap;
        private double _minValue, _maxValue;
        private string _statusText = "";

        public GraphicsViewModel()
        {
            var services = App.Current?.Services;
            _dataService = services?.DataService;
            _exportService = services?.ExportService ?? new ExportService();
            _sensorService = services?.SensorService;

            PrevPeriodCommand = new RelayCommand(_ => ShiftPeriod(-1));
            NextPeriodCommand = new RelayCommand(_ => ShiftPeriod(1));
            ToggleLiveCommand = new RelayCommand(_ => IsLiveMode = !IsLiveMode);
            SavePngCommand = new RelayCommand(async _ => await SaveImageAsync("png"));
            SaveJpegCommand = new RelayCommand(async _ => await SaveImageAsync("jpg"));
            SaveHtmlCommand = new RelayCommand(async _ => await SaveHtmlAsync());
            ShareCommand = new RelayCommand(_ => ShowShareUI());

            if (_sensorService != null) _sensorService.OnReading += OnLiveReading;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (_dataService != null)
                {
                    await _dataService.InitializeAsync();
                    await ReloadAsync();
                }
            }
            catch { }
        }

        private void OnLiveReading(PressureRecord record)
        {
            if (!_isLiveMode || record == null) return;
            var end = DateTime.Now;
            var start = end - PeriodSpans[_periodIndex];
            if (record.Timestamp >= start && record.Timestamp <= end)
            {
                _filtered.Add(record);
                RebuildChart();
            }
        }

        public async Task ReloadAsync()
        {
            var end = DateTime.Now;
            var start = end - PeriodSpans[_periodIndex];
            var all = _dataService?.GetAll() ?? new List<PressureRecord>();
            _filtered = all.Where(r => r.Timestamp >= start && r.Timestamp <= end)
                           .OrderBy(r => r.Timestamp).ToList();
            RebuildChart();
        }

        private void ShiftPeriod(int delta)
        {
            PeriodIndex = Math.Min(PeriodNames.Length - 1, Math.Max(0, _periodIndex + delta));
        }

        public int PeriodIndex
        {
            get => _periodIndex;
            set
            {
                _periodIndex = Math.Min(PeriodNames.Length - 1, Math.Max(0, value));
                OnPropertyChanged();
                OnPropertyChanged(nameof(PeriodLabel));
                _ = ReloadAsync();
            }
        }

        public string PeriodLabel => PeriodNames[_periodIndex];
        public IEnumerable<string> PeriodOptions => PeriodNames;
        public bool CanPrev => _periodIndex > 0;
        public bool CanNext => _periodIndex < PeriodNames.Length - 1;

        private void RebuildChart()
        {
            var values = _filtered.Select(r => _unitMode == UnitMode.MmHg ? r.PressureMmHg : r.PressureHpa).ToList();
            MinValue = values.Count > 0 ? values.Min() : 0;
            MaxValue = values.Count > 0 ? values.Max() : 0;
            StatusText = $"{_filtered.Count} точек";

            const double w = 900, h = 400;
            var bmp = new WriteableBitmap((int)w, (int)h);
            bmp.FillRectangle(new Windows.Foundation.Rect(0, 0, w, h), Windows.UI.Colors.Transparent);

            if (values.Count >= 2)
            {
                var pts = new List<ChartPoint>();
                for (int i = 0; i < values.Count; i++) pts.Add(new ChartPoint { X = i, Y = values[i] });
                ChartRenderer.DrawPolylineOnBitmap(bmp, pts,
                    _unitMode == UnitMode.MmHg ? Windows.UI.Color.FromArgb(255, 0, 160, 255)
                                               : Windows.UI.Color.FromArgb(255, 0, 200, 120));
            }
            ChartBitmap = bmp;
        }

        public WriteableBitmap ChartBitmap
        {
            get => _chartBitmap;
            private set { _chartBitmap = value; OnPropertyChanged(); }
        }

        public double MinValue { get => _minValue; private set { _minValue = value; OnPropertyChanged(); } }
        public double MaxValue { get => _maxValue; private set { _maxValue = value; OnPropertyChanged(); } }
        public string MinLabel => _filtered.Count > 0 ? $"{MinValue:F1}" : "--";
        public string MaxLabel => _filtered.Count > 0 ? $"{MaxValue:F1}" : "--";

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsLiveMode
        {
            get => _isLiveMode;
            set { _isLiveMode = value; OnPropertyChanged(); }
        }

        public UnitMode UnitMode
        {
            get => _unitMode;
            set { _unitMode = value; OnPropertyChanged(); RebuildChart(); }
        }

        #region Commands
        public RelayCommand PrevPeriodCommand { get; }
        public RelayCommand NextPeriodCommand { get; }
        public RelayCommand ToggleLiveCommand { get; }
        public RelayCommand SavePngCommand { get; }
        public RelayCommand SaveJpegCommand { get; }
        public RelayCommand SaveHtmlCommand { get; }
        public RelayCommand ShareCommand { get; }
        #endregion

        private async Task SaveImageAsync(string ext)
        {
            if (_chartBitmap == null) return;
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
            };
            picker.FileTypeChoices.Add(ext == "png" ? "PNG image" : "JPEG image",
                new List<string> { ext == "png" ? ".png" : ".jpg" });
            picker.SuggestedFileName = $"pressure_{DateTime.Now:yyyyMMdd_HHmm}.{ext}";
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            byte[] bytes = await ImageEncoder.EncodeAsync(_chartBitmap, ext == "png");
            using (var stream = await file.OpenStreamForWriteAsync())
            {
                if (bytes != null) await stream.WriteAsync(bytes, 0, bytes.Length);
            }
            StatusText = "Сохранено";
            if (bytes != null && ext == "png")
                _cachedPng = bytes;
        }

        private byte[] _cachedPng;

        private async Task SaveHtmlAsync()
        {
            if (_chartBitmap == null) return;
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeChoices.Add("HTML document", new List<string> { ".html" });
            picker.SuggestedFileName = $"pressure_chart_{DateTime.Now:yyyyMMdd_HHmm}";
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            if (_cachedPng == null)
            {
                _cachedPng = await ImageEncoder.EncodeAsync(_chartBitmap, true);
            }
            await _exportService.ExportPlotHtmlAsync(file, _cachedPng, "Barometer chart");
            StatusText = "HTML сохранён";
        }

        private void ShowShareUI()
        {
            var dtm = Windows.ApplicationModel.DataTransfer.DataTransferManager.GetForCurrentView();
            dtm.DataRequested -= OnDataRequested;
            dtm.DataRequested += OnDataRequested;
            Windows.ApplicationModel.DataTransfer.DataTransferManager.ShowShareUI();
        }

        private void OnDataRequested(Windows.ApplicationModel.DataTransfer.DataTransferManager sender,
            Windows.ApplicationModel.DataTransfer.DataRequestedEventArgs args)
        {
            var data = args.Request.Data;
            data.Properties.Title = "Barometer — график давления";
            data.SetText($"Давление: {MinLabel}–{MaxLabel} ({PeriodLabel}), {_filtered.Count} точек.");
            if (_cachedPng != null)
            {
                var rnd = RandomAccessStreamReference.CreateFromStream(
                    new InMemoryRandomAccessStreamWithContent(_cachedPng));
                data.ResourceMap.Add("image/png", rnd);
            }
        }

        /// <summary>Helper: in-memory RandomAccessStream preloaded with bytes.</summary>
        private sealed class InMemoryRandomAccessStreamWithContent : Windows.Storage.Streams.InMemoryRandomAccessStream
        {
            public InMemoryRandomAccessStreamWithContent(byte[] content)
            {
                using (var writer = new Windows.Storage.Streams.DataWriter(this.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(content);
                    writer.StoreAsync().AsTask().Wait();
                    writer.FlushAsync().AsTask().Wait();
                }
                Seek(0);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Dispose()
        {
            if (_sensorService != null) _sensorService.OnReading -= OnLiveReading;
        }
    }
}
