// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using OmronPlcRx.Core.Requests;

namespace OmronPlcRx.Core.Responses;

internal static class ReadMemoryAreaWordResponse
{
    internal static short[] ExtractValues(ReadMemoryAreaWordRequest request, FINSResponse response)
    {
        if (response.Data?.Length < request.Length * 2)
        {
            throw new FINSException("The Response Data Length of '" + response.Data.Length.ToString() + "' was too short - Expecting a Length of '" + (request.Length * 2).ToString() + "'");
        }

        var values = new short[request.Length];
        var data = response.Data;

        for (int i = 0, w = 0; i < request.Length * 2; i += 2, w++)
        {
            // Data is big-endian per protocol, convert to host order (little-endian)
            values[w] = (short)((data![i] << 8) | data[i + 1]);
        }

        return values;
    }
}
