// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum StatusFunctionCode : byte
{
    ReadCPUUnitStatus = 0x01,
    ReadCycleTime = 0x20,
}
