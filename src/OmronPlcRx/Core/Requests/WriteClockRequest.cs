// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using OmronPlcRx.Core.Converters;
using OmronPlcRx.Core.Enums;

namespace OmronPlcRx.Core.Requests;

internal sealed class WriteClockRequest : FINSRequest
{
    private WriteClockRequest(OmronPLCConnection plc)
        : base(plc)
    {
    }

    internal DateTime DateTime { get; set; }

    internal byte DayOfWeek { get; set; }

    internal static WriteClockRequest CreateNew(OmronPLCConnection plc, DateTime dateTime, byte dayOfWeek) => new(plc)
    {
        FunctionCode = (byte)Enums.FunctionCode.TimeData,
        SubFunctionCode = (byte)TimeDataFunctionCode.WriteClock,
        DateTime = dateTime,
        DayOfWeek = dayOfWeek,
    };

    protected override List<byte> BuildRequestData() => new List<byte>
        {
            // Year (Last 2 Digits)
            BCDConverter.GetBCDByte((byte)(DateTime.Year % 100)),

            // Month
            BCDConverter.GetBCDByte((byte)DateTime.Month),

            // Day
            BCDConverter.GetBCDByte((byte)DateTime.Day),

            // Hour
            BCDConverter.GetBCDByte((byte)DateTime.Hour),

            // Minute
            BCDConverter.GetBCDByte((byte)DateTime.Minute),

            // Second
            BCDConverter.GetBCDByte((byte)DateTime.Second),

            // Day of Week
            BCDConverter.GetBCDByte(DayOfWeek)
        };
}
