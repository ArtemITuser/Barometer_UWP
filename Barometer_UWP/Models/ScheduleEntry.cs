using System;

namespace Barometer_UWP.Models
{
    public class ScheduleEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TimeSpan Interval { get; set; }
        public TimeSpan? Start { get; set; }
        public TimeSpan? End { get; set; }
        public bool Enabled { get; set; } = true;
    }
}