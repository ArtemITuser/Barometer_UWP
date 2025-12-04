using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Barometer_UWP.Models;
using Barometer_UWP.Helpers;
using Newtonsoft.Json;
using Windows.Storage;

namespace Barometer_UWP.Services
{
    public class DataService
    {
        private const string DATA_FILE_NAME = "pressure_data.json";
        private const int MAX_RECORDS = 50000; // Maximum number of records to keep
        
        private readonly object _lock = new object();
        private List<PressureRecord> _records;
        private StorageFile _dataFile;
        
        public IReadOnlyList<PressureRecord> GetAll()
        {
            lock (_lock)
            {
                return new ReadOnlyCollection<PressureRecord>(_records.ToList());
            }
        }

        public async Task InitializeAsync()
        {
            _records = new List<PressureRecord>();
            _dataFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(DATA_FILE_NAME, CreationCollisionOption.OpenIfExists);
            
            await LoadAsync();
        }

        public async Task<List<PressureRecord>> LoadAsync()
        {
            lock (_lock)
            {
                _records.Clear();
            }

            try
            {
                var json = await FileIO.ReadTextAsync(_dataFile);
                if (!string.IsNullOrEmpty(json))
                {
                    var loadedRecords = JsonConvert.DeserializeObject<List<PressureRecord>>(json);
                    if (loadedRecords != null)
                    {
                        lock (_lock)
                        {
                            _records.AddRange(loadedRecords);
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

        public async Task SaveAsync()
        {
            try
            {
                lock (_lock)
                {
                    // Keep only the most recent records to prevent unbounded growth
                    if (_records.Count > MAX_RECORDS)
                    {
                        var recordsToKeep = _records.OrderByDescending(r => r.Timestamp)
                                                    .Take(MAX_RECORDS)
                                                    .ToList();
                        _records.Clear();
                        _records.AddRange(recordsToKeep);
                    }
                    
                    var json = JsonConvert.SerializeObject(_records, Formatting.Indented);
                    await FileIO.WriteTextAsync(_dataFile, json);
                }
            }
            catch (Exception ex)
            {
                await Logger.LogErrorAsync("Error saving pressure data", ex);
            }
        }

        public async Task AddAsync(PressureRecord record)
        {
            lock (_lock)
            {
                _records.Add(record);
            }
            
            await SaveAsync();
        }
    }
}