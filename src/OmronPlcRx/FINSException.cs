// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive;
#else
namespace OmronPlcRx;
#endif

/// <summary>An exception that represents a FINS protocol error or invalid response.</summary>
public class FINSException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FINSException"/> class.</summary>
    public FINSException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FINSException" /> class with a message.</summary>
    /// <param name="message">The message that describes the error.</param>
    public FINSException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FINSException" /> class with a message and inner exception.</summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
    public FINSException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
