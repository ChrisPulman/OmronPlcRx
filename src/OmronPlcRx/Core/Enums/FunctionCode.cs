// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum FunctionCode : byte
{
    MemoryArea = 0x01,
    ParameterArea = 0x02,
    ProgramArea = 0x03,
    OperatingMode = 0x04,
    MachineConfiguration = 0x05,
    Status = 0x06,
    TimeData = 0x07,
    MessageDisplay = 0x09,
    AccessRights = 0x0C,
    ErrorLog = 0x21,
    FINSWriteLog = 0x21,
    FileMemory = 0x22,
    Debugging = 0x23,
    SerialGateway = 0x28,
}
