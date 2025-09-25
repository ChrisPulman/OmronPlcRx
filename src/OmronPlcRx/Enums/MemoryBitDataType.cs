// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Enums;

/// <summary>
/// Bit-addressable PLC memory areas.
/// </summary>
public enum MemoryBitDataType : byte
{
    /// <summary>
    /// Data memory area (DM).
    /// </summary>
    DataMemory = 0x2,

    /// <summary>
    /// Common I/O area (CIO).
    /// </summary>
    CommonIO = 0x30,

    /// <summary>
    /// Work area (W).
    /// </summary>
    Work = 0x31,

    /// <summary>
    /// Holding area (H).
    /// </summary>
    Holding = 0x32,

    /// <summary>
    /// Auxiliary area (A).
    /// </summary>
    Auxiliary = 0x33,
}
