// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Results;

internal readonly record struct SendMessageResult
{
    internal int Bytes { get; init; }

    internal int Packets { get; init; }
}
