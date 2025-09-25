// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum SerialGatewayFunctionCode : byte
{
    ConvertToCompoWayFCommand = 0x03,
    ConvertToModbusRTUCommand = 0x04,
    ConvertToModbusASCIICommand = 0x05,
}
