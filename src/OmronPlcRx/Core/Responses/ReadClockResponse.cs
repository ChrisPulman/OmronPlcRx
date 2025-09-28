// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using OmronPlcRx.Core.Converters;
using OmronPlcRx.Core.Requests;

namespace OmronPlcRx.Core.Responses;

internal static class ReadClockResponse
{
    internal const int DateLength = 6;
    internal const int DayOfWeekLength = 1;

    internal static ClockResult ExtractClock(ReadClockRequest request, FINSResponse response)
    {
        if (response.Data?.Length < DateLength + DayOfWeekLength)
        {
            throw new FINSException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '" + (DateLength + DayOfWeekLength).ToString() + "'");
        }

        var data = response.Data;

        return new ClockResult
        {
            ClockDateTime = GetClockDateTime(SubArray(data, 0, DateLength)),
            DayOfWeek = BCDConverter.ToByte(data![DateLength]),
        };
    }

    private static DateTime GetClockDateTime(byte[] bytes)
    {
        var year = BCDConverter.ToByte(bytes[0]);
        var month = BCDConverter.ToByte(bytes[1]);
        var day = BCDConverter.ToByte(bytes[2]);
        var hour = BCDConverter.ToByte(bytes[3]);
        var minute = BCDConverter.ToByte(bytes[4]);
        var second = BCDConverter.ToByte(bytes[5]);

        if (year < 70)
        {
            return new DateTime(2000 + year, month, day, hour, minute, second);
        }
        else if (year < 100)
        {
            return new DateTime(1900 + year, month, day, hour, minute, second);
        }

        throw new FINSException("Invalid DateTime Values received from the PLC Clock");
    }

    private static byte[] SubArray(byte[]? data, int index, int length)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var result = new byte[length];
        Array.Copy(data, index, result, 0, length);
        return result;
    }

    internal struct ClockResult
    {
        internal DateTime ClockDateTime;
        internal byte DayOfWeek;
    }
}
