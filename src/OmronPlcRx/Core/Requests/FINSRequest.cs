// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using OmronPlcRx.Core.Channels;

namespace OmronPlcRx.Core.Requests;

internal abstract class FINSRequest
{
    internal const int HeaderLength = 10;
    internal const int CommandLength = 2;

    protected FINSRequest(OmronPLCConnection plc)
    {
        if (plc.Channel is TCPChannel)
        {
            LocalNodeID = (plc.Channel as TCPChannel).LocalNodeID;
            RemoteNodeID = (plc.Channel as TCPChannel).RemoteNodeID;
        }
        else
        {
            LocalNodeID = plc.LocalNodeID;
            RemoteNodeID = plc.RemoteNodeID;
        }
    }

    internal byte LocalNodeID { get; }

    internal byte RemoteNodeID { get; }

    internal byte ServiceID { get; set; }

    internal byte FunctionCode { get; set; }

    internal byte SubFunctionCode { get; set; }

    internal ReadOnlyMemory<byte> BuildMessage(byte requestId)
    {
        ServiceID = requestId;

        var message = new List<byte>
        {
            // Information Control Field
            0x80,

            // Reserved by System
            0,

            // Permissible Number of Gateways
            0x02,

            // Destination Network Address
            0, // Local Network

            // Destination Node Address
            // 0 = Local PLC Unit
            // 1 to 254 = Destination Node Address
            // 255 = Broadcasting
            RemoteNodeID,

            // Destination Unit Address
            0, // PLC (CPU Unit)

            // Source Network Address
            0, // Local Network

            // Source Node Address
            LocalNodeID, // Local Server

            // Source Unit Address
            0,

            // Service ID
            ServiceID,

            // Main Function Code
            FunctionCode,

            // Sub Function Code
            SubFunctionCode
        };

        // Request Data
        message.AddRange(BuildRequestData());

        return new ReadOnlyMemory<byte>([.. message]);
    }

    protected abstract List<byte> BuildRequestData();
}
