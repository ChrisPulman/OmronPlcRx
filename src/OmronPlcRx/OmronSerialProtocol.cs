// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx;

/// <summary>
/// Specifies the serial protocol used to carry FINS messages.
/// </summary>
public enum OmronSerialProtocol
{
    /// <summary>
    /// Host Link FINS using ASCII FA frames.
    /// </summary>
    HostLinkFins,

    /// <summary>
    /// Omron Toolbus using binary 0xAB frames carrying binary FINS messages.
    /// </summary>
    Toolbus,
}
