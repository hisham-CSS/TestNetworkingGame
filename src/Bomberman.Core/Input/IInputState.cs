using System;
using System.IO;

namespace Bomberman.Core
{
    /// <summary>
    /// Input states in the engine. Implementations must be structs and support binary
    /// serialization (this is what lets Week 3 send inputs over the wire). Back-ported from
    /// the production Chronos.Core.IInputState.
    /// </summary>
    public interface IInputState<T> : IEquatable<T> where T : struct, IInputState<T>
    {
        void Serialize(BinaryWriter writer);
        static abstract T Deserialize(BinaryReader reader);
    }
}
