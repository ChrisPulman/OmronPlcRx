// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OmronPlcRx.Core;
using OmronPlcRx.Core.Converters;
using OmronPlcRx.Core.Types;
using OmronPlcRx.Enums;
using OmronPlcRx.Results;
using OmronPlcRx.Tags;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace OmronPlcRx;

/// <summary>
/// Reactive wrapper providing typed tag storage, polling and observation for an <see cref="OmronPLCConnection"/> instance.
/// Implements <see cref="IOmronPlcRx"/> so individual tag streams and a multiplex stream can be consumed.
/// </summary>
public sealed class OmronPlcRx : IOmronPlcRx
{
    /// <summary>Stores the p lc value.</summary>
    private readonly OmronPLCConnection _plc;

    /// <summary>Stores the p ol li nt er va l value.</summary>
    private readonly TimeSpan _pollInterval;

    /// <summary>Executes the e nt ri es operation.</summary>
    private readonly ConcurrentDictionary<string, ITagEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Executes the s ub je ct s operation.</summary>
    private readonly ConcurrentDictionary<string, BehaviorSignal<object?>> _subjects = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Executes the t ag ch an ge d operation.</summary>
    private readonly Signal<IPlcTag?> _tagChanged = new();

    /// <summary>Executes the e rr or s operation.</summary>
    private readonly Signal<OmronPLCException?> _errors = new();

    /// <summary>Executes the c ts operation.</summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Stores the p ol ll oo p value.</summary>
    private readonly Task _pollLoop;

    /// <summary>Stores the d is po se d value.</summary>
    private bool _disposed;

    /// <summary>Stores the p lc in it ia li ze d value.</summary>
    private volatile bool _plcInitialized;

    /// <summary>Initializes a new instance of the <see cref="OmronPlcRx" /> class.</summary>
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

    /// <summary>Initializes a new instance of the <see cref="OmronPlcRx" /> class using serial FINS communications.</summary>
    /// <param name="localNodeId">The local node identifier.</param>
    /// <param name="remoteNodeId">The remote node identifier.</param>
    /// <param name="serialOptions">The serial FINS options.</param>
    /// <param name="timeout">The timeout.</param>
    /// <param name="retries">The retries.</param>
    /// <param name="pollInterval">Polling interval (default 100 ms).</param>
    public OmronPlcRx(byte localNodeId, byte remoteNodeId, OmronSerialOptions serialOptions, int timeout = 2000, int retries = 1, TimeSpan? pollInterval = null)
    {
        if (serialOptions is null)
        {
            throw new ArgumentNullException(nameof(serialOptions));
        }

        _plc = new(localNodeId, remoteNodeId, ConnectionMethod.Serial, serialOptions.PortName, 0, timeout, retries, serialOptions);
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        _pollLoop = Task.Run(PollLoopAsync);
    }

    /// <summary>Tag entry abstraction used internally for polymorphic read access.</summary>
    private interface ITagEntry
    {
        /// <summary>Gets the last cached value as a boxed object.</summary>
        IPlcTag? Tag { get; }

        /// <summary>Reads the tag value from the PLC updating internal state.</summary>
        /// <param name="plc">PLC connection.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the value changed; otherwise false.</returns>
        Task<bool> ReadAsync(OmronPLCConnection plc, CancellationToken ct);
    }

    /// <inheritdoc />
    public IObservable<IPlcTag?> ObserveAll => _tagChanged;

    /// <inheritdoc />
    public IObservable<OmronPLCException?> Errors => _errors;

    /// <inheritdoc />
    public bool IsDisposed => _disposed;

    /// <summary>Gets the plc type value.</summary>
    /// <value>
    /// The type of the PLC.
    /// </value>
    public PLCType PLCType => _plc.PLCType;

    /// <summary>Gets the controller model value.</summary>
    public string? ControllerModel => _plc.ControllerModel;

    /// <summary>Gets the controller version value.</summary>
    public string? ControllerVersion => _plc.ControllerVersion;

