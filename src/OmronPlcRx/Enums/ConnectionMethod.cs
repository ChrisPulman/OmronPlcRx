// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Enums;

/// <summary>
/// Transport protocol used for communication with the PLC.
/// </summary>
public enum ConnectionMethod
{
    /// <summary>
    /// Transmission Control Protocol.
    /// </summary>
    TCP,

    /// <summary>
    /// User Datagram Protocol.
    /// </summary>
    UDP,
}
