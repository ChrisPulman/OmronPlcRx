// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reactive.Disposables;

namespace OmronPlcRx
{
    /// <summary>
    /// IOmronPlcRx.
    /// </summary>
    public interface IOmronPlcRx : ICancelable
    {
        /// <summary>
        /// Gets the observe all.
        /// </summary>
        /// <value>
        /// The observe all.
        /// </value>
        IObservable<string?> ObserveAll { get; }

        /// <summary>
        /// Gets the errors.
        /// </summary>
        /// <value>
        /// The errors.
        /// </value>
        IObservable<OmronPLCException?> Errors { get; }

        /// <summary>
        /// Adds the update tag item.
        /// </summary>
        /// <typeparam name="T">The type to observe.</typeparam>
        /// <param name="variable">The PLC variable.</param>
        /// <param name="tagName">Name of the tag.</param>
        void AddUpdateTagItem<T>(string variable, string tagName);

        /// <summary>
        /// Observes the specified variable.
        /// </summary>
        /// <typeparam name="T">The PLC type.</typeparam>
        /// <param name="tagName">The Tag Name.</param>
        /// <returns>
        /// An observable sequence of values of type T.
        /// </returns>
        IObservable<T?> Observe<T>(string? tagName);

        /// <summary>
        /// Reads the specified variable.
        /// </summary>
        /// <typeparam name="T">The PLC type.</typeparam>
        /// <param name="tagName">The Tag Name.</param>
        /// <returns>
        /// A value of T.
        /// </returns>
        T? Value<T>(string? tagName);

        /// <summary>
        /// Writes the specified variable value.
        /// </summary>
        /// <typeparam name="T">The PLC type.</typeparam>
        /// <param name="tagName">The Tag Name.</param>
        /// <param name="value">The value.</param>
        void Value<T>(string? tagName, T? value);
    }
}
