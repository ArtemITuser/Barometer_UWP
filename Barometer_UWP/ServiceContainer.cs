using Barometer_UWP.Services;

namespace Barometer_UWP
{
    /// <summary>Simple singleton service locator for the app.</summary>
    public class ServiceContainer
    {
        private SensorService _sensorService;
        private DataService _dataService;
        private AuthService _authService;
        private OneDriveService _oneDriveService;
        private ScheduleService _scheduleService;
        private ExportService _exportService;
        private LocationService _locationService;
        private UpdateService _updateService;
        private TileService _tileService;

        public SensorService SensorService => _sensorService ?? (_sensorService = new SensorService());
        public DataService DataService => _dataService ?? (_dataService = new DataService());
        public AuthService AuthService => _authService ?? (_authService = new AuthService());
        public OneDriveService OneDriveService => _oneDriveService ?? (_oneDriveService = new OneDriveService(AuthService));
        public ScheduleService ScheduleService => _scheduleService ?? (_scheduleService = new ScheduleService());
        public ExportService ExportService => _exportService ?? (_exportService = new ExportService());
        public LocationService LocationService => _locationService ?? (_locationService = new LocationService());
        public UpdateService UpdateService => _updateService ?? (_updateService = new UpdateService());
        public TileService TileService => _tileService ?? (_tileService = new TileService());
    }
}
