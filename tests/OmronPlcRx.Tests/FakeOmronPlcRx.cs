// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using OmronPlcRx.Enums;
using OmronPlcRx.Results;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace OmronPlcRx.Tests;

/// <summary>In-memory PLC test double used by generated stream tests.</summary>
internal sealed class FakeOmronPlcRx : IOmronPlcRx
{
    /// <summary>Publishes PLC errors.</summary>
    private readonly Signal<OmronPLCException?> _errors = new();

    /// <summary>Stores per-tag value subjects.</summary>
    private readonly Dictionary<string, BehaviorSignal<object?>> _subjects = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores the latest per-tag values.</summary>
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Publishes aggregate tag change notifications.</summary>
    private readonly Signal<global::OmronPlcRx.Tags.IPlcTag?> _all = new();

    /// <summary>Gets the tag registrations captured by this fake PLC.</summary>
    public List<Registration> Registrations { get; } = [];

    /// <summary>Gets the writes captured by this fake PLC.</summary>
    public List<Write> Writes { get; } = [];

    /// <inheritdoc />
    public IObservable<global::OmronPlcRx.Tags.IPlcTag?> ObserveAll => _all;

    /// <inheritdoc />
    public IObservable<OmronPLCException?> Errors => _errors;

    /// <inheritdoc />
    public PLCType PLCType => PLCType.Unknown;

    /// <inheritdoc />
    public string? ControllerModel => null;

    /// <inheritdoc />
    public string? ControllerVersion => null;

    /// <summary>Gets a value indicating whether this fake PLC is disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public void AddUpdateTagItem<T>(string variable, string tagName)
    {
        Registrations.Add(new(tagName, variable, typeof(T)));
        _ = GetSubject(tagName);
    }

    /// <inheritdoc />
    public IObservable<T?> Observe<T>(string? tagName)
    {
        if (tagName is null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        return GetSubject(tagName).Select(value => value is null ? default : (T?)value);
    }

    /// <inheritdoc />
    public T? Value<T>(string? tagName)
    {
        if (tagName is null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        return _values.TryGetValue(tagName, out var value) && value is T typed ? typed : default;
    }

    /// <inheritdoc />
    public void Value<T>(string? tagName, T? value)
    {
        if (tagName is null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        Writes.Add(new(tagName, value, typeof(T)));
        Publish(tagName, value);
    }

    /// <inheritdoc />
    public Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(default(ReadClockResult));

    /// <inheritdoc />
    public Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, CancellationToken cancellationToken = default) =>
        Task.FromResult(default(WriteClockResult));

    /// <inheritdoc />
    public Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, int newDayOfWeek, CancellationToken cancellationToken = default) =>
        Task.FromResult(default(WriteClockResult));

    /// <inheritdoc />
    public Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(default(ReadCycleTimeResult));

    /// <summary>Publishes a tag value to observers.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="tagName">The tag name.</param>
    /// <param name="value">The tag value.</param>
    public void Publish<T>(string tagName, T? value)
    {
        _values[tagName] = value;
        GetSubject(tagName).OnNext(value);
        _all.OnNext(null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        foreach (var subject in _subjects.Values)
        {
            subject.OnCompleted();
            subject.Dispose();
        }

        _all.OnCompleted();
        _all.Dispose();
        _errors.OnCompleted();
        _errors.Dispose();
    }

    /// <summary>Gets or creates the subject for a tag.</summary>
    /// <param name="tagName">The tag name.</param>
    /// <returns>The tag value subject.</returns>
    private BehaviorSignal<object?> GetSubject(string tagName)
    {
        if (_subjects.TryGetValue(tagName, out var subject))
        {
            return subject;
        }

        subject = new(default);
        _subjects.Add(tagName, subject);
        return subject;
    }

    /// <summary>Captures a registered PLC tag.</summary>
    /// <param name="TagName">The tag name.</param>
    /// <param name="Address">The PLC address.</param>
    /// <param name="TagType">The tag value type.</param>
    public sealed record Registration(string TagName, string Address, Type TagType);

    /// <summary>Captures a PLC tag write.</summary>
    /// <param name="TagName">The tag name.</param>
    /// <param name="Value">The written value.</param>
    /// <param name="TagType">The tag value type.</param>
    public sealed record Write(string TagName, object? Value, Type TagType);
}
