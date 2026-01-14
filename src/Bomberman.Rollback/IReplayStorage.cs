using System;
using System.Threading.Tasks;

namespace Bomberman.Rollback
{
    /// <summary>
    /// Abstraction for loading and saving replay data.
    /// </summary>
    public interface IReplayStorage
    {
        /// <summary>
        /// Saves the serialized replay data to the specified identifier (e.g. file path).
        /// </summary>
        void Save(string identifier, string data);

        /// <summary>
        /// Loads the serialized replay data from the specified identifier.
        /// </summary>
        string Load(string identifier);

        /// <summary>
        /// Checks if the identifier exists.
        /// </summary>
        bool Exists(string identifier);
    }
}
