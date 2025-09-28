// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using OmronPlcRx.Core;
using OmronPlcRx.Enums;
using OmronPlcRx.Tags;

namespace OmronPlcRx;

/// <summary>
/// Reactive wrapper providing typed tag storage, polling and observation for an <see cref="OmronPLCConnection"/> instance.
/// Implements <see cref="IOmronPlcRx"/> so individual tag streams and a multiplex stream can be consumed.
/// </summary>
public sealed class OmronPlcRx : IOmronPlcRx
{
    private readonly OmronPLCConnection _plc;
    private readonly TimeSpan _pollInterval;
    private readonly ConcurrentDictionary<string, ITagEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object> _subjects = new(StringComparer.OrdinalIgnoreCase); // value is BehaviorSubject<object?>
    private readonly Subject<object?> _tagChanged = new();
    private readonly Subject<OmronPLCException?> _errors = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pollLoop;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmronPlcRx" /> class.
    /// </summary>
    /// <param name="localNodeId">The local node identifier.</param>
    /// <param name="remoteNodeId">The remote node identifier.</param>
    /// <param name="connectionMethod">The connection method.</param>
    /// <param name="remoteHost">The remote host.</param>
    /// <param name="port">The port.</param>
    /// <param name="timeout">The timeout.</param>
    /// <param name="retries">The retries.</param>
    /// <param name="pollInterval">Polling interval (default 100 ms).</param>
    public OmronPlcRx(byte localNodeId, byte remoteNodeId, ConnectionMethod connectionMethod, string remoteHost, int port = 9600, int timeout = 2000, int retries = 1, TimeSpan? pollInterval = null)
    {
        _plc = new(localNodeId, remoteNodeId, connectionMethod, remoteHost, port, timeout, retries);
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        _pollLoop = Task.Run(PollLoopAsync);
    }

    /// <summary>
    /// ITagEntry.
    /// </summary>
    private interface ITagEntry
    {
        /// <summary>
        /// Gets the boxed value.
        /// </summary>
        /// <value>
        /// The boxed value.
        /// </value>
        object BoxedValue { get; }

        /// <summary>
        /// Reads the asynchronous.
        /// </summary>
        /// <param name="plc">The PLC.</param>
        /// <param name="ct">The ct.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<bool> ReadAsync(OmronPLCConnection plc, CancellationToken ct);
    }

    /// <inheritdoc />
    public IObservable<object?> ObserveAll => _tagChanged.AsObservable();

    /// <inheritdoc />
    public IObservable<OmronPLCException?> Errors => _errors.AsObservable();

    /// <inheritdoc />
    public bool IsDisposed => _disposed;

