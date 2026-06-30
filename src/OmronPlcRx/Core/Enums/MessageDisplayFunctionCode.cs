// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Enums;
#else
namespace OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the m es sa ge di sp la yf un ct io nc od e enumeration.</summary>
internal enum MessageDisplayFunctionCode : byte
{
    /// <summary>Represents the r ea d enum value.</summary>
    Read = 0x20,
}