    /// <summary>Reads the PLC real-time clock via the underlying connection.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock read result.</returns>
    public Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken = default) => _plc.ReadClockAsync(cancellationToken);

    /// <summary>Writes the PLC real-time clock (day-of-week inferred) via the underlying connection.</summary>
    /// <param name="newDateTime">New date/time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock write result.</returns>
    public Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, CancellationToken cancellationToken = default) => _plc.WriteClockAsync(newDateTime, cancellationToken);

    /// <summary>Writes the PLC real-time clock with explicit day-of-week via the underlying connection.</summary>
    /// <param name="newDateTime">New date/time.</param>
    /// <param name="newDayOfWeek">Day of week (0-6).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock write result.</returns>
    public Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, int newDayOfWeek, CancellationToken cancellationToken = default) => _plc.WriteClockAsync(newDateTime, newDayOfWeek, cancellationToken);

    /// <summary>Reads PLC scan cycle time statistics via the underlying connection.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cycle time statistics.</returns>
    public Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken = default) => _plc.ReadCycleTimeAsync(cancellationToken);

    /// <inheritdoc />
        /// <typeparam name="T">The t value type.</typeparam>
        /// <param name="variable">The variable value.</param>
        /// <param name="tagName">The tag name value.</param>
    public void AddUpdateTagItem<T>(string variable, string tagName)
    {
        ThrowIfNullOrWhiteSpace(variable, nameof(variable));
        ThrowIfNullOrWhiteSpace(tagName, nameof(tagName));

        var tag = new PlcTag<T>(tagName, variable);
        var entry = new TagEntry<T>(tag);
        _ = _entries.AddOrUpdate(tagName, entry, (_, __) => entry);
        _ = _subjects.GetOrAdd(tagName, _ => new(default));
    }

    /// <inheritdoc />
        /// <typeparam name="T">The t value type.</typeparam>
        /// <param name="tagName">The tag name value.</param>
    public IObservable<T?> Observe<T>(string? tagName)
    {
        if (tagName is null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        var subject = _subjects.GetOrAdd(tagName, _ => new(default));
        return subject.Select(v => v is null ? default : (T?)ConvertTo<T>(v));
    }

    /// <inheritdoc />
        /// <typeparam name="T">The t value type.</typeparam>
        /// <param name="tagName">The tag name value.</param>
    public T? Value<T>(string? tagName)
    {
        if (tagName is null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        return !_entries.TryGetValue(tagName, out var entry) || entry is not TagEntry<T> typed || typed.Tag is not PlcTag<T> plcTag ? default : plcTag.Value;
    }

    /// <inheritdoc />
        /// <typeparam name="T">The t value type.</typeparam>
        /// <param name="tagName">The tag name value.</param>
        /// <param name="value">The value to write.</param>
    public void Value<T>(string? tagName, T? value)
    {
        if (tagName is null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        if (!_entries.TryGetValue(tagName, out var entry) || entry is not TagEntry<T> typed)
        {
            throw new KeyNotFoundException($"Tag '{tagName}' not found or incorrect type.");
        }

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

    /// <summary>Dispose pattern.</summary>
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
            _ = _pollLoop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex)
        {
            ex.Handle(static inner => inner is OperationCanceledException);
        }

        foreach (var bs in _subjects.Values)
        {
            bs.OnCompleted();
            bs.Dispose();
        }

        _tagChanged.OnCompleted();
        _errors.OnCompleted();
        _tagChanged.Dispose();
        _errors.Dispose();
        _plc.Dispose();
        _cts.Dispose();
    }

    /// <summary>Executes the c on ve rt to operation.</summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="value">The v al ue value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static object? ConvertTo<T>(object value)
    {
        return value is T t ? t : (T)Convert.ChangeType(value, typeof(T));
    }

    /// <summary>Throws when a string argument is null, empty, or whitespace.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    private static void ThrowIfNullOrWhiteSpace(string? value, string paramName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (value.Trim().Length == 0)
        {
            throw new ArgumentException("The value cannot be empty or whitespace.", paramName);
        }
    }

    /// <summary>Initializes a new instance of the <see cref="ParseAddress"/> class.</summary>
    /// <param name="address">The a dd re ss value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static (string Area, ushort Address, byte? BitIndex) ParseAddress(string address)
    {
        var baseForParse = address;
        var bracketIndex = baseForParse.IndexOf('[');
        if (bracketIndex >= 0 &&
            baseForParse.IndexOf(']', bracketIndex + 1) is var endBracket &&
            endBracket > bracketIndex)
        {
            baseForParse = baseForParse.Remove(bracketIndex, endBracket - bracketIndex + 1);
        }

        var dotIndex = baseForParse.IndexOf('.');
        string basePart;
        string? bitPart = null;
        if (dotIndex >= 0)
        {
            basePart = baseForParse[..dotIndex];
            bitPart = baseForParse[(dotIndex + 1)..];
        }
        else
        {
            basePart = baseForParse;
        }

        byte? bitIndex = null;
        if (bitPart is not null)
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
        var numberPart = basePart.Remove(0, firstDigit);
        if (!ushort.TryParse(numberPart, out var addr))
        {
            throw new FormatException($"Invalid numeric address in '{address}'");
        }

        return (area, addr, bitIndex);
    }

    /// <summary>Initializes a new instance of the <see cref="ExtractStringMeta"/> class.</summary>
    /// <param name="address">The a dd re ss value.</param>
    /// <param name="defaultLength">The d ef au lt le ng th value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static (string BaseAddress, int Length) ExtractStringMeta(string address, int defaultLength = 16)
    {
        var bracketIndex = address.IndexOf('[');
        if (bracketIndex >= 0)
        {
            var end = address.IndexOf(']', bracketIndex + 1);
            if (end > bracketIndex)
            {
                var lenPart = address[(bracketIndex + 1)..end];
                if (int.TryParse(lenPart, out var len) && len > 0)
                {
                    return (address.Remove(bracketIndex, end - bracketIndex + 1), len);
                }
            }
        }

        return (address, defaultLength);
    }

    /// <summary>Initializes a new instance of the <see cref="ToBitType"/> class.</summary>
    /// <param name="area">The a re a value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static MemoryBitDataType ToBitType(string area) => area switch
    {
        "D" or "DM" => MemoryBitDataType.DataMemory,
        "C" or "CIO" => MemoryBitDataType.CommonIO,
        "W" => MemoryBitDataType.Work,
        "H" => MemoryBitDataType.Holding,
        "A" => MemoryBitDataType.Auxiliary,
        _ => throw new ArgumentOutOfRangeException(nameof(area), $"Unsupported bit area '{area}'"),
    };

    /// <summary>Initializes a new instance of the <see cref="ToWordType"/> class.</summary>
    /// <param name="area">The a re a value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static MemoryWordDataType ToWordType(string area) => area switch
    {
        "D" or "DM" => MemoryWordDataType.DataMemory,
        "C" or "CIO" => MemoryWordDataType.CommonIO,
        "W" => MemoryWordDataType.Work,
        "H" => MemoryWordDataType.Holding,
        "A" => MemoryWordDataType.Auxiliary,
        _ => throw new ArgumentOutOfRangeException(nameof(area), $"Unsupported word area '{area}'"),
    };

    /// <summary>Initializes a new instance of the <see cref="PollLoopAsync"/> class.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PollLoopAsync()
    {
        // Lazy initialize PLC once before first poll
        if (!_plcInitialized)
        {
            try
            {
                await _plc.InitializeAsync(_cts.Token).ConfigureAwait(false);
                _plcInitialized = true;
            }
            catch (Exception ex)
            {
                _errors.OnNext(new OmronPLCException("PLC initialization failed", ex));
            }
        }

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
                        if (changed && entry.Tag is IPlcTag tag)
                        {
                            if (_subjects.TryGetValue(name, out var subj))
                            {
                                subj.OnNext(tag.Value);
                            }

                            _tagChanged.OnNext(tag);
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

    /// <summary>Executes the w ri te va lu ea sy nc operation.</summary>
        /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="entry">The e nt ry value.</param>
    /// <param name="value">The v al ue value.</param>
    /// <param name="ct">The c t value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WriteValueAsync<T>(TagEntry<T> entry, T? value, CancellationToken ct)
    {
        if (value is null)
        {
            return;
        }

        if (typeof(T) == typeof(string))
        {
            var raw = entry.Tag.Address;
            var (baseAddr, length) = ExtractStringMeta(raw);
            var (area, addr, bitIndex) = ParseAddress(baseAddr);
            if (bitIndex is not null)
            {
                throw new NotSupportedException("Bit indexing not supported for string types.");
            }

            var str = Convert.ToString(value) ?? string.Empty;
            var bytes = Encoding.ASCII.GetBytes(str);
            if (bytes.Length > length)
            {
                Array.Resize(ref bytes, length);
            }
            else if (bytes.Length < length)
            {
                var padded = new byte[length];
                Array.Copy(bytes, padded, bytes.Length);
                bytes = padded;
            }

            // Ensure even length
            if (bytes.Length % 2 != 0)
            {
                Array.Resize(ref bytes, bytes.Length + 1);
            }

            var wordCount = bytes.Length / 2;
            var words = new short[wordCount];
            for (var i = 0; i < wordCount; i++)
            {
                var b1 = bytes[i * 2];
                var b2 = bytes[(i * 2) + 1];

                // Store first char (b1) in high byte for consistency with other multi-byte handling (network big-end assumption)
                words[i] = (short)((b1 << 8) | b2);
            }

            await _plc.WriteWordsAsync(words, addr, ToWordType(area), ct).ConfigureAwait(false);
            return;
        }

        var (area2, addr2, bitIndex2) = ParseAddress(entry.Tag.Address);

        if (typeof(T) == typeof(bool))
        {
            var b = Convert.ToBoolean(value);
            if (bitIndex2 is null)
            {
                var wordVal = (short)(b ? 1 : 0);
                await _plc.WriteWordsAsync([wordVal], addr2, ToWordType(area2), ct).ConfigureAwait(false);
            }
            else
            {
                await _plc.WriteBitsAsync([b], addr2, bitIndex2.Value, ToBitType(area2), ct).ConfigureAwait(false);
            }
        }
        else if (typeof(T) == typeof(byte))
        {
            var bv = Convert.ToByte(value);
            var word = (short)bv; // store in low byte
            await _plc.WriteWordsAsync([word], addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(ushort))
        {
            var us = Convert.ToUInt16(value);
            var word = unchecked((short)us);
            await _plc.WriteWordsAsync([word], addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(short))
        {
            var s = Convert.ToInt16(value);
            await _plc.WriteWordsAsync([s], addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(int))
        {
            var i = Convert.ToInt32(value);
            var hi = (ushort)((i >> 16) & 0xFFFF);
            var lo = (ushort)(i & 0xFFFF);

            // PLC expects low word first
            short[] words = [unchecked((short)lo), unchecked((short)hi)];
            await _plc.WriteWordsAsync(words, addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(uint))
        {
            var ui = Convert.ToUInt32(value);
            var hi = (ushort)((ui >> 16) & 0xFFFF);
            var lo = (ushort)(ui & 0xFFFF);
            short[] words = [unchecked((short)lo), unchecked((short)hi)];
            await _plc.WriteWordsAsync(words, addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(float))
        {
            var f = Convert.ToSingle(value);
            var bytes = BitConverter.GetBytes(f);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes); // bytes now big-endian overall
            }

            // bytes[0..1] = high-order 16 bits, bytes[2..3] = low-order 16 bits (after reversal)
            var highWord = (ushort)((bytes[0] << 8) | bytes[1]);
            var lowWord = (ushort)((bytes[2] << 8) | bytes[3]);

            // Write low word first, then high word
            short[] words = [unchecked((short)lowWord), unchecked((short)highWord)];
            await _plc.WriteWordsAsync(words, addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(double))
        {
            var d = Convert.ToDouble(value);
            var bytes = BitConverter.GetBytes(d);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes); // big-endian ordering overall
            }

            // Construct 4 words: bytes[0..1] high-most ... bytes[6..7] low-most; need to output low words first
            var highOrderWord0 = (ushort)((bytes[0] << 8) | bytes[1]);
            var highOrderWord1 = (ushort)((bytes[2] << 8) | bytes[3]);
            var lowOrderWord0 = (ushort)((bytes[4] << 8) | bytes[5]);
            var lowOrderWord1 = (ushort)((bytes[6] << 8) | bytes[7]);

            // Order: low words first, then high words
            short[] words = [unchecked((short)lowOrderWord0), unchecked((short)lowOrderWord1), unchecked((short)highOrderWord0), unchecked((short)highOrderWord1)];
            await _plc.WriteWordsAsync(words, addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(Bcd16))
        {
            var numeric = ((Bcd16)(object)value).Value;
            var word = BCDConverter.GetBCDWord(numeric);
            await _plc.WriteWordsAsync([word], addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(BcdU16))
        {
            var numeric = ((BcdU16)(object)value).Value;
            var word = BCDConverter.GetBCDWord(numeric);
            await _plc.WriteWordsAsync([word], addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(Bcd32))
        {
            var numeric = ((Bcd32)(object)value).Value;
            var bcdWords = BCDConverter.GetBCDWords(numeric); // assume [lowWord, highWord]
            short[] words = [bcdWords[0], bcdWords[1]]; // low first
            await _plc.WriteWordsAsync(words, addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
        else if (typeof(T) == typeof(BcdU32))
        {
            var numeric = ((BcdU32)(object)value).Value;
            var bcdWords = BCDConverter.GetBCDWords(numeric);
            short[] words = [bcdWords[0], bcdWords[1]];
            await _plc.WriteWordsAsync(words, addr2, ToWordType(area2), ct).ConfigureAwait(false);
        }
    }

    /// <summary>Represents the t ag en tr y type.</summary>
        /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="tag">The t ag value.</param>
    private sealed class TagEntry<T>(PlcTag<T> tag) : ITagEntry
    {
        /// <summary>Gets the tag value.</summary>
        public IPlcTag Tag { get; } = tag;

        /// <inheritdoc />
        /// <param name="plc">The plc value.</param>
        /// <param name="ct">The ct value.</param>
        public async Task<bool> ReadAsync(OmronPLCConnection plc, CancellationToken ct)
        {
            // String handling
            if (typeof(T) == typeof(string))
            {
                var (baseAddr, length) = ExtractStringMeta(Tag.Address);
                var (area, addr, bitIndex) = ParseAddress(baseAddr);
                if (bitIndex is not null)
                {
                    throw new NotSupportedException("Bit indexing not supported for string types.");
                }

                var wordCount = (length + 1) / 2; // two chars per word
                var words = await plc.ReadWordsAsync(addr, (ushort)wordCount, ToWordType(area), ct).ConfigureAwait(false);
                var bytes = new List<byte>(wordCount * 2);
                for (var i = 0; i < wordCount; i++)
                {
                    var w = (ushort)words.Values[i];
                    bytes.Add((byte)(w >> 8));
                    bytes.Add((byte)(w & 0xFF));
                }

                if (bytes.Count > length)
                {
                    bytes.RemoveRange(length, bytes.Count - length);
                }

                // Trim at first null
                var nullIndex = bytes.IndexOf(0);
                var arr = (nullIndex >= 0 ? bytes.GetRange(0, nullIndex) : bytes).ToArray();
                object newStr = Encoding.ASCII.GetString(arr);
                if (!Equals(newStr, Tag.Value) && Tag is PlcTag<T> plcTagStr)
                {
                    plcTagStr.Value = (T)newStr;
                    return true;
                }

                return false;
            }

            var (area2, addr2, bitIndex2) = ParseAddress(Tag.Address);
            object newVal;
            if (typeof(T) == typeof(bool))
            {
                if (bitIndex2 is null)
                {
                    var word = await plc.ReadWordAsync(addr2, ToWordType(area2), ct).ConfigureAwait(false);
                    newVal = word.Values[0] != 0;
                }
                else
                {
                    var bits = await plc.ReadBitsAsync(addr2, bitIndex2.Value, 1, ToBitType(area2), ct).ConfigureAwait(false);
                    newVal = bits.Values[0];
                }
            }
            else if (typeof(T) == typeof(short))
            {
                var words = await plc.ReadWordsAsync(addr2, 1, ToWordType(area2), ct).ConfigureAwait(false);
                newVal = words.Values[0];
            }
            else if (typeof(T) == typeof(byte))
            {
                var words = await plc.ReadWordsAsync(addr2, 1, ToWordType(area2), ct).ConfigureAwait(false);
                newVal = (byte)(words.Values[0] & 0xFF);
            }
            else if (typeof(T) == typeof(ushort))
            {
                var words = await plc.ReadWordsAsync(addr2, 1, ToWordType(area2), ct).ConfigureAwait(false);
                newVal = (ushort)words.Values[0];
            }
            else if (typeof(T) == typeof(int))
            {
                var words = await plc.ReadWordsAsync(addr2, 2, ToWordType(area2), ct).ConfigureAwait(false);

                // PLC provides low word first
                var lo = (ushort)words.Values[0];
                var hi = (ushort)words.Values[1];
                var composite = ((uint)hi << 16) | lo;
                newVal = unchecked((int)composite);
            }
            else if (typeof(T) == typeof(uint))
            {
                var words = await plc.ReadWordsAsync(addr2, 2, ToWordType(area2), ct).ConfigureAwait(false);
                var lo = (uint)(ushort)words.Values[0];
                var hi = (uint)(ushort)words.Values[1];
                newVal = (hi << 16) | lo;
            }
            else if (typeof(T) == typeof(float))
            {
                var words = await plc.ReadWordsAsync(addr2, 2, ToWordType(area2), ct).ConfigureAwait(false);

                // low word first
                var lo = (ushort)words.Values[0];
                var hi = (ushort)words.Values[1];
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
            else if (typeof(T) == typeof(double))
            {
                var words = await plc.ReadWordsAsync(addr2, 4, ToWordType(area2), ct).ConfigureAwait(false);

                // words: [low2][low3][high0][high1]
                var w0 = (ushort)words.Values[2];
                var w1 = (ushort)words.Values[3];
                var w2 = (ushort)words.Values[0];
                var w3 = (ushort)words.Values[1];
                var bytes = new byte[8]
                {
                    (byte)(w0 >> 8), (byte)(w0 & 0xFF),
                    (byte)(w1 >> 8), (byte)(w1 & 0xFF),
                    (byte)(w2 >> 8), (byte)(w2 & 0xFF),
                    (byte)(w3 >> 8), (byte)(w3 & 0xFF)
                };
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                newVal = BitConverter.ToDouble(bytes, 0);
            }
            else if (typeof(T) == typeof(Bcd16))
            {
                var words = await plc.ReadWordsAsync(addr2, 1, ToWordType(area2), ct).ConfigureAwait(false);
                var val = BCDConverter.ToInt16(words.Values[0]);
                newVal = new Bcd16(val);
            }
            else if (typeof(T) == typeof(BcdU16))
            {
                var words = await plc.ReadWordsAsync(addr2, 1, ToWordType(area2), ct).ConfigureAwait(false);
                var val = BCDConverter.ToUInt16(words.Values[0]);
                newVal = new BcdU16(val);
            }
            else if (typeof(T) == typeof(Bcd32))
            {
                var words = await plc.ReadWordsAsync(addr2, 2, ToWordType(area2), ct).ConfigureAwait(false);
                var low = words.Values[0];
                var high = words.Values[1];
                var val = BCDConverter.ToInt32(low, high);
                newVal = new Bcd32(val);
            }
            else if (typeof(T) == typeof(BcdU32))
            {
                var words = await plc.ReadWordsAsync(addr2, 2, ToWordType(area2), ct).ConfigureAwait(false);
                var low = words.Values[0];
                var high = words.Values[1];
                var val = BCDConverter.ToUInt32(low, high);
                newVal = new BcdU32(val);
            }
            else
            {
                throw new NotSupportedException($"Tag type '{nameof(T)}' not supported.");
            }

            if (Equals(newVal, Tag.Value) || Tag is not PlcTag<T> plcTag)
            {
                return false;
            }

            plcTag.Value = (T)newVal;
            return true;
        }
    }
}
