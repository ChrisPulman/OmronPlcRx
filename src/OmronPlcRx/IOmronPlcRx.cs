// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using OmronPlcRx.Enums;
using OmronPlcRx.Results;
using OmronPlcRx.Tags;

namespace OmronPlcRx;

/// <summary>
/// IOmronPlcRx.
/// </summary>
public interface IOmronPlcRx : ICancelable
{
    /// <summary>Gets an observable of all tag change events.</summary>
    IObservable<IPlcTag?> ObserveAll { get; }

    /// <summary>Gets an observable of operational errors.</summary>
    IObservable<OmronPLCException?> Errors { get; }

    /// <summary>Gets the detected PLC type.</summary>
    PLCType PLCType { get; }

    /// <summary>Gets the PLC controller model string.</summary>
    string? ControllerModel { get; }

    /// <summary>Gets the PLC controller version string.</summary>
    string? ControllerVersion { get; }

    /// <summary>Gets a value indicating whether the instance has been disposed.</summary>
    bool IsDisposed { get; }

    /// <summary>Registers or updates a tag definition.</summary>
    /// <typeparam name="T">Tag value type.</typeparam>
    /// <param name="variable">PLC address (e.g. D100, D100.0, D200[20]).</param>
    /// <param name="tagName">Logical tag name.</param>
    void AddUpdateTagItem<T>(string variable, string tagName);

    /// <summary>Observes a tag value stream.</summary>
    /// <typeparam name="T">Tag type.</typeparam>
    /// <param name="tagName">Registered tag name.</param>
    /// <returns>Observable sequence of values.</returns>
    IObservable<T?> Observe<T>(string? tagName);

    /// <summary>Gets last cached value for a tag.</summary>
    /// <typeparam name="T">Tag type.</typeparam>
    /// <param name="tagName">Registered tag name.</param>
    /// <returns>Cached value or default.</returns>
    T? Value<T>(string? tagName);

    /// <summary>Asynchronously writes a value to the PLC (fire-and-forget).</summary>
    /// <typeparam name="T">Tag type.</typeparam>
    /// <param name="tagName">Registered tag name.</param>
    /// <param name="value">Value to write.</param>
    void Value<T>(string? tagName, T? value);

    /// <summary>Reads the PLC real-time clock.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock read result.</returns>
    Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the PLC real-time clock (day-of-week inferred from date).</summary>
    /// <param name="newDateTime">New date/time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock write result.</returns>
    Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, CancellationToken cancellationToken = default);

    /// <summary>Writes the PLC real-time clock with explicit day-of-week.</summary>
    /// <param name="newDateTime">New date/time.</param>
    /// <param name="newDayOfWeek">Day of week (0-6).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clock write result.</returns>
    Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, int newDayOfWeek, CancellationToken cancellationToken = default);

    /// <summary>Reads PLC scan cycle time statistics.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cycle time statistics.</returns>
    Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken = default);
}
