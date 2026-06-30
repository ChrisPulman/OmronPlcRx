// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using OmronPlcRx.Core.Requests;

namespace OmronPlcRx.Core.Responses;

/// <summary>Represents the r ea dm em or ya re ab it re sp on se type.</summary>
internal static class ReadMemoryAreaBitResponse
{
    /// <summary>Initializes a new instance of the <see cref="ExtractValues"/> class.</summary>
    /// <param name="request">The r eq ue st value.</param>
    /// <param name="response">The r es po ns e value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
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
