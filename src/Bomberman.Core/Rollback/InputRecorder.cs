using System;
using Bomberman.Core.Input;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Bomberman.Core;

namespace Bomberman.Core.Rollback
{
    /// <summary>
    /// Records and plays back input history for replays and rollback prediction.
    /// Also handles serialization of input states.
    /// </summary>
    public class InputRecorder
    {
        private List<InputState[]> _history = new List<InputState[]>();
        
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
            public List<InputState[]> History { get; set; } = new List<InputState[]>();
        }

        /// <summary>
        /// Appends a frame of inputs to the history.
        /// </summary>
        /// <param name="inputs">Array of inputs for all players.</param>
        public void RecordFrame(InputState[] inputs)
        {
            // Clone the array to ensure we store a snapshot, just in case
            // Although structs are value types, the array itself is a reference.
            InputState[] snapshot = (InputState[])inputs.Clone();
            _history.Add(snapshot);
        }

        /// <summary>
        /// Retrieves the inputs for a specific frame.
        /// </summary>
        public InputState[] GetFrame(int frame)
        {
            if (frame < 0 || frame >= _history.Count) return new InputState[0]; // Or return empty inputs?
            return _history[frame];
        }

        /// <summary>
        /// Updates the inputs for a past frame (used during rollback correction).
        /// </summary>
        public void UpdateFrame(int frame, InputState[] inputs)
        {
            if (frame >= 0 && frame < _history.Count)
            {
                _history[frame] = (InputState[])inputs.Clone();
            }
        }

        /// <summary>
        /// Clears all recording history.
        /// </summary>
        public void Reset()
        {
            _history.Clear();
        }

        /// <summary>
        /// Saves the recorded history to a JSON file.
        /// </summary>
        public void Save(string path, int seed, int totalPlayers)
        {
            try 
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var data = new ReplayData 
                {
                    Seed = seed,
                    TotalPlayers = totalPlayers,
                    History = _history
                };

                var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(path, json);
                Console.WriteLine($"Saved replay to {path} ({_history.Count} frames)");
            }
            catch(Exception e)
            {
                Console.WriteLine($"Failed to save replay: {e.Message}");
            }
        }

        // Serialization Helpers for Networking
        
        /// <summary>
        /// Serializes a single input state for network transmission.
        /// </summary>
        public static byte[] SerializeInput(int frameId, InputState input)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(frameId);
                writer.Write(input.Movement.X);
                writer.Write(input.Movement.Y);
                writer.Write(input.PlaceBomb);
                writer.Write(input.BombTarget.X);
                writer.Write(input.BombTarget.Y);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a single input state from a byte array.
        /// </summary>
        public static (int frameId, InputState input) DeserializeInput(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                int frameId = reader.ReadInt32();
                InputState input = new InputState();
                input.Movement.X = reader.ReadInt32();
                input.Movement.Y = reader.ReadInt32();
                input.PlaceBomb = reader.ReadBoolean();
                input.BombTarget = new IntVector2(reader.ReadInt32(), reader.ReadInt32());
                return (frameId, input);
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
                
                // Try deserialize as new format
                try 
                {
                     var data = JsonSerializer.Deserialize<ReplayData>(json, options);
                     if (data != null)
                     {
                         _history = data.History ?? new List<InputState[]>();
                         Seed = data.Seed;
                         TotalPlayers = data.TotalPlayers;
                         Console.WriteLine($"Loaded replay from {path} (v2, {_history.Count} frames)");
                         return;
                     }
                }
                catch
                {
                    // Fallback to old list format (optional, or just fail)
                    // Assuming we want to support old replays minimally or just break them (this is dev phase so break is arguably fine, but clean check is better)
                }

                // Fallback attempt for raw list
                _history = JsonSerializer.Deserialize<List<InputState[]>>(json, options) ?? new List<InputState[]>();
                Seed = 0; // Unknown
                TotalPlayers = 2; // Default?
                Console.WriteLine($"Loaded replay from {path} (Legacy List, {_history.Count} frames)");
            }
             catch(Exception e)
            {
                Console.WriteLine($"Failed to load replay: {e.Message}");
            }
        }
    }
}
