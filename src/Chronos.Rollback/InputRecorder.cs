using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Chronos.Core;

namespace Chronos.Rollback
{
    /// <summary>
    /// Records and plays back input history for replays and rollback prediction.
    /// Also handles serialization of input states.
    /// </summary>
    public class InputRecorder<TInput> where TInput : struct, IInputState<TInput>
    {
        private List<TInput[]> _history = new List<TInput[]>();
        
        /// <summary>The seed used for this recording session.</summary>
        public int Seed { get; private set; }
        
        /// <summary>Number of players in this recording.</summary>
        public int TotalPlayers { get; private set; }
        
        /// <summary>Total frames recorded.</summary>
        public int FrameCount => _history.Count;

        private class ReplayData
        {
            public int Seed { get; set; }
            public int TotalPlayers { get; set; }
            public List<TInput[]> History { get; set; } = new List<TInput[]>();
        }

        public void RecordFrame(TInput[] inputs)
        {
            TInput[] snapshot = (TInput[])inputs.Clone();
            _history.Add(snapshot);
        }

        public TInput[] GetFrame(int frame)
        {
            if (frame < 0 || frame >= _history.Count) return new TInput[0];
            return _history[frame];
        }

        public void UpdateFrame(int frame, TInput[] inputs)
        {
            if (frame >= 0 && frame < _history.Count)
            {
                _history[frame] = (TInput[])inputs.Clone();
            }
        }

        public void Reset()
        {
            _history.Clear();
        }

        private readonly IReplayStorage? _storage;

        public InputRecorder(IReplayStorage? storage = null)
        {
            _storage = storage;
        }

        public void Save(string path, int seed, int totalPlayers)
        {
            try 
            {
                var data = new ReplayData 
                {
                    Seed = seed,
                    TotalPlayers = totalPlayers,
                    History = _history
                };

                var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
                string json = JsonSerializer.Serialize(data, options);
                
                if (_storage != null)
                {
                    _storage.Save(path, json);
                }
                else
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(path, json);
                }
                Console.WriteLine($"[Chronos.Rollback] Saved replay to {path} ({_history.Count} frames)");
            }
            catch(Exception e)
            {
                Console.WriteLine($"[Chronos.Rollback] Failed to save replay: {e.Message}");
            }
        }

        public void Load(string path)
        {
            string json = "";

            if (_storage != null)
            {
                 if (!_storage.Exists(path)) return;
                 json = _storage.Load(path);
            }
            else
            {
                if (!File.Exists(path)) return;
                json = File.ReadAllText(path);
            }
            
            try
            {
                var options = new JsonSerializerOptions { IncludeFields = true };
                var data = JsonSerializer.Deserialize<ReplayData>(json, options);
                if (data != null)
                {
                    _history = data.History ?? new List<TInput[]>();
                    Seed = data.Seed;
                    TotalPlayers = data.TotalPlayers;
                    Console.WriteLine($"[Chronos.Rollback] Loaded replay from {path} ({_history.Count} frames)");
                }
            }
             catch(Exception e)
            {
                Console.WriteLine($"[Chronos.Rollback] Failed to load replay: {e.Message}");
            }
        }
    }
}
