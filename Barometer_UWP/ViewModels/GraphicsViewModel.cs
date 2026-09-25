using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;
using Barometer_UWP.Services;

namespace Barometer_UWP.ViewModels
{
    /// <summary>
    /// Interactive chart view model (RC1): period selection, pan through history,
    /// pinch zoom of the value axis with adaptive tick step, tap-to-inspect tooltip,
    /// realtime mode, PNG/JPEG/HTML export and sharing.
    /// Rendering goes into a WriteableBitmap via ChartRenderer — no external chart
    /// package needed (OxyPlot.UWP/LiveCharts are unavailable for Win10 Mobile ARM).
    /// </summary>
    public class GraphicsViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly string[] PeriodNames =
            { "1 час", "2 часа", "6 часов", "Сутки", "2 суток", "Неделя", "Месяц", "6 мес.", "Год" };
        private static readonly TimeSpan[] PeriodSpans =
            { TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(6),
              TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(7),
              TimeSpan.FromDays(30), TimeSpan.FromDays(182), TimeSpan.FromDays(365) };

        private const double PlotW = 900, PlotH = 400;

        private readonly DataService _dataService;
        private readonly ExportService _exportService;
        private readonly SensorService _sensorService;

        private List<PressureRecord> _all = new List<PressureRecord>();
        private DateTime _windowEnd = DateTime.Now;
        private int _periodIndex = 3; // Сутки
        private bool _isLiveMode = true;
        private UnitMode _unitMode = UnitMode.MmHg;
        private double _yZoom = 1.0;
        private WriteableBitmap _chartBitmap;
        private string _statusText = "";
        private byte[] _cachedPng;
        private bool _disposed;

        // current rendered window (for HitTest / AxisInfo)
        private DateTime _winStart, _winStop;
        private double _dispMin, _dispMax;
        private List<PressureRecord> _filtered = new List<PressureRecord>();
        private List<double> _displayValues = new List<double>();