    /// <inheritdoc />
    public void AddUpdateTagItem<T>(string variable, string tagName)
    {
        if (string.IsNullOrWhiteSpace(variable))
        {
            throw new ArgumentNullException(nameof(variable));
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        var tag = new PlcTag<T>(tagName, variable);
        var entry = new TagEntry<T>(tag);
        _entries.AddOrUpdate(tagName, entry, (_, __) => entry);
        _subjects.GetOrAdd(tagName, _ => new BehaviorSubject<object?>(default));
    }

    /// <inheritdoc />
    public IObservable<T?> Observe<T>(string? tagName)
    {
        if (tagName == null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        var subject = (BehaviorSubject<object?>)_subjects.GetOrAdd(tagName, _ => new BehaviorSubject<object?>(default));
        return subject.Select(v => v is null ? default : (T?)ConvertTo<T>(v));
    }

    /// <inheritdoc />
    public T? Value<T>(string? tagName)
    {
        if (tagName == null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        if (_entries.TryGetValue(tagName, out var entry) && entry is TagEntry<T> typed)
        {
            return typed.Tag.Value;
        }

        return default;
    }

    /// <inheritdoc />
    public void Value<T>(string? tagName, T? value)
    {
        if (tagName == null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        if (!_entries.TryGetValue(tagName, out var entry) || entry is not TagEntry<T> typed)
        {
            throw new KeyNotFoundException($"Tag '{tagName}' not found or incorrect type.");
        }

        // Write to PLC synchronously by scheduling (do not block caller)
        _ = Task.Run(async () =>
        {
            try
            {
                await WriteValueAsync(typed, value, _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _errors.OnNext(new OmronPLCException($"Failed to write tag '{tagName}'", ex));
            }
        });
    }

    /// <summary>
    /// Dispose pattern.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        try
        {
            _pollLoop.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        foreach (var s in _subjects.Values)
        {
            if (s is BehaviorSubject<object?> bs)
            {
                bs.OnCompleted();
                bs.Dispose();
            }
        }

        _tagChanged.OnCompleted();
        _errors.OnCompleted();
        _tagChanged.Dispose();
        _errors.Dispose();
        _plc.Dispose();
        _cts.Dispose();
    }

    private static object? ConvertTo<T>(object value)
    {
        if (value is T t)
        {
            return t;
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static (string Area, ushort Address, byte? BitIndex) ParseAddress(string address)
    {
        var dotIndex = address.IndexOf('.');
        string basePart;
        string bitPart = null;
        if (dotIndex >= 0)
        {
            basePart = address.Substring(0, dotIndex);
            bitPart = address[(dotIndex + 1)..];
        }
        else
        {
            basePart = address;
        }

        byte? bitIndex = null;
        if (bitPart != null)
        {
            if (!byte.TryParse(bitPart, out var bi) || bi > 15)
            {
                throw new FormatException($"Invalid bit index in address '{address}'");
            }

            bitIndex = bi;
        }

        var firstDigit = -1;
        for (var i = 0; i < basePart.Length; i++)
        {
            if (char.IsDigit(basePart[i]))
            {
                firstDigit = i;
                break;
            }
        }

        if (firstDigit < 0)
        {
            throw new FormatException($"No numeric portion in address '{address}'");
        }

        var area = basePart[..firstDigit].ToUpperInvariant();
        var numberPart = basePart[firstDigit..];
        if (!ushort.TryParse(numberPart, out var addr))
        {
            throw new FormatException($"Invalid numeric address in '{address}'");
        }

        return (area, addr, bitIndex);
    }

    private static MemoryBitDataType ToBitType(string area) => area switch
    {
        "D" or "DM" => MemoryBitDataType.DataMemory,
        "C" or "CIO" => MemoryBitDataType.CommonIO,
        "W" => MemoryBitDataType.Work,
        "H" => MemoryBitDataType.Holding,
        "A" => MemoryBitDataType.Auxiliary,
        _ => throw new ArgumentOutOfRangeException(nameof(area), $"Unsupported bit area '{area}'"),
    };

    private static MemoryWordDataType ToWordType(string area) => area switch
    {
        "D" or "DM" => MemoryWordDataType.DataMemory,
        "C" or "CIO" => MemoryWordDataType.CommonIO,
        "W" => MemoryWordDataType.Work,
        "H" => MemoryWordDataType.Holding,
        "A" => MemoryWordDataType.Auxiliary,
        _ => throw new ArgumentOutOfRangeException(nameof(area), $"Unsupported word area '{area}'"),
    };

    private async Task PollLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                foreach (var kvp in _entries)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }

                    var name = kvp.Key;
                    var entry = kvp.Value;
                    try
                    {
                        var changed = await entry.ReadAsync(_plc, _cts.Token).ConfigureAwait(false);
                        if (changed)
                        {
                            if (_subjects.TryGetValue(name, out var obj) && obj is BehaviorSubject<object?> subj)
                            {
                                subj.OnNext(entry.BoxedValue);
                            }

                            _tagChanged.OnNext(name);
                        }
                    }
                    catch (OmronPLCException ex)
                    {
                        _errors.OnNext(new OmronPLCException(ex.Message, ex));
                    }
                    catch (Exception ex)
                    {
                        _errors.OnNext(new OmronPLCException($"Unexpected error reading tag '{name}'", ex));
                    }
                }
            }
            catch (Exception loopEx)
            {
                _errors.OnNext(new OmronPLCException("Polling loop failure", loopEx));
            }

            try
            {
                await Task.Delay(_pollInterval, _cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task WriteValueAsync<T>(TagEntry<T> entry, T? value, CancellationToken ct)
    {
        if (value == null)
        {
            return;
        }

        var (area, addr, bitIndex) = ParseAddress(entry.Tag.Address);

        if (typeof(T) == typeof(bool))
        {
            var b = Convert.ToBoolean(value);
            if (bitIndex is null)
            {
                // treat as word bool (0/1)
                var wordVal = (short)(b ? 1 : 0);
                await _plc.WriteWordsAsync(new short[] { wordVal }, addr, ToWordType(area), ct).ConfigureAwait(false);
            }
            else
            {
                await _plc.WriteBitsAsync(new bool[] { b }, addr, bitIndex.Value, ToBitType(area), ct).ConfigureAwait(false);
            }
        }
        else if (typeof(T) == typeof(short))
        {
            var s = Convert.ToInt16(value);
            await _plc.WriteWordsAsync(new short[] { s }, addr, ToWordType(area), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(int))
        {
            var i = Convert.ToInt32(value);
            var hi = (ushort)((i >> 16) & 0xFFFF);
            var lo = (ushort)(i & 0xFFFF);
            short[] words = { unchecked((short)hi), unchecked((short)lo) };
            await _plc.WriteWordsAsync(words, addr, ToWordType(area), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(float))
        {
            var f = Convert.ToSingle(value);
            var bytes = BitConverter.GetBytes(f);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            var hi = (ushort)((bytes[0] << 8) | bytes[1]);
            var lo = (ushort)((bytes[2] << 8) | bytes[3]);
            short[] words = { unchecked((short)hi), unchecked((short)lo) };
            await _plc.WriteWordsAsync(words, addr, ToWordType(area), ct).ConfigureAwait(false);
        }
        else
        {
            throw new NotSupportedException($"Write not supported for type '{typeof(T).Name}'");
        }

        entry.Tag.WriteValue = value;
    }

    private sealed class TagEntry<T>(PlcTag<T> tag) : ITagEntry
    {
        public PlcTag<T> Tag { get; } = tag;

        public object BoxedValue => Tag.Value;

        public async Task<bool> ReadAsync(OmronPLCConnection plc, CancellationToken ct)
        {
            var (area, addr, bitIndex) = ParseAddress(Tag.Address);
            object newVal;

            if (typeof(T) == typeof(bool))
            {
                if (bitIndex is null)
                {
                    var word = await plc.ReadWordAsync(addr, ToWordType(area), ct).ConfigureAwait(false);
                    newVal = word.Values[0] != 0;
                }
                else
                {
                    var bits = await plc.ReadBitsAsync(addr, bitIndex.Value, 1, ToBitType(area), ct).ConfigureAwait(false);
                    newVal = bits.Values[0];
                }
            }
            else if (typeof(T) == typeof(short))
            {
                var words = await plc.ReadWordsAsync(addr, 1, ToWordType(area), ct).ConfigureAwait(false);
                newVal = words.Values[0];
            }
            else if (typeof(T) == typeof(int))
            {
                var words = await plc.ReadWordsAsync(addr, 2, ToWordType(area), ct).ConfigureAwait(false);
                var hi = (ushort)words.Values[0];
                var lo = (ushort)words.Values[1];
                var composite = ((uint)hi << 16) | lo;
                newVal = unchecked((int)composite);
            }
            else if (typeof(T) == typeof(float))
            {
                var words = await plc.ReadWordsAsync(addr, 2, ToWordType(area), ct).ConfigureAwait(false);
                var hi = (ushort)words.Values[0];
                var lo = (ushort)words.Values[1];
                var bytes = new byte[4]
                {
                    (byte)(hi >> 8), (byte)(hi & 0xFF), (byte)(lo >> 8), (byte)(lo & 0xFF)
                };
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                newVal = BitConverter.ToSingle(bytes, 0);
            }
            else
            {
                throw new NotSupportedException($"Tag type '{typeof(T).Name}' not supported.");
            }

            if (!Equals(newVal, Tag.Value))
            {
                Tag.Value = (T)newVal;
                return true;
            }

            return false;
        }
    }
}
