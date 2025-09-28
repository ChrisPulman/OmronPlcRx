// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using OmronPlcRx.Core.Requests;

namespace OmronPlcRx.Core.Responses;

internal static class ReadMemoryAreaBitResponse
{
    internal static bool[] ExtractValues(ReadMemoryAreaBitRequest request, FINSResponse response)
    {
        if (response.Data?.Length < request.Length)
        {
            throw new FINSException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '" + request.Length.ToString() + "'");
        }

        var result = new bool[request.Length];
        var data = response.Data;
        for (var i = 0; i < request.Length; i++)
        {
            result[i] = data![i] != 0;
        }

        return result;
    }
}
