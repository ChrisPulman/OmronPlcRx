// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using OmronPlcRx.Core.Converters;
using OmronPlcRx.Core.Requests;

namespace OmronPlcRx.Core.Responses;

internal static class ReadCycleTimeResponse
{
    internal const int CycleTimeItemLength = 4;

    internal static CycleTimeResult ExtractCycleTime(ReadCycleTimeRequest request, FINSResponse response)
    {
        if (response.Data?.Length < CycleTimeItemLength * 3)
        {
            throw new FINSException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '" + (CycleTimeItemLength * 3).ToString() + "'");
        }

        var data = response.Data;

        return new CycleTimeResult
        {
            AverageCycleTime = GetCycleTime(SubArray(data, 0, CycleTimeItemLength)),
            MaximumCycleTime = GetCycleTime(SubArray(data, CycleTimeItemLength, CycleTimeItemLength)),
            MinimumCycleTime = GetCycleTime(SubArray(data, CycleTimeItemLength * 2, CycleTimeItemLength)),
        };
    }

    private static double GetCycleTime(byte[] bytes)
    {
        if (bytes.Length != 4)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), "The Cycle Time Bytes Array Length must be 4");
        }

        Array.Reverse(bytes);
        var cycleTimeValue = BCDConverter.ToUInt32(bytes);

        if (cycleTimeValue > 0)
        {
            return cycleTimeValue / 10d;
        }

        return 0;
    }

    private static byte[] SubArray(byte[]? data, int index, int length)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "The Data Array cannot be null");
        }

        var result = new byte[length];
        Array.Copy(data, index, result, 0, length);
        return result;
    }

    internal record struct CycleTimeResult
    {
        internal double MinimumCycleTime;
        internal double MaximumCycleTime;
        internal double AverageCycleTime;
    }
}
