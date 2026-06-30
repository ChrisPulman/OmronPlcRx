// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using OmronPlcRx.Core.Responses;

namespace OmronPlcRx.Core.Results;

internal readonly record struct ProcessRequestResult
{
    internal int BytesSent { get; init; }

    internal int PacketsSent { get; init; }

    internal int BytesReceived { get; init; }

    internal int PacketsReceived { get; init; }

    internal double Duration { get; init; }

    internal FINSResponse Response { get; init; }
}
