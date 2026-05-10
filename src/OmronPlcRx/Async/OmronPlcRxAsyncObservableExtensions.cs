// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using OmronPlcRx.Tags;
using ReactiveUI.Extensions.Async;

namespace OmronPlcRx.Async;

/// <summary>
/// Bridges Omron PLC classic Rx streams into ReactiveUI.Extensions async observables.
/// </summary>
public static class OmronPlcRxAsyncObservableExtensions
{
    /// <summary>
    /// Observes a typed PLC tag as an async observable.
    /// </summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC reactive facade.</param>
    /// <param name="tagName">The registered tag name.</param>
    /// <returns>An async observable that can use ReactiveUI.Extensions.Async operators.</returns>
    public static IObservableAsync<T?> ObserveAsync<T>(this IOmronPlcRx plc, string? tagName)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        return plc.Observe<T>(tagName).ToObservableAsync();
    }

    /// <summary>
    /// Observes every changed PLC tag as an async observable.
    /// </summary>
    /// <param name="plc">The PLC reactive facade.</param>
    /// <returns>An async observable of all changed tags.</returns>
    public static IObservableAsync<IPlcTag?> ObserveAllAsync(this IOmronPlcRx plc)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        return plc.ObserveAll.ToObservableAsync();
    }

    /// <summary>
    /// Observes PLC operational errors as an async observable.
    /// </summary>
    /// <param name="plc">The PLC reactive facade.</param>
    /// <returns>An async observable of PLC errors.</returns>
    public static IObservableAsync<OmronPLCException?> ErrorsAsync(this IOmronPlcRx plc)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        return plc.Errors.ToObservableAsync();
    }

    /// <summary>
    /// Observes a typed PLC tag as an async enumerable.
    /// </summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC reactive facade.</param>
    /// <param name="tagName">The registered tag name.</param>
    /// <param name="cancellationToken">Cancellation token for the async enumeration.</param>
    /// <returns>An async enumerable of tag values.</returns>
    public static IAsyncEnumerable<T?> ObserveValuesAsync<T>(this IOmronPlcRx plc, string? tagName, CancellationToken cancellationToken = default) =>
        plc.ObserveAsync<T>(tagName)
            .TakeUntil(cancellationToken)
            .ToAsyncEnumerable(static () => System.Threading.Channels.Channel.CreateUnbounded<T?>());
}
#endif
