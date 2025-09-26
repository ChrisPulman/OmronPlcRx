// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace OmronPlcRx;

/// <summary>
/// An exception that represents a FINS protocol error or invalid response.
/// </summary>
public class FINSException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FINSException"/> class with a message.
    /// </summary>
    internal FINSException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FINSException"/> class with a message and inner exception.
    /// </summary>
    internal FINSException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
