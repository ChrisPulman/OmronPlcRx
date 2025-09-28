// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Results;

/// <summary>
/// Result of a Read Cycle Time operation.
/// </summary>
public readonly record struct ReadCycleTimeResult
{
    /// <summary>Gets the total bytes sent.</summary>
    public int BytesSent { get; init; }

    /// <summary>Gets the total packets sent.</summary>
    public int PacketsSent { get; init; }

    /// <summary>Gets the total bytes received.</summary>
    public int BytesReceived { get; init; }

    /// <summary>Gets the total packets received.</summary>
    public int PacketsReceived { get; init; }

    /// <summary>Gets the duration in milliseconds.</summary>
    public double Duration { get; init; }

    /// <summary>Gets the minimum cycle time in milliseconds.</summary>
    public double MinimumCycleTime { get; init; }

    /// <summary>Gets the maximum cycle time in milliseconds.</summary>
    public double MaximumCycleTime { get; init; }

    /// <summary>Gets the average cycle time in milliseconds.</summary>
    public double AverageCycleTime { get; init; }
}
