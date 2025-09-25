// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum AccessRightsFunctionCode : byte
{
    Acquire = 0x01,
    ForcedAcquire = 0x02,
    Release = 0x03,
}
