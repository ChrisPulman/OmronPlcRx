// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using OmronPlcRx.Core.Responses;

namespace OmronPlcRx.Core.Results;

/// <summary>Represents the p ro ce ss re qu es tr es ul t type.</summary>
internal readonly record struct ProcessRequestResult
{
    /// <summary>Gets or sets the bytes sent value.</summary>
    internal int BytesSent { get; init; }

    /// <summary>Gets or sets the packets sent value.</summary>
    internal int PacketsSent { get; init; }

    /// <summary>Gets or sets the bytes received value.</summary>
    internal int BytesReceived { get; init; }

    /// <summary>Gets or sets the packets received value.</summary>
    internal int PacketsReceived { get; init; }

    /// <summary>Gets or sets the duration value.</summary>
    internal double Duration { get; init; }

    /// <summary>Gets or sets the response value.</summary>
    internal FINSResponse Response { get; init; }
}
