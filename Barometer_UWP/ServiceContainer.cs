using Barometer_UWP.Services;

namespace Barometer_UWP
{
    public class ServiceContainer
    {
        private SensorService _sensorService;
        private DataService _dataService;
        private ScheduleService _scheduleService;
        private ExportService _exportService;

        public SensorService SensorService
        {
            get
            {
                if (_sensorService == null)
                    _sensorService = new SensorService();
                return _sensorService;
            }
        }

        public DataService DataService
        {
            get
            {
                if (_dataService == null)
                    _dataService = new DataService();
                return _dataService;
            }
        }

        public ScheduleService ScheduleService
        {
            get
            {
                if (_scheduleService == null)
                    _scheduleService = new ScheduleService();
                return _scheduleService;
            }
        }

        public ExportService ExportService
        {
            get
            {
                if (_exportService == null)
                    _exportService = new ExportService();
                return _exportService;
            }
        }
    }
}