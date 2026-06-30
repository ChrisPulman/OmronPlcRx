// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using OmronPlcRx.Core.Types;

namespace OmronPlcRx.Tests;

/// <summary>Generated PLC tag fixture used by source-generator tests.</summary>
public sealed partial class GeneratedMachineState
{
    [PlcTag("D100", Writable = true)]
    private short _tankLevel;

    [PlcTag("D100.0", TagName = "PumpRun")]
    private bool _pumpRunning;

    [PlcTag("D600[20]")]
    private string? _lineName;

    [PlcTag("D700")]
    private Bcd16 _bcdTemp;
}
