// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using OmronPlcRx.Core.Enums;
using OmronPlcRx.Enums;

namespace OmronPlcRx.Core.Requests;

internal sealed class ReadMemoryAreaBitRequest : FINSRequest
{
    private ReadMemoryAreaBitRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    internal ushort Address { get; set; }

    internal byte StartBitIndex { get; set; }

    internal ushort Length { get; set; }

    internal MemoryBitDataType DataType { get; set; }

    internal static ReadMemoryAreaBitRequest CreateNew(OmronPLCConnection plc, ushort address, byte startBitIndex, ushort length, MemoryBitDataType dataType) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.MemoryArea,
        SubFunctionCode = (byte)MemoryAreaFunctionCode.Read,
        Address = address,
        StartBitIndex = startBitIndex,
        Length = length,
        DataType = dataType,
    };

    protected override List<byte> BuildRequestData()
    {
        var data = new List<byte>(1 + 2 + 1 + 2)
        {
            // Memory Area Data Type
            (byte)DataType
        };

        // Address (big-endian)
        var addr = BitConverter.GetBytes(Address);
        Array.Reverse(addr);
        data.AddRange(addr);

        // Bit Index
        data.Add(StartBitIndex);

        // Length (big-endian)
        var len = BitConverter.GetBytes(Length);
        Array.Reverse(len);
        data.AddRange(len);

        return data;
    }
}
