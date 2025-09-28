// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace OmronPlcRx.Tags
{
    /// <summary>
    /// PlcTag.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    public class PlcTag<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlcTag{T}"/> class.
        /// </summary>
        /// <param name="tagName">The tag Name.</param>
        /// <param name="address">The address.</param>
        /// <exception cref="ArgumentNullException">
        /// name
        /// or
        /// address.
        /// </exception>
        public PlcTag(string tagName, string address)
        {
            TagName = tagName ?? throw new ArgumentNullException(nameof(tagName));
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }

        /// <summary>
        /// Gets the Tag Name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string TagName { get; }

        /// <summary>
        /// Gets the address.
        /// </summary>
        /// <value>
        /// The address.
        /// </value>
        public string Address { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is bit address.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is bit address; otherwise, <c>false</c>.
        /// </value>
        public bool IsBitAddress => Address.Contains(".");

        /// <summary>
        /// Gets the value read.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        public T Value { get; internal set; }

        /// <summary>
        /// Gets or sets the value to write.
        /// </summary>
        /// <value>
        /// The write value.
        /// </value>
        public T WriteValue { get; set; }
    }
}
