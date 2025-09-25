// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx.Core.Enums;

internal enum ErrorLogFunctionCode : byte
{
    ClearMessages = 0x01,
    Read = 0x02,
    ClearLog = 0x03,
}
