// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Enums;

/// <summary>
/// Word-addressable PLC memory areas.
/// </summary>
public enum MemoryWordDataType : byte
{
    /// <summary>
    /// Data memory area (DM).
    /// </summary>
    DataMemory = 0x82,

    /// <summary>
    /// Common I/O area (CIO).
    /// </summary>
    CommonIO = 0xB0,

    /// <summary>
    /// Work area (W).
    /// </summary>
    Work = 0xB1,

    /// <summary>
    /// Holding area (H).
    /// </summary>
    Holding = 0xB2,

    /// <summary>
    /// Auxiliary area (A).
    /// </summary>
    Auxiliary = 0xB3,
}
