// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using OmronPlcRx.Core.Responses;

namespace OmronPlcRx.Core.Results;

internal record struct ProcessRequestResult
{
    internal int BytesSent;
    internal int PacketsSent;
    internal int BytesReceived;
    internal int PacketsReceived;
    internal double Duration;
    internal FINSResponse Response;
}
