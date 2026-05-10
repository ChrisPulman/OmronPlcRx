// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Reactive.Subjects;
using OmronPlcRx.Enums;
using OmronPlcRx.Results;

namespace OmronPlcRx.Tests;

internal sealed class FakeOmronPlcRx : IOmronPlcRx
{
    private readonly Subject<OmronPLCException?> _errors = new();
    private readonly Dictionary<string, BehaviorSubject<object?>> _subjects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Subject<global::OmronPlcRx.Tags.IPlcTag?> _all = new();

    public List<Registration> Registrations { get; } = [];

    public List<Write> Writes { get; } = [];

    public IObservable<global::OmronPlcRx.Tags.IPlcTag?> ObserveAll => _all.AsObservable();

    public IObservable<OmronPLCException?> Errors => _errors.AsObservable();

    public PLCType PLCType => PLCType.Unknown;

    public string? ControllerModel => null;

    public string? ControllerVersion => null;

    public bool IsDisposed { get; private set; }

    public void AddUpdateTagItem<T>(string variable, string tagName)
    {
        Registrations.Add(new(tagName, variable, typeof(T)));
        _ = GetSubject(tagName);
    }

    public IObservable<T?> Observe<T>(string? tagName)
    {
        if (tagName == null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        return GetSubject(tagName).Select(value => value is null ? default : (T?)value);
    }

    public T? Value<T>(string? tagName)
    {
        if (tagName == null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        return _values.TryGetValue(tagName, out var value) && value is T typed ? typed : default;
    }

    public void Value<T>(string? tagName, T? value)
    {
        if (tagName == null)
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        Writes.Add(new(tagName, value, typeof(T)));
        Publish(tagName, value);
    }

    public Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(default(ReadClockResult));

    public Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, CancellationToken cancellationToken = default) =>
        Task.FromResult(default(WriteClockResult));

    public Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, int newDayOfWeek, CancellationToken cancellationToken = default) =>
        Task.FromResult(default(WriteClockResult));

    public Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(default(ReadCycleTimeResult));

    public void Publish<T>(string tagName, T? value)
    {
        _values[tagName] = value;
        GetSubject(tagName).OnNext(value);
        _all.OnNext(null);
    }

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

    private BehaviorSubject<object?> GetSubject(string tagName)
    {
        if (_subjects.TryGetValue(tagName, out var subject))
        {
            return subject;
        }

        subject = new(default);
        _subjects.Add(tagName, subject);
        return subject;
    }

    public sealed record Registration(string TagName, string Address, Type TagType);

    public sealed record Write(string TagName, object? Value, Type TagType);
}
