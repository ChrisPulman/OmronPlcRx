// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using OmronPlcRx.Core.Requests;

namespace OmronPlcRx.Core.Responses;

/// <summary>Represents the w ri te cl oc kr es po ns e type.</summary>
internal static class WriteClockResponse
{
    /// <summary>Initializes a new instance of the <see cref="Validate"/> class.</summary>
    /// <param name="request">The r eq ue st value.</param>
    /// <param name="response">The r es po ns e value.</param>
    internal static void Validate(WriteClockRequest request, FINSResponse response)
    {
        _ = request ?? throw new ArgumentNullException(nameof(request));
        _ = response ?? throw new ArgumentNullException(nameof(response));
    }
}
