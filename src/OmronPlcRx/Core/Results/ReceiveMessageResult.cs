// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace OmronPlcRx.Core.Results;

internal record struct ReceiveMessageResult
{
    internal Memory<byte> Message;
    internal int Bytes;
    internal int Packets;
}
