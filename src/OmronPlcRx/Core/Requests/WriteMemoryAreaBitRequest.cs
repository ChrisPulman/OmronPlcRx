// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using OmronPlcRx.Core.Enums;
using OmronPlcRx.Enums;

namespace OmronPlcRx.Core.Requests;

internal sealed class WriteMemoryAreaBitRequest : FINSRequest
{
    private WriteMemoryAreaBitRequest(OmronPLC plc)
        : base(plc)
    {
    }

    internal ushort Address { get; set; }

    internal byte StartBitIndex { get; set; }

    internal MemoryBitDataType DataType { get; set; }

    internal bool[]? Values { get; set; }

    internal static WriteMemoryAreaBitRequest CreateNew(OmronPLC plc, ushort address, byte startBitIndex, MemoryBitDataType dataType, bool[] values) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.MemoryArea,
        SubFunctionCode = (byte)MemoryAreaFunctionCode.Write,
        Address = address,
        StartBitIndex = startBitIndex,
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
        var addr = BitConverter.GetBytes(Address);
        Array.Reverse(addr);
        data.AddRange(addr);

        // Bit Index
        data.Add(StartBitIndex);

        // Length (big-endian)
        var len = BitConverter.GetBytes((ushort)Values!.Length);
        Array.Reverse(len);
        data.AddRange(len);

        // Bit Values
        for (var i = 0; i < Values.Length; i++)
        {
            data.Add(Values[i] ? (byte)1 : (byte)0);
        }

        return data;
    }
}
