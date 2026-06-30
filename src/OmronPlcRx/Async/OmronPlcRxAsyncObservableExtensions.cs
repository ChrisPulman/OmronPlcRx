// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using CP.IO.Ports;
using OmronPlcRx.Tags;
using ReactiveUI.Primitives.Async;

namespace OmronPlcRx.Async;

/// <summary>Bridges Omron PLC classic Rx streams into ReactiveUI.Primitives.Async observables.</summary>
public static class OmronPlcRxAsyncObservableExtensions
{
    /// <summary>Provides async observable members for an Omron PLC facade.</summary>
    /// <param name="plc">The PLC reactive facade.</param>
    extension(IOmronPlcRx plc)
    {
        /// <summary>Observes a typed PLC tag as an async observable.</summary>
        /// <typeparam name="T">The tag value type.</typeparam>
        /// <param name="tagName">The registered tag name.</param>
        /// <returns>An async observable that can use ReactiveUI.Primitives.Async operators.</returns>
        public IObservableAsync<T?> ObserveAsAsyncObservable<T>(string? tagName)
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            return plc.Observe<T>(tagName).ToObservableAsync();
        }

        /// <summary>Observes every changed PLC tag as an async observable.</summary>
        /// <returns>An async observable of all changed tags.</returns>
        public IObservableAsync<IPlcTag?> ObserveAllAsAsyncObservable()
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            return plc.ObserveAll.ToObservableAsync();
        }

        /// <summary>Observes PLC operational errors as an async observable.</summary>
        /// <returns>An async observable of PLC errors.</returns>
        public IObservableAsync<OmronPLCException?> ErrorsAsAsyncObservable()
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            return plc.Errors.ToObservableAsync();
        }

        /// <summary>Observes a typed PLC tag as an async enumerable.</summary>
        /// <typeparam name="T">The tag value type.</typeparam>
        /// <param name="tagName">The registered tag name.</param>
        /// <param name="cancellationToken">Cancellation token for the async enumeration.</param>
        /// <returns>An async enumerable of tag values.</returns>
        public IAsyncEnumerable<T?> ObserveValuesAsync<T>(string? tagName, CancellationToken cancellationToken = default) =>
            plc.ObserveAsAsyncObservable<T>(tagName)
                .TakeUntil(cancellationToken)
                .ToAsyncEnumerable(static () => System.Threading.Channels.Channel.CreateUnbounded<T?>());
    }
}