        public GraphicsViewModel()
        {
            var services = App.Current?.Services;
            _dataService = services?.DataService;
            _exportService = services?.ExportService ?? new ExportService();
            _sensorService = services?.SensorService;

            PrevPeriodCommand = new RelayCommand(_ => PanBy(-1));
            NextPeriodCommand = new RelayCommand(_ => PanBy(1));
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
                    _all = _dataService.GetAll().ToList();
                }
                RebuildChart();
            }
            catch { }
        }

        private void OnLiveReading(PressureRecord record)
        {
            if (record == null || _disposed) return;
            var dispatcher = Windows.UI.Xaml.Window.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                _ = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () => HandleLive(record));
                return;
            }
            HandleLive(record);
        }

        private void HandleLive(PressureRecord record)
        {
            if (_disposed) return;
            _all.Add(record);
            if (!_isLiveMode) return;
            _windowEnd = DateTime.Now;   // live mode: window follows "now"
            _yZoom = 1.0;
            RebuildChart();
        }

        public async Task ReloadAsync()
        {
            try
            {
                if (_dataService != null) _all = _dataService.GetAll().ToList();
            }
            catch { }
            RebuildChart();
        }

        // ---- windowing / interaction ------------------------------------------

        public int PeriodIndex
        {
            get => _periodIndex;
            set
            {
                _periodIndex = Math.Min(PeriodNames.Length - 1, Math.Max(0, value));
                _yZoom = 1.0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PeriodLabel));
                OnPropertyChanged(nameof(CanPrev));
                OnPropertyChanged(nameof(CanNext));
                RebuildChart();
            }
        }

        public string PeriodLabel => PeriodNames[_periodIndex];
        public IEnumerable<string> PeriodOptions => PeriodNames;
        public bool CanPrev => _periodIndex > 0;
        public bool CanNext => _periodIndex < PeriodNames.Length - 1;

        /// <summary>Pan the visible window by N steps (one step = half a window).</summary>
        public void PanBy(int steps)
        {
            if (steps == 0) return;
            var span = PeriodSpans[_periodIndex];
            var now = DateTime.Now;
            _windowEnd = _windowEnd.AddSeconds(steps * span.TotalSeconds / 2);
            if (_windowEnd > now) _windowEnd = now;
            var earliest = _all.Count > 0 ? _all[0].Timestamp : now.AddDays(-400);
            if (_windowEnd - span < earliest && steps < 0) _windowEnd = earliest + span;
            IsLiveMode = false; // manual pan leaves live-follow
            RebuildChart();
        }

        /// <summary>Zoom the value (Y) axis: factor &lt; 1 zooms in, &gt; 1 zooms out.</summary>
        public void ZoomY(double factor)
        {
            _yZoom = Math.Min(8.0, Math.Max(0.25, _yZoom * factor));
            RebuildChart();
        }

        /// <summary>Hit-test normalized coordinates (fx, fy in 0..1) -> tooltip string or null.</summary>
        public string HitTest(double fx, double fy)
        {
            if (_filtered == null || _filtered.Count < 2) return null;
            if (fx < 0 || fx > 1 || fy < 0 || fy > 1) return null;
            int idx = (int)Math.Round(fx * (_filtered.Count - 1));
            idx = Math.Min(_filtered.Count - 1, Math.Max(0, idx));
            var r = _filtered[idx];
            double v = _unitMode == UnitMode.MmHg ? r.PressureMmHg : r.PressureHpa;
            string unit = _unitMode == UnitMode.MmHg ? "мм рт. ст." : "гПа";
            return $"{r.Timestamp:dd.MM HH:mm}  {v:F1} {unit}";
        }

        // ---- rendering ---------------------------------------------------------

        private void RebuildChart()
        {
            var end = _windowEnd;
            var start = end - PeriodSpans[_periodIndex];
            _winStart = start; _winStop = end;

            _filtered = _all.Where(r => r.Timestamp >= start && r.Timestamp <= end)
                            .OrderBy(r => r.Timestamp).ToList();
            _displayValues = _filtered
                .Select(r => _unitMode == UnitMode.MmHg ? r.PressureMmHg : r.PressureHpa).ToList();

            OnPropertyChanged(nameof(RangeInfoText));
            OnPropertyChanged(nameof(EmptyHintVisibility));
            OnPropertyChanged(nameof(AxisInfoText));
            OnPropertyChanged(nameof(MinLabel));
            OnPropertyChanged(nameof(MaxLabel));

            if (_filtered.Count < 2)
            {
                ChartBitmap = null;
                StatusText = "Нет данных за период";
                return;
            }

            double dataMin = _displayValues.Min(), dataMax = _displayValues.Max();
            double center = (dataMin + dataMax) / 2;
            double halfSpan = Math.Max((dataMax - dataMin) / 2, 0.5) * _yZoom;
            _dispMin = center - halfSpan;
            _dispMax = center + halfSpan;

            var pts = new List<ChartPoint>(_filtered.Count);
            for (int i = 0; i < _filtered.Count; i++)
                pts.Add(new ChartPoint
                {
                    X = (_filtered[i].Timestamp - start).TotalMinutes,
                    Y = _displayValues[i]
                });
            pts = ChartRenderer.Decimate(pts, 4000);

            var lineColor = _unitMode == UnitMode.MmHg
                ? Color.FromArgb(255, 0, 160, 255)
                : Color.FromArgb(255, 0, 200, 120);

            var bmp = new WriteableBitmap((int)PlotW, (int)PlotH);
            using (bmp.OpenStream()) { } // allocate back buffer
            bool dark = IsDarkTheme();
            bmp.FillRectangle(new Windows.Foundation.Rect(0, 0, PlotW, PlotH),
                dark ? Color.FromArgb(255, 24, 24, 28) : Color.FromArgb(255, 250, 250, 252));

            DrawGridAndAxis(bmp, start, end, dark);
            ChartRenderer.DrawPolylineOnBitmap(bmp, pts, lineColor, 2.0);

            ChartBitmap = bmp;
            _cachedPng = null;
            StatusText = $"{_filtered.Count} точек";
        }

        private static bool IsDarkTheme()
        {
            try
            {
                return Windows.UI.Xaml.Application.Current.RequestedTheme ==
                       Windows.UI.Xaml.ApplicationTheme.Dark;
            }
            catch { return false; }
        }

        private void DrawGridAndAxis(WriteableBitmap bmp, DateTime start, DateTime end, bool dark)
        {
            double spanMin = (end - start).TotalMinutes;
            double stepMin;
            PickTickStep(spanMin, out stepMin);

            var gridColor = dark ? Color.FromArgb(255, 60, 60, 66) : Color.FromArgb(255, 225, 225, 230);

            // horizontal grid lines (value axis)
            foreach (var v in NiceTicks(_dispMin, _dispMax, 5))
            {
                double y = PlotH - 30 - (v - _dispMin) /
                    Math.Max(1e-9, _dispMax - _dispMin) * (PlotH - 50);
                bmp.DrawLineThick(new List<Windows.Foundation.Point>
                {
                    new Windows.Foundation.Point(50, y),
                    new Windows.Foundation.Point(PlotW - 10, y)
                }, gridColor, 1);
            }

            // vertical grid lines (time axis)
            var first = StartOfStep(start, stepMin);
            for (var t = first; t <= end; t = t.AddMinutes(stepMin))
            {
                double x = 50 + (t - start).TotalMinutes / Math.Max(1e-9, spanMin) * (PlotW - 60);
                bmp.DrawLineThick(new List<Windows.Foundation.Point>
                {
                    new Windows.Foundation.Point(x, 10),
                    new Windows.Foundation.Point(x, PlotH - 30)
                }, gridColor, 1);
            }
        }

        private static DateTime StartOfStep(DateTime t, double stepMin)
        {
            if (stepMin >= 1440) return t.Date;
            if (stepMin >= 60) return t.Date.AddHours(t.Hour);
            int s = (int)stepMin;
            if (s <= 0) s = 5;
            return t.Date.AddMinutes(t.TimeOfDay.Ticks / TimeSpan.TicksPerMinute / s * s);
        }

        /// <summary>Chooses a human tick step (~6 major ticks) for the visible span.</summary>
        private static string PickTickStep(double spanMin, out double stepMin)
        {
            double target = spanMin / 6;
            double[] candidates = { 5, 10, 15, 30, 60, 120, 240, 480, 720, 1440, 2880, 10080, 43200 };
            stepMin = candidates.First(c => c >= target);
            if (stepMin < 60) return $"{(int)stepMin} мин";
            if (stepMin < 1440) return $"{(int)(stepMin / 60)} ч";
            if (stepMin < 10080) return $"{(int)(stepMin / 1440)} дн";
            return $"{(int)(stepMin / 10080)} нед";
        }

        private static List<double> NiceTicks(double min, double max, int count)
        {
            var list = new List<double>();
            double span = max - min;
            if (span <= 0) return list;
            double raw = span / count;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double step = (norm >= 5 ? 5 : norm >= 2 ? 2 : 1) * mag;
            double start = Math.Ceiling(min / step) * step;
            for (double v = start; v <= max + 1e-9; v += step) list.Add(v);
            return list;
        }

        // ---- bindable state -----------------------------------------------------

        public WriteableBitmap ChartBitmap
        {
            get => _chartBitmap;
            private set { _chartBitmap = value; OnPropertyChanged(); }
        }

        public bool HasData => _filtered != null && _filtered.Count >= 2;

        public Windows.UI.Xaml.Visibility EmptyHintVisibility =>
            HasData ? Windows.UI.Xaml.Visibility.Collapsed : Windows.UI.Xaml.Visibility.Visible;

        public string MinLabel => HasData ? _displayValues.Min().ToString("F1") : "--";
        public string MaxLabel => HasData ? _displayValues.Max().ToString("F1") : "--";
        public string RangeInfoText => HasData ? $"min {MinLabel} / max {MaxLabel}" : "";

        public string AxisInfoText
        {
            get
            {
                if (!HasData) return "";
                double stepMin;
                PickTickStep((_winStop - _winStart).TotalMinutes, out stepMin);
                string unit = _unitMode == UnitMode.MmHg ? "мм рт. ст." : "гПа";
                return $"{_winStart:dd.MM HH:mm} – {_winStop:dd.MM HH:mm}, шаг {stepMin:F0} мин, ось {unit}";
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsLiveMode
        {
            get => _isLiveMode;
            set
            {
                if (_isLiveMode == value) return;
                _isLiveMode = value;
                OnPropertyChanged();
                if (value) { _windowEnd = DateTime.Now; _yZoom = 1.0; RebuildChart(); }
            }
        }

        public UnitMode UnitMode
        {
            get => _unitMode;
            set { _unitMode = value; OnPropertyChanged(); RebuildChart(); }
        }

        // ---- commands / export / share -------------------------------------------

        public RelayCommand PrevPeriodCommand { get; }
        public RelayCommand NextPeriodCommand { get; }
        public RelayCommand ToggleLiveCommand { get; }
        public RelayCommand SavePngCommand { get; }
        public RelayCommand SaveJpegCommand { get; }
        public RelayCommand SaveHtmlCommand { get; }
        public RelayCommand ShareCommand { get; }

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
            if (bytes != null)
            {
                using (var stream = await file.OpenStreamForWriteAsync())
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                if (ext == "png") _cachedPng = bytes;
                StatusText = "Сохранено";
            }
        }

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
                _cachedPng = await ImageEncoder.EncodeAsync(_chartBitmap, true);
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

        private async void OnDataRequested(
            Windows.ApplicationModel.DataTransfer.DataTransferManager sender,
            Windows.ApplicationModel.DataTransfer.DataRequestedEventArgs args)
        {
            var request = args.Request;
            var deferral = request.GetDeferral();
            try
            {
                var data = request.Data;
                data.Properties.Title = "Barometer — график давления";
                data.SetText($"Давление: {MinLabel}–{MaxLabel}, период {PeriodLabel}, {_filtered.Count} точек.");

                if (_cachedPng == null && _chartBitmap != null)
                    _cachedPng = await ImageEncoder.EncodeAsync(_chartBitmap, true);
                if (_cachedPng != null)
                {
                    var png = _cachedPng;
                    data.ResourceMap.Add("image/png",
                        RandomAccessStreamReference.CreateFromStream(
                            new InMemoryRandomAccessStreamWithContent(png)));
                }
            }
            finally { deferral.Complete(); }
        }

        /// <summary>In-memory RandomAccessStream preloaded with bytes.</summary>
        private sealed class InMemoryRandomAccessStreamWithContent : InMemoryRandomAccessStream
        {
            public InMemoryRandomAccessStreamWithContent(byte[] content)
            {
                using (var writer = new DataWriter(this.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(content);
                    writer.StoreAsync().AsTask().GetAwaiter().GetResult();
                    writer.FlushAsync().AsTask().GetAwaiter().GetResult();
                }
                Seek(0);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_sensorService != null) _sensorService.OnReading -= OnLiveReading;
        }
    }
}
