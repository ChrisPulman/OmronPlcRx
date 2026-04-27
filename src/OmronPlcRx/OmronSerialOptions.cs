// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO.Ports;

namespace OmronPlcRx;

/// <summary>
/// Serial Host Link FINS connection settings.
/// </summary>
public sealed record OmronSerialOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmronSerialOptions"/> class.
    /// </summary>
    /// <param name="portName">Serial port name, e.g. COM1 or /dev/ttyUSB0.</param>
    public OmronSerialOptions(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("The serial port name cannot be empty.", nameof(portName));
        }

        PortName = portName;
    }

    /// <summary>
    /// Gets the serial port name.
    /// </summary>
    public string PortName { get; init; }

    /// <summary>
    /// Gets the serial baud rate.
    /// </summary>
    public int BaudRate { get; init; } = 9600;

    /// <summary>
    /// Gets the serial data bit count.
    /// </summary>
    public int DataBits { get; init; } = 7;

    /// <summary>
    /// Gets the serial parity setting.
    /// </summary>
    public Parity Parity { get; init; } = Parity.Even;

    /// <summary>
    /// Gets the serial stop bits setting.
    /// </summary>
    public StopBits StopBits { get; init; } = StopBits.Two;

    /// <summary>
    /// Gets the serial handshake setting.
    /// </summary>
    public Handshake Handshake { get; init; } = Handshake.None;

    /// <summary>
    /// Gets the Host Link unit number, 0 to 31.
    /// </summary>
    public byte HostLinkUnitNumber { get; init; }

    /// <summary>
    /// Gets the response wait time nibble, 0 to 15, in units of 10 ms.
    /// </summary>
    public byte ResponseWaitTime { get; init; }

    /// <summary>
    /// Gets the Host Link FINS frame mode.
    /// </summary>
    public OmronHostLinkFinsFrameMode FrameMode { get; init; } = OmronHostLinkFinsFrameMode.Direct;

    /// <summary>
    /// Validates this options instance.
    /// </summary>
    public void Validate()
    {
        if (HostLinkUnitNumber > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(HostLinkUnitNumber), "The Host Link unit number must be between 0 and 31.");
        }

        if (ResponseWaitTime > 0x0F)
        {
            throw new ArgumentOutOfRangeException(nameof(ResponseWaitTime), "The response wait time must be between 0 and 15.");
        }

        if (BaudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaudRate), "The baud rate must be greater than zero.");
        }

        if (DataBits < 5 || DataBits > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(DataBits), "The data bit count must be between 5 and 8.");
        }
    }
}
