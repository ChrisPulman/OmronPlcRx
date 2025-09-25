// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum FileMemoryFunctionCode : byte
{
    ReadFileName = 0x01,
    ReadSingleFile = 0x02,
    WriteSingleFile = 0x03,
    FormatMemory = 0x04,
    DeleteFile = 0x05,
    CopyFile = 0x07,
    ChangeFileName = 0x08,
    MemoryAreaTransfer = 0x0A,
    ParameterAreaTransfer = 0x0B,
    ProgramAreaTransfer = 0x0C,
    CreateOrDeleteDirectory = 0x15,
}
