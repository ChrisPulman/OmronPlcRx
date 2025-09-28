// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace OmronPlcRx;

/// <summary>
/// Represents errors that occur during Omron PLC communication or processing.
/// </summary>
public class OmronPLCException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmronPLCException"/> class.
    /// </summary>
    public OmronPLCException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmronPLCException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public OmronPLCException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmronPLCException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public OmronPLCException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
