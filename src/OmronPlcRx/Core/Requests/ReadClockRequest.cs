// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using OmronPlcRx.Core.Enums;

namespace OmronPlcRx.Core.Requests;

internal sealed class ReadClockRequest : FINSRequest
{
    private ReadClockRequest(OmronPLC plc)
        : base(plc)
    {
    }

    internal static ReadClockRequest CreateNew(OmronPLC plc) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.TimeData,
        SubFunctionCode = (byte)TimeDataFunctionCode.ReadClock,
    };

    protected override List<byte> BuildRequestData() => new();
}
