// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using OmronPlcRx.Core.Enums;
using OmronPlcRx.Enums;

namespace OmronPlcRx.Core.Requests;

internal sealed class ReadMemoryAreaWordRequest : FINSRequest
{
    private ReadMemoryAreaWordRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    internal ushort StartAddress { get; set; }

    internal ushort Length { get; set; }

    internal MemoryWordDataType DataType { get; set; }

    internal static ReadMemoryAreaWordRequest CreateNew(OmronPLCConnection plc, ushort startAddress, ushort length, MemoryWordDataType dataType) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.MemoryArea,
        SubFunctionCode = (byte)MemoryAreaFunctionCode.Read,
        StartAddress = startAddress,
        Length = length,
        DataType = dataType,
    };

    protected override List<byte> BuildRequestData()
    {
        var data = new List<byte>
        {
            // Memory Area Data Type
            (byte)DataType
        };

        // Address (big-endian)
        var addr = BitConverter.GetBytes(StartAddress);
        Array.Reverse(addr);
        data.AddRange(addr);

        // Reserved
        data.Add(0);

        // Length (big-endian)
        var len = BitConverter.GetBytes(Length);
        Array.Reverse(len);
        data.AddRange(len);

        return data;
    }
}
