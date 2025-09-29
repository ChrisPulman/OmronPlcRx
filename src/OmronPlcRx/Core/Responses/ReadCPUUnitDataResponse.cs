// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace OmronPlcRx.Core.Responses;

internal static class ReadCPUUnitDataResponse
{
    internal const int ControllerModelLength = 20;
    internal const int ControllerVersionLength = 20;
    internal const int SystemReservedLength = 40;
    internal const int AreaDataLength = 12;
    internal const int TotalResponseLength = ControllerModelLength + ControllerVersionLength + SystemReservedLength + AreaDataLength;

    internal static CPUUnitDataResult ExtractData(FINSResponse response)
    {
        if (response.Data?.Length < TotalResponseLength)
        {
            throw new FINSException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '" + TotalResponseLength.ToString() + "'");
        }

        var data = response.Data;

        var result = default(CPUUnitDataResult);

        result.ControllerModel = ExtractStringValue(SubArray(data, 0, ControllerModelLength));

        result.ControllerVersion = ExtractStringValue(SubArray(data, ControllerModelLength, ControllerVersionLength));

        return result;
    }

    private static string ExtractStringValue(byte[] bytes)
    {
        var stringBytes = new List<byte>(bytes.Length);

        foreach (var byteValue in bytes)
        {
            if (byteValue > 0)
            {
                stringBytes.Add(byteValue);
            }
            else
            {
                break;
            }
        }

        if (stringBytes.Count == 0)
        {
            return string.Empty;
        }

        return Encoding.ASCII.GetString([.. stringBytes]).Trim();
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

    internal record struct CPUUnitDataResult
    {
        internal string ControllerModel;
        internal string ControllerVersion;
    }
}
