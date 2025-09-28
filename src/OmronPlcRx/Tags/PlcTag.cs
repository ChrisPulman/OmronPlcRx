// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace OmronPlcRx.Tags
{
    /// <summary>
    /// PlcTag.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <remarks>
    /// Initializes a new instance of the <see cref="PlcTag{T}"/> class.
    /// </remarks>
    /// <param name="tagName">The tag Name.</param>
    /// <param name="address">The address.</param>
    /// <exception cref="ArgumentNullException">
    /// name
    /// or
    /// address.
    /// </exception>
    public class PlcTag<T>(string tagName, string address) : IPlcTag
    {
        /// <summary>
        /// Gets the Tag Name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string TagName { get; } = tagName ?? throw new ArgumentNullException(nameof(tagName));

        /// <summary>
        /// Gets the address.
        /// </summary>
        /// <value>
        /// The address.
        /// </value>
        public string Address { get; } = address ?? throw new ArgumentNullException(nameof(address));

        /// <summary>
        /// Gets the value read.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        public T? Value { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this instance is bit address.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is bit address; otherwise, <c>false</c>.
        /// </value>
        public Type TagType => typeof(T);

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        object? IPlcTag.Value => Value;
    }
}
