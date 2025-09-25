// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum DebuggingFunctionCode : byte
{
    ForceBits = 0x01,
    ClearForcedBits = 0x02,
}
