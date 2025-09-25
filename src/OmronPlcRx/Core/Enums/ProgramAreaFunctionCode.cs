// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum ProgramAreaFunctionCode : byte
{
    Read = 0x06,
    Write = 0x07,
    Clear = 0x08,
}
