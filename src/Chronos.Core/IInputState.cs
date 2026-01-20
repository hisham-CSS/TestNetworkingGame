using System.IO;

namespace Chronos.Core;

/// <summary>
/// Interface for input definitions in the Chronos engine.
/// Implementations must be value types (structs) and support precise binary serialization/deserialization.
/// </summary>
/// <typeparam name="T">The specific input struct type (CRTP pattern).</typeparam>
public interface IInputState<T> : IEquatable<T> where T : struct, IInputState<T>
{
    /// <summary>
    /// Serializes the input state to the provided BinaryWriter.
    /// </summary>
    /// <param name="writer">The target writer.</param>
    void Serialize(BinaryWriter writer);

    /// <summary>
    /// Deserializes a new input state from the provided BinaryReader.
    /// </summary>
    /// <param name="reader">The source reader.</param>
    /// <returns>The deserialized input state.</returns>
    static abstract T Deserialize(BinaryReader reader);
}
