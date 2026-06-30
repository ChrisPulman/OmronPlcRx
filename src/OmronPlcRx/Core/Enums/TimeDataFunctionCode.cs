// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

/// <summary>Represents the t im ed at af un ct io nc od e enumeration.</summary>
internal enum TimeDataFunctionCode : byte
{
    /// <summary>Represents the r ea dc lo ck enum value.</summary>
    ReadClock = 0x01,
    /// <summary>Represents the w ri te cl oc k enum value.</summary>
    WriteClock = 0x02,
}
