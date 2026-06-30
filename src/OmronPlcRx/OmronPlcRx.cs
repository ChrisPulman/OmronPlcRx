// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmronPlcRx.Core;
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
        switch (value)
        {
            case null:
                throw new ArgumentNullException(paramName);
            case var text when text.Trim().Length == 0:
                throw new ArgumentException("The value cannot be empty or whitespace.", paramName);
        }
    }

    /// <summary>Initializes a new instance of the <see cref="ParseAddress"/> class.</summary>
    /// <param name="address">The a dd re ss value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static (string Area, ushort Address, byte? BitIndex) ParseAddress(string address)
    {
        var baseForParse = RemoveLengthSpecifier(address);
        var (basePart, bitPart) = SplitBitPart(baseForParse);
        var bitIndex = ParseBitIndex(bitPart, address);
        var firstDigit = FindFirstDigit(basePart);

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

    /// <summary>Removes a string length specifier from an address.</summary>
    /// <param name="address">The address to parse.</param>
    /// <returns>The address without a length specifier.</returns>
    private static string RemoveLengthSpecifier(string address)
    {
        var bracketIndex = address.IndexOf('[');
        if (bracketIndex < 0)
        {
            return address;
        }

        var endBracket = address.IndexOf(']', bracketIndex + 1);
        return endBracket > bracketIndex ? address.Remove(bracketIndex, endBracket - bracketIndex + 1) : address;
    }

    /// <summary>Splits an address into base and bit-index parts.</summary>
    /// <param name="address">The address to parse.</param>
    /// <returns>The base and bit-index parts.</returns>
    private static (string BasePart, string? BitPart) SplitBitPart(string address)
    {
        var dotIndex = address.IndexOf('.');
        return dotIndex >= 0 ? (address[..dotIndex], address[(dotIndex + 1)..]) : (address, null);
    }

    /// <summary>Parses an optional bit index.</summary>
    /// <param name="bitPart">The bit-index text.</param>
    /// <param name="address">The source address.</param>
    /// <returns>The bit index, if present.</returns>
    private static byte? ParseBitIndex(string? bitPart, string address)
    {
        if (bitPart is null)
        {
            return null;
        }

        if (byte.TryParse(bitPart, out var bitIndex) && bitIndex <= 15)
        {
            return bitIndex;
        }

        throw new FormatException($"Invalid bit index in address '{address}'");
    }

    /// <summary>Finds the first digit in a string.</summary>
    /// <param name="value">The value to scan.</param>
    /// <returns>The zero-based digit index, or -1.</returns>
    private static int FindFirstDigit(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsDigit(value[i]))
            {
                return i;
            }
        }

        return -1;
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
        await InitializePlcForPollingAsync().ConfigureAwait(false);

        while (!_cts.IsCancellationRequested)
        {
            await PollEntriesOnceAsync().ConfigureAwait(false);
            if (!await DelayUntilNextPollAsync().ConfigureAwait(false))
            {
                break;
            }
        }
    }

    /// <summary>Initializes the PLC before polling starts.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task InitializePlcForPollingAsync()
    {
        if (_plcInitialized)
        {
            return;
        }

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

    /// <summary>Polls all registered tag entries once.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PollEntriesOnceAsync()
    {
        try
        {
            foreach (var kvp in _entries)
            {
                if (_cts.IsCancellationRequested)
                {
                    break;
                }

                await PollEntryAsync(kvp.Key, kvp.Value).ConfigureAwait(false);
            }
        }
        catch (Exception loopEx)
        {
            _errors.OnNext(new OmronPLCException("Polling loop failure", loopEx));
        }
    }

    /// <summary>Polls one tag entry.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="entry">The tag entry.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PollEntryAsync(string name, ITagEntry entry)
    {
        try
        {
            var changed = await entry.ReadAsync(_plc, _cts.Token).ConfigureAwait(false);
            PublishChangedTag(name, entry, changed);
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

    /// <summary>Publishes changed tag values to observers.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="entry">The tag entry.</param>
    /// <param name="changed">A value indicating whether the entry changed.</param>
    private void PublishChangedTag(string name, ITagEntry entry, bool changed)
    {
        if (!changed || entry.Tag is not IPlcTag tag)
        {
            return;
        }

        if (_subjects.TryGetValue(name, out var subject))
        {
            subject.OnNext(tag.Value);
        }

        _tagChanged.OnNext(tag);
    }

    /// <summary>Delays until the next poll interval.</summary>
    /// <returns>A value indicating whether polling should continue.</returns>
    private async Task<bool> DelayUntilNextPollAsync()
    {
        try
        {
            await Task.Delay(_pollInterval, _cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
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
            await WriteStringValueAsync(entry.Tag.Address, value, ct).ConfigureAwait(false);
            return;
        }

        var (area, addr, bitIndex) = ParseAddress(entry.Tag.Address);
        await WriteNonStringValueAsync(typeof(T), (object)value!, area, addr, bitIndex, ct).ConfigureAwait(false);
    }

    /// <summary>Writes a string value to word memory.</summary>
    /// <param name="address">The tag address.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WriteStringValueAsync(string address, object value, CancellationToken ct)
    {
        var (baseAddr, length) = ExtractStringMeta(address);
        var (area, addr, bitIndex) = ParseAddress(baseAddr);
        PlcTagValueCodec.ThrowIfBitIndexedString(bitIndex);
        var words = PlcTagValueCodec.GetStringWords(value, length);
        await _plc.WriteWordsAsync(words, addr, ToWordType(area), ct).ConfigureAwait(false);
    }

    /// <summary>Writes a non-string value to PLC memory.</summary>
    /// <param name="type">The value type.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="area">The memory area.</param>
    /// <param name="addr">The memory address.</param>
    /// <param name="bitIndex">The optional bit index.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WriteNonStringValueAsync(Type type, object value, string area, ushort addr, byte? bitIndex, CancellationToken ct)
    {
        if (type == typeof(bool))
        {
            await WriteBooleanValueAsync(Convert.ToBoolean(value), area, addr, bitIndex, ct).ConfigureAwait(false);
            return;
        }

        if (PlcTagValueCodec.TryGetSingleWord(type, value, out var word))
        {
            await _plc.WriteWordsAsync([word], addr, ToWordType(area), ct).ConfigureAwait(false);
            return;
        }

        if (!PlcTagValueCodec.TryGetWordArray(type, value, out var words))
        {
            return;
        }

        await _plc.WriteWordsAsync(words, addr, ToWordType(area), ct).ConfigureAwait(false);
    }

    /// <summary>Writes a Boolean value as either a bit or word.</summary>
    /// <param name="value">The Boolean value.</param>
    /// <param name="area">The memory area.</param>
    /// <param name="addr">The memory address.</param>
    /// <param name="bitIndex">The optional bit index.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WriteBooleanValueAsync(bool value, string area, ushort addr, byte? bitIndex, CancellationToken ct)
    {
        if (bitIndex is null)
        {
            await _plc.WriteWordsAsync([(short)(value ? 1 : 0)], addr, ToWordType(area), ct).ConfigureAwait(false);
            return;
        }

        await _plc.WriteBitsAsync([value], addr, bitIndex.Value, ToBitType(area), ct).ConfigureAwait(false);
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
            var newValue = await ReadValueAsync(plc, ct).ConfigureAwait(false);
            return UpdateValue(newValue);
        }

        /// <summary>Reads a value from the PLC.</summary>
        /// <param name="plc">The PLC connection.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The read value.</returns>
        private async Task<object> ReadValueAsync(OmronPLCConnection plc, CancellationToken ct)
        {
            if (typeof(T) == typeof(string))
            {
                return await ReadStringValueAsync(plc, ct).ConfigureAwait(false);
            }

            var (area, addr, bitIndex) = ParseAddress(Tag.Address);
            if (typeof(T) == typeof(bool))
            {
                return await PlcTagValueCodec.ReadBooleanValueAsync(plc, ToWordType(area), ToBitType(area), addr, bitIndex, ct).ConfigureAwait(false);
            }

            var wordCount = PlcTagValueCodec.GetReadWordCount(typeof(T));
            if (wordCount == 0)
            {
                throw new NotSupportedException($"Tag type '{nameof(T)}' not supported.");
            }

            var words = await plc.ReadWordsAsync(addr, (ushort)wordCount, ToWordType(area), ct).ConfigureAwait(false);
            return PlcTagValueCodec.ConvertReadWords(typeof(T), words.Values);
        }

        /// <summary>Reads a string value from the PLC.</summary>
        /// <param name="plc">The PLC connection.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The string value.</returns>
        private async Task<object> ReadStringValueAsync(OmronPLCConnection plc, CancellationToken ct)
        {
            var (baseAddr, length) = ExtractStringMeta(Tag.Address);
            var (area, addr, bitIndex) = ParseAddress(baseAddr);
            PlcTagValueCodec.ThrowIfBitIndexedString(bitIndex);
            var wordCount = (length + 1) / 2;
            var words = await plc.ReadWordsAsync(addr, (ushort)wordCount, ToWordType(area), ct).ConfigureAwait(false);
            return PlcTagValueCodec.GetStringFromWords(words.Values, length, wordCount);
        }

        /// <summary>Updates the cached tag value.</summary>
        /// <param name="newValue">The new value.</param>
        /// <returns>A value indicating whether the cached value changed.</returns>
        private bool UpdateValue(object newValue)
        {
            if (Equals(newValue, Tag.Value) || Tag is not PlcTag<T> plcTag)
            {
                return false;
            }

            plcTag.Value = (T)newValue;
            return true;
        }
    }
}
