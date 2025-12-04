using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Barometer_UWP.Models;

namespace Barometer_UWP.Services
{
    public class ScheduleService
    {
        private readonly List<ScheduleEntry> _scheduleEntries;
        private readonly object _lock = new object();

        public ScheduleService()
        {
            _scheduleEntries = new List<ScheduleEntry>();
        }

        public IReadOnlyList<ScheduleEntry> GetAllEntries()
        {
            lock (_lock)
            {
                return new ReadOnlyCollection<ScheduleEntry>(_scheduleEntries.ToList());
            }
        }

        public void AddEntry(ScheduleEntry entry)
        {
            lock (_lock)
            {
                _scheduleEntries.Add(entry);
            }
        }

        public void RemoveEntry(Guid id)
        {
            lock (_lock)
            {
                var entry = _scheduleEntries.FirstOrDefault(e => e.Id == id);
                if (entry != null)
                {
                    _scheduleEntries.Remove(entry);
                }
            }
        }

        public void UpdateEntry(ScheduleEntry updatedEntry)
        {
            lock (_lock)
            {
                var existingEntry = _scheduleEntries.FirstOrDefault(e => e.Id == updatedEntry.Id);
                if (existingEntry != null)
                {
                    var index = _scheduleEntries.IndexOf(existingEntry);
                    _scheduleEntries[index] = updatedEntry;
                }
            }
        }

        public ScheduleEntry GetEntry(Guid id)
        {
            lock (_lock)
            {
                return _scheduleEntries.FirstOrDefault(e => e.Id == id);
            }
        }

        public bool IsWithinTimeWindow(ScheduleEntry entry, DateTime time)
        {
            if (!entry.Start.HasValue && !entry.End.HasValue)
            {
                return true; // No time window specified, always valid
            }

            var timeOfDay = time.TimeOfDay;

            if (entry.Start.HasValue && entry.End.HasValue)
            {
                if (entry.Start.Value <= entry.End.Value)
                {
                    // Normal case: start time is before end time
                    return timeOfDay >= entry.Start.Value && timeOfDay <= entry.End.Value;
                }
                else
                {
                    // Edge case: schedule wraps around midnight (e.g., 22:00 to 06:00)
                    return timeOfDay >= entry.Start.Value || timeOfDay <= entry.End.Value;
                }
            }
            else if (entry.Start.HasValue)
            {
                return timeOfDay >= entry.Start.Value;
            }
            else if (entry.End.HasValue)
            {
                return timeOfDay <= entry.End.Value;
            }

            return true;
        }
    }
}