// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Results;

internal record struct SendMessageResult
{
    internal int Bytes;
    internal int Packets;
}
