// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace OmronPlcRx.Tags
{
    /// <summary>
    /// IPlcTag.
    /// </summary>
    public interface IPlcTag
    {
        /// <summary>
        /// Gets the address.
        /// </summary>
        /// <value>
        /// The address.
        /// </value>
        string Address { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is bit address.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is bit address; otherwise, <c>false</c>.
        /// </value>
        Type TagType { get; }

        /// <summary>
        /// Gets the name of the tag.
        /// </summary>
        /// <value>
        /// The name of the tag.
        /// </value>
        string TagName { get; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        object? Value { get; }
    }
}
