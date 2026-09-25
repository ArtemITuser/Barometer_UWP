using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Barometer_UWP.Helpers;
using Barometer_UWP.Models;
using Newtonsoft.Json;
using Windows.Storage;

namespace Barometer_UWP.Services
{
    /// <summary>Backup file schema: records + metadata in one JSON document.</summary>
    public class BackupFile
    {
        [JsonProperty("metadata")] public BackupMetadata Metadata { get; set; }
        [JsonProperty("records")] public List<PressureRecord> Records { get; set; }
    }

    public class DataService
    {
        private const string DATA_FILE_NAME = "pressure_data.json";
        private const int MAX_RECORDS = 50000; // rotation cap

        private readonly object _lock = new object();
        private List<PressureRecord> _records = new List<PressureRecord>();
        private StorageFile _dataFile;
        private bool _initialized;

        public event Action<PressureRecord> RecordAdded;

        public IReadOnlyList<PressureRecord> GetAll()
        {
            lock (_lock)
            {
                return new ReadOnlyCollection<PressureRecord>(_records.OrderBy(r => r.Timestamp).ToList());
            }
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            _dataFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                DATA_FILE_NAME, CreationCollisionOption.OpenIfExists);
            await LoadAsync();
            _initialized = true;
        }

        public async Task<List<PressureRecord>> LoadAsync()
        {
            try
            {
                var json = await FileIO.ReadTextAsync(_dataFile);
                if (!string.IsNullOrEmpty(json))
                {
                    var loaded = JsonConvert.DeserializeObject<List<PressureRecord>>(json);
                    if (loaded != null)
                    {
                        lock (_lock)
                        {
                            _records = loaded
                                .Where(r => r != null && r.PressureHpa > 10 && r.PressureHpa < 2000)
                                .OrderBy(r => r.Timestamp)
                                .ToList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await Logger.LogErrorAsync("Error loading pressure data", ex);
            }
            return GetAll().ToList();
        }

        /// <summary>Persists the in-memory record set to the local JSON store (thread-safe snapshot).</summary>
        public async Task SaveAsync()
        {
            try
            {
                List<PressureRecord> snapshot;
                lock (_lock)
                {
                    if (_records.Count > MAX_RECORDS)
                    {
                        _records = _records.OrderByDescending(r => r.Timestamp).Take(MAX_RECORDS)
                                            .OrderBy(r => r.Timestamp).ToList();
                    }
                    snapshot = _records.ToList();
                }
                var json = JsonConvert.SerializeObject(snapshot, Formatting.None);
                await FileIO.WriteTextAsync(_dataFile, json);
            }
            catch (Exception ex)
            {
                await Logger.LogErrorAsync("Error saving pressure data", ex);
            }
        }

        public async Task AddAsync(PressureRecord record)
        {
            if (record == null) return;
            bool added;
            lock (_lock)
            {
                added = !_records.Any(r => r.Timestamp == record.Timestamp);
                if (added) _records.Add(record);
            }
            if (added)
            {
                RecordAdded?.Invoke(record);
                await SaveAsync();
            }
        }

        /// <summary>Adds many records with duplicate-merge and a single save. Returns count added.</summary>
        public async Task<int> AddRangeAsync(IEnumerable<PressureRecord> incoming)
        {
            int added = 0;
            lock (_lock)
            {
                var existing = new HashSet<DateTime>(_records.Select(r => r.Timestamp));
                foreach (var r in incoming ?? Enumerable.Empty<PressureRecord>())
                {
                    if (r == null || existing.Contains(r.Timestamp)) continue;
                    _records.Add(r);
                    existing.Add(r.Timestamp);
                    added++;
                }
                _records = _records.OrderBy(r => r.Timestamp).ToList();
            }
            if (added > 0) await SaveAsync();
            return added;
        }

        public async Task ClearAsync()
        {
            lock (_lock) { _records.Clear(); }
            await SaveAsync();
        }

        #region Backup / restore

        public static string MakeBackupFileName(DateTime utcNow) =>
            $"barometer_backup_{utcNow:yyyyMMdd_HHmmss}.json";

        /// <summary>Creates a local backup file in LocalFolder\backups and returns its path+bytes.</summary>
        public async Task<BackupResult> CreateLocalBackupAsync()
        {
            var snapshot = GetAll().ToList();
            var meta = new BackupMetadata
            {
                Filename = MakeBackupFileName(DateTime.UtcNow),
                CreatedAt = DateTime.UtcNow,
                Storage = "Local"
            };
            var doc = new BackupFile { Metadata = meta, Records = snapshot };
            var json = JsonConvert.SerializeObject(doc, Formatting.None);

            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "backups", CreationCollisionOption.OpenIfExists);
            var file = await folder.CreateFileAsync(meta.Filename, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, json);

            return new BackupResult { File = file, Name = meta.Filename, Bytes = System.Text.Encoding.UTF8.GetBytes(json) };
        }

        /// <summary>Restores records from a backup JSON document (merges, dedupes by timestamp). Returns count restored.</summary>
        public async Task<int> RestoreFromJsonAsync(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return 0;
            List<PressureRecord> records = null;
            try
            {
                var doc = JsonConvert.DeserializeObject<BackupFile>(json);
                if (doc != null && doc.Records != null) records = doc.Records;
                else records = JsonConvert.DeserializeObject<List<PressureRecord>>(json); // plain array fallback
            }
            catch (Exception ex)
            {
                await Logger.LogErrorAsync("Backup parse failed", ex);
                return 0;
            }
            return await AddRangeAsync(records);
        }

        public async Task<int> RestoreFromFileAsync(StorageFile file)
        {
            var json = await FileIO.ReadTextAsync(file);
            return await RestoreFromJsonAsync(json);
        }

        /// <summary>List of local backups, newest first.</summary>
        public async Task<List<BackupMetadata>> ListLocalBackupsAsync()
        {
            var result = new List<BackupMetadata>();
            try
            {
                var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync("backups");
                foreach (var f in (await folder.GetFilesAsync()).Where(f => f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(new BackupMetadata { Filename = f.Name, CreatedAt = f.DateCreated.UtcDateTime, Storage = "Local" });
                }
            }
            catch { /* no backups folder yet */ }
            return result.OrderByDescending(b => b.CreatedAt).ToList();
        }

        #endregion
    }

    public class BackupResult
    {
        public StorageFile File { get; set; }
        public string Name { get; set; }
        public byte[] Bytes { get; set; }
    }
}
