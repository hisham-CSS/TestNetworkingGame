using System.IO;

namespace Chronos.Core;

/// <summary>
/// Interface for input states in the Chronos engine.
/// Implementations must be structs and support binary serialization.
/// </summary>
public interface IInputState<T> : IEquatable<T> where T : struct, IInputState<T>
{
    void Serialize(BinaryWriter writer);
    static abstract T Deserialize(BinaryReader reader);
}
