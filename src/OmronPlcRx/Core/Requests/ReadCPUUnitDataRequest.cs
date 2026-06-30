// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using OmronPlcRx.Core.Enums;

namespace OmronPlcRx.Core.Requests;

/// <summary>Represents the r ea dc pu un it da ta re qu es t type.</summary>
internal sealed class ReadCPUUnitDataRequest : FINSRequest
{
    /// <summary>Initializes a new instance of the <see cref="ReadCPUUnitDataRequest"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    private ReadCPUUnitDataRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CreateNew"/> class.</summary>
    /// <param name="plc">The p lc value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static ReadCPUUnitDataRequest CreateNew(OmronPLCConnection plc) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.MachineConfiguration,
        SubFunctionCode = (byte)MachineConfigurationFunctionCode.ReadCPUUnitData,
    };

    protected override List<byte> BuildRequestData() => [0];
}
