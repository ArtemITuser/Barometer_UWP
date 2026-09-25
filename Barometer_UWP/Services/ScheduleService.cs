using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Data.Json;
using Windows.Storage;

namespace Barometer_UWP.Services
{
    public class ScheduleEntryInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int StartMinutes { get; set; } = 0;     // minutes from midnight
        public int EndMinutes { get; set; } = 24 * 60 - 1;
        public int IntervalSeconds { get; set; } = 60;
        public bool Enabled { get; set; } = true;

        public string StartString => FormatM(StartMinutes);
        public string EndString => FormatM(EndMinutes);
        public string IntervalString => $"{IntervalSeconds} s";

        private static string FormatM(int m) => $"{m / 60:00}:{m % 60:00}";

        public bool Contains(DateTime t)
        {
            var mins = t.Hour * 60 + t.Minute;
            if (StartMinutes <= EndMinutes)
                return mins >= StartMinutes && mins <= EndMinutes;
            return mins >= StartMinutes || mins <= EndMinutes; // wraps midnight
        }
    }

    /// <summary>
    /// Night-time collection schedule with persistence to LocalSettings.
    /// Default rule: every 5 min from 23:00 to 07:00, enabled.
    /// </summary>
    public sealed class ScheduleService
    {
        private const string SettingsKey = "schedule_entries_v1";
        private readonly List<ScheduleEntryInfo> _entries = new List<ScheduleEntryInfo>();
        private readonly object _lock = new object();

        public event Action Changed;

        public ScheduleService()
        {
            Load();
            lock (_lock)
            {
                if (_entries.Count == 0)
                {
                    _entries.Add(new ScheduleEntryInfo
                    {
                        StartMinutes = 23 * 60,
                        EndMinutes = 7 * 60 - 1,
                        IntervalSeconds = 300,
                        Enabled = true
                    });
                    Save();
                }
            }
        }

        public IReadOnlyList<ScheduleEntryInfo> GetEntries()
        {
            lock (_lock) return _entries.ToList();
        }

        public void Add(ScheduleEntryInfo entry)
        {
            if (entry == null) return;
            lock (_lock) _entries.Add(entry);
            Save();
            Changed?.Invoke();
        }

        public void Update(ScheduleEntryInfo entry)
        {
            lock (_lock)
            {
                var idx = _entries.FindIndex(e => e.Id == entry.Id);
                if (idx >= 0) _entries[idx] = entry;
            }
            Save();
            Changed?.Invoke();
        }

        public void Remove(Guid id)
        {
            lock (_lock) _entries.RemoveAll(e => e.Id == id);
            Save();
            Changed?.Invoke();
        }

        public void Toggle(Guid id, bool enabled)
        {
            lock (_lock)
            {
                var e = _entries.FirstOrDefault(x => x.Id == id);
                if (e != null) e.Enabled = enabled;
            }
            Save();
            Changed?.Invoke();
        }

        /// <summary>True when a sensor reading should be taken at this moment.</summary>
        public bool ShouldCollectNow(DateTime nowUtc)
        {
            var local = nowUtc.ToLocalTime();
            lock (_lock)
            {
                foreach (var e in _entries.Where(e => e.Enabled))
                    if (e.Contains(local)) return true;
            }
            return false;
        }

        private void Load()
        {
            try
            {
                var raw = ApplicationData.Current.LocalSettings.Values[SettingsKey] as string;
                if (string.IsNullOrEmpty(raw)) return;
                var arr = JsonArray.Parse(raw);
                lock (_lock)
                {
                    _entries.Clear();
                    foreach (var v in arr)
                    {
                        var o = v.GetObject();
                        _entries.Add(new ScheduleEntryInfo
                        {
                            Id = Guid.TryParse(o.GetNamedString("id"), out var g) ? g : Guid.NewGuid(),
                            StartMinutes = (int)o.GetNamedNumber("start"),
                            EndMinutes = (int)o.GetNamedNumber("end"),
                            IntervalSeconds = (int)o.GetNamedNumber("interval"),
                            Enabled = o.GetNamedBoolean("enabled")
                        });
                    }
                }
            }
            catch { /* corrupted settings -> defaults */ }
        }

        private void Save()
        {
            var arr = new JsonArray();
            lock (_lock)
            {
                foreach (var e in _entries)
                {
                    var o = new JsonObject();
                    o.SetNamedValue("id", JsonValue.CreateStringValue(e.Id.ToString()));
                    o.SetNamedValue("start", JsonValue.CreateNumberValue(e.StartMinutes));
                    o.SetNamedValue("end", JsonValue.CreateNumberValue(e.EndMinutes));
                    o.SetNamedValue("interval", JsonValue.CreateNumberValue(e.IntervalSeconds));
                    o.SetNamedValue("enabled", JsonValue.CreateBooleanValue(e.Enabled));
                    arr.Append(o);
                }
            }
            ApplicationData.Current.LocalSettings.Values[SettingsKey] = arr.Stringify();
        }
    }
}
