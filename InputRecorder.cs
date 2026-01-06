using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Bomberman
{
    public class InputRecorder
    {
        private List<InputState[]> _history = new List<InputState[]>();

        public int FrameCount => _history.Count;

        public void RecordFrame(InputState[] inputs)
        {
            // Clone the array to ensure we store a snapshot, just in case
            // Although structs are value types, the array itself is a reference.
            InputState[] snapshot = (InputState[])inputs.Clone();
            _history.Add(snapshot);
        }

        public InputState[] GetFrame(int frame)
        {
            if (frame < 0 || frame >= _history.Count) return new InputState[0]; // Or return empty inputs?
            return _history[frame];
        }

        public void Reset()
        {
            _history.Clear();
        }

        public void Save(string path)
        {
            try 
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
                string json = JsonSerializer.Serialize(_history, options);
                File.WriteAllText(path, json);
                Console.WriteLine($"Saved replay to {path} ({_history.Count} frames)");
            }
            catch(Exception e)
            {
                Console.WriteLine($"Failed to save replay: {e.Message}");
            }
        }

        public void Load(string path)
        {
            if (!File.Exists(path)) 
            {
                Console.WriteLine($"Replay file not found: {path}");
                return;
            }
            
            try
            {
                var options = new JsonSerializerOptions { IncludeFields = true };
                string json = File.ReadAllText(path);
                _history = JsonSerializer.Deserialize<List<InputState[]>>(json, options) ?? new List<InputState[]>();
                Console.WriteLine($"Loaded replay from {path} ({_history.Count} frames)");
            }
             catch(Exception e)
            {
                Console.WriteLine($"Failed to load replay: {e.Message}");
            }
        }
    }
}
