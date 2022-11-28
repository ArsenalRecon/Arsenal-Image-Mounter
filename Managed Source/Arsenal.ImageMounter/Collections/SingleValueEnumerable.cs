using System;
using System.Collections;
using System.Collections.Generic;

namespace Arsenal.ImageMounter.Collections;

/// <summary>
/// Features for allocation-free enumerators for single values
/// </summary>
public static class SingleValueEnumerable
{
    /// <summary>
    /// Gets an allocation-free enumerable for a single value
    /// </summary>
    /// <typeparam name="T">Type of value</typeparam>
    /// <param name="value">Value</param>
    /// <returns>Value enumerable</returns>
    public static SingleValueEnumerable<T?> Get<T>(T? value) => new(value);
}

/// <summary>
/// An allocation-free enumerable for a single value
/// </summary>
/// <typeparam name="T">Type of value</typeparam>
public readonly struct SingleValueEnumerable<T> : IEnumerable<T?>
{
    /// <summary>
    /// Value that will be returned once for this enumeration.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Initializes an allocation-free enumerable for a single value
    /// </summary>
    /// <param name="value">Value</param>
    /// <returns>Value enumerable</returns>
    public SingleValueEnumerable(T value)
    {
        Value = value;
    }

    /// <summary>
    /// Initializes an allocation-free single value enumerator for stored value
    /// </summary>
    /// <returns>Value enumerator</returns>
    public IEnumerator<T?> GetEnumerator() => new SingleValueEnumerator(Value);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// An allocation-free enumerator for a single value
    /// </summary>
    public struct SingleValueEnumerator : IEnumerator<T?>
    {
        /// <summary>
        /// Initializes an allocation-free single value enumerator for a value
        /// </summary>
        /// <param name="value">Value to enumerate once</param>
        /// <returns>Value enumerator</returns>
        public SingleValueEnumerator(T? value) : this()
        {
            Value = value;
        }

        /// <summary>
        /// Value that will be enumerated once
        /// </summary>
        public T? Value { get; }

        /// <summary>
        /// Indicates whether enumerator has started
        /// </summary>
        public bool Started { get; private set; }

        /// <summary>
        /// Returns default if enumeration has not started, otherwise the supplied value
        /// </summary>
        public T? Current => Started ? Value : default;

        object? IEnumerator.Current => Current;

        void IDisposable.Dispose() { }

        /// <summary>
        /// Starts enumeration by returning true on first call and then always false
        /// </summary>
        /// <returns>true on first call and then always false</returns>
        public bool MoveNext()
        {
            if (Started)
            {
                return false;
            }

            Started = true;
            return true;
        }

        /// <summary>
        /// Resets state of enumerator to enumerate supplied value once again
        /// </summary>
        public void Reset() => Started = false;
    }
}
