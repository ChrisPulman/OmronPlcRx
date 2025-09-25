// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum MemoryAreaFunctionCode : byte
{
    Read = 0x01,
    Write = 0x02,
    Fill = 0x03,
    MultipleRead = 0x04,
    Transfer = 0x05,
}
