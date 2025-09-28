// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using OmronPlcRx.Core.Enums;
using OmronPlcRx.Enums;

namespace OmronPlcRx.Core.Requests;

internal sealed class WriteMemoryAreaWordRequest : FINSRequest
{
    private WriteMemoryAreaWordRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    internal ushort StartAddress { get; set; }

    internal MemoryWordDataType DataType { get; set; }

    internal short[]? Values { get; set; }

    internal static WriteMemoryAreaWordRequest CreateNew(OmronPLCConnection plc, ushort startAddress, MemoryWordDataType dataType, short[] values) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.MemoryArea,
        SubFunctionCode = (byte)MemoryAreaFunctionCode.Write,
        StartAddress = startAddress,
        DataType = dataType,
        Values = values,
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
        var len = BitConverter.GetBytes((ushort)Values!.Length);
        Array.Reverse(len);
        data.AddRange(len);

        // Word Values (big-endian)
        foreach (var value in Values)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            data.AddRange(bytes);
        }

        return data;
    }
}
