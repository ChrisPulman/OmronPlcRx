// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using OmronPlcRx.Core.Enums;

namespace OmronPlcRx.Core.Requests;

internal sealed class ReadCycleTimeRequest : FINSRequest
{
    private ReadCycleTimeRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    internal static ReadCycleTimeRequest CreateNew(OmronPLCConnection plc) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.Status,
        SubFunctionCode = (byte)StatusFunctionCode.ReadCycleTime,
    };

    protected override List<byte> BuildRequestData() => [01]; // Read Cycle Time
}
