// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace OmronPlcRx.Enums;

/// <summary>Transport protocol used for communication with the PLC.</summary>
public enum ConnectionMethod
{
    /// <summary>Transmission Control Protocol.</summary>
    TCP,

    /// <summary>User Datagram Protocol.</summary>
    UDP,

    /// <summary>Serial FINS protocol using Host Link FINS or Toolbus framing.</summary>
    Serial,
}
