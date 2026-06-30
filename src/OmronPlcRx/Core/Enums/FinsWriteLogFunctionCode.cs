// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Enums;
#else
namespace OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the f in sw ri te lo gf un ct io nc od e enumeration.</summary>
internal enum FinsWriteLogFunctionCode : byte
{
    /// <summary>Represents the r ea d enum value.</summary>
    Read = 0x40,
    /// <summary>Represents the c le ar enum value.</summary>
    Clear = 0x41,
}
