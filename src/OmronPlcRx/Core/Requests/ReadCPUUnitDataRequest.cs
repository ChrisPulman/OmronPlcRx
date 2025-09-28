// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using OmronPlcRx.Core.Enums;

namespace OmronPlcRx.Core.Requests;

internal sealed class ReadCPUUnitDataRequest : FINSRequest
{
    private ReadCPUUnitDataRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    internal static ReadCPUUnitDataRequest CreateNew(OmronPLCConnection plc) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.MachineConfiguration,
        SubFunctionCode = (byte)MachineConfigurationFunctionCode.ReadCPUUnitData,
    };

    protected override List<byte> BuildRequestData() =>
        [
            0
        ];
}
