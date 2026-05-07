// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Ports;
using OmronPlcRx;
using OmronPlcRx.Enums;
using Xunit;

namespace OmronPlcRx.Tests;

public sealed class SerialHostLinkFinsTests
{
    [Fact]
    public void ConnectionMethod_ExposesSerialTransport()
    {
        Assert.True(Enum.IsDefined(typeof(ConnectionMethod), "Serial"));
    }

    [Fact]
    public void SerialOptions_DefaultToCommonOmronHostLinkSettings()
    {
        var options = new OmronSerialOptions("COM1");

        Assert.Equal("COM1", options.PortName);
        Assert.Equal(9600, options.BaudRate);
        Assert.Equal(7, options.DataBits);
        Assert.Equal(Parity.Even, options.Parity);
        Assert.Equal(StopBits.Two, options.StopBits);
        Assert.Equal(Handshake.None, options.Handshake);
        Assert.Equal(OmronSerialProtocol.HostLinkFins, options.Protocol);
        Assert.Equal(0, options.HostLinkUnitNumber);
        Assert.Equal(0, options.ResponseWaitTime);
        Assert.Equal(OmronHostLinkFinsFrameMode.Direct, options.FrameMode);
    }

    [Fact]
    public void SerialOptions_DefaultFrameLengthMatchesCs1ToolbusConfiguration()
    {
        var options = new OmronSerialOptions("COM1");

        Assert.Equal(1004, options.MaximumFrameLength);
    }

    [Fact]
    public void SerialConnection_AllowsDirectCpuDestinationNodeZero()
    {
        using var plc = new OmronPlcRx(
            localNodeId: 11,
            remoteNodeId: 0,
            serialOptions: new OmronSerialOptions("COM1"),
            timeout: 2000,
            retries: 0,
            pollInterval: TimeSpan.FromSeconds(30));

        Assert.False(plc.IsDisposed);
    }

    [Fact]
    public void SerialOptions_CanSelectToolbusProtocol()
    {
        var options = new OmronSerialOptions("COM1")
        {
            Protocol = OmronSerialProtocol.Toolbus,
            BaudRate = 115200,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
        };

        Assert.Equal(OmronSerialProtocol.Toolbus, options.Protocol);
        Assert.Equal(1004, options.MaximumFrameLength);
    }

    [Fact]
    public void SerialOptions_CreateToolbusUsesCommonToolbusSettings()
    {
        var options = OmronSerialOptions.CreateToolbus("COM1");

        Assert.Equal("COM1", options.PortName);
        Assert.Equal(OmronSerialProtocol.Toolbus, options.Protocol);
        Assert.Equal(115200, options.BaudRate);
        Assert.Equal(8, options.DataBits);
        Assert.Equal(Parity.None, options.Parity);
        Assert.Equal(StopBits.One, options.StopBits);
        Assert.Equal(Handshake.None, options.Handshake);
        Assert.Equal(1004, options.MaximumFrameLength);
    }

    [Fact]
    public void SerialOptions_ValidateIgnoresHostLinkOnlyValuesForToolbus()
    {
        var options = OmronSerialOptions.CreateToolbus("COM1") with
        {
            HostLinkUnitNumber = 255,
            ResponseWaitTime = 255,
        };

        options.Validate();
    }

    [Fact]
    public void ToolbusFinsFrameCodec_ExposesSynchronizationFrame()
    {
        Assert.Equal("AC01", Convert.ToHexString(ToolbusFinsFrameCodec.SynchronizationFrame.ToArray()));
    }

    [Fact]
    public void ToolbusFinsFrameCodec_RejectsTooShortFinsRequest()
    {
        var fins = new byte[11];

        Assert.Throws<ArgumentException>(() => ToolbusFinsFrameCodec.EncodeRequest(fins));
    }

    [Fact]
    public void ToolbusFinsFrameCodec_EncodesBinaryFinsRequestWithLengthAndChecksum()
    {
        var fins = Convert.FromHexString("800002000000000000050101");

        var frame = ToolbusFinsFrameCodec.EncodeRequest(fins);

        Assert.Equal("AB000E8000020000000000000501010142", Convert.ToHexString(frame.ToArray()));
    }

    [Fact]
    public void ToolbusFinsFrameCodec_EncodesMaximumLengthRequestAcceptedByLengthField()
    {
        var fins = new byte[ushort.MaxValue - 2];

        var frame = ToolbusFinsFrameCodec.EncodeRequest(fins);

        Assert.Equal(ushort.MaxValue + 3, frame.Length);
        Assert.Equal("ABFFFF", Convert.ToHexString(frame.Span[..3]));
        Assert.Equal("02A9", Convert.ToHexString(frame.Span[^2..]));
    }

    [Fact]
    public void ToolbusFinsFrameCodec_RejectsRequestLongerThanLengthFieldAllows()
    {
        var fins = new byte[ushort.MaxValue - 1];

        Assert.Throws<ArgumentOutOfRangeException>(() => ToolbusFinsFrameCodec.EncodeRequest(fins));
    }

    [Fact]
    public void ToolbusFinsFrameCodec_DecodesToolbusFrameToBinaryFinsResponse()
    {
        var payload = Convert.FromHexString("C0000200000000000005010100001234");
        var frame = ToolbusFinsFrameCodec.EncodeRequest(payload);

        var decoded = ToolbusFinsFrameCodec.DecodeResponse(frame);

        Assert.Equal(payload, decoded.ToArray());
    }

    [Fact]
    public void ToolbusFinsFrameCodec_RejectsInvalidStartByte()
    {
        var frame = Convert.FromHexString("AA000E8000020000000000000501010142");

        var ex = Assert.Throws<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        Assert.Contains("0xAB", ex.Message);
    }

    [Fact]
    public void ToolbusFinsFrameCodec_RejectsDeclaredLengthMismatch()
    {
        var frame = Convert.FromHexString("AB000D8000020000000000000501010142");

        var ex = Assert.Throws<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        Assert.Contains("declared length", ex.Message);
    }

    [Fact]
    public void ToolbusFinsFrameCodec_RejectsTooSmallDeclaredLength()
    {
        var frame = Convert.FromHexString("AB00010142");

        var ex = Assert.Throws<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        Assert.Contains("length", ex.Message);
    }

    [Fact]
    public void ToolbusFinsFrameCodec_RejectsTooShortFinsResponsePayload()
    {
        var frame = ToolbusFinsFrameCodec.EncodeRequest(Convert.FromHexString("C00002000000000000050101"));

        var ex = Assert.Throws<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        Assert.Contains("payload", ex.Message);
    }

    [Fact]
    public void ToolbusFinsFrameCodec_RejectsInvalidFinsResponseHeader()
    {
        var frame = ToolbusFinsFrameCodec.EncodeRequest(Convert.FromHexString("8000020000000000000501010000"));

        var ex = Assert.Throws<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        Assert.Contains("FINS header", ex.Message);
    }

    [Fact]
    public void ToolbusFinsFrameCodec_RejectsInvalidChecksum()
    {
        var frame = Convert.FromHexString("AB000E8000020000000000000501010143");

        var ex = Assert.Throws<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        Assert.Contains("checksum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolbusFinsFrameCodec_CalculatesChecksumWithSixteenBitWraparound()
    {
        var data = new byte[258];
        Array.Fill(data, (byte)0xFF);

        var checksum = ToolbusFinsFrameCodec.CalculateChecksum(data);

        Assert.Equal((ushort)0x00FE, checksum);
    }

    [Fact]
    public void HostLinkFcs_CalculatesXorAcrossFrameText()
    {
        var fcs = HostLinkFinsFrameCodec.CalculateFcs("@00FA0000000010101820064000002");

        Assert.Equal("7C", fcs);
    }

    [Fact]
    public void HostLinkFinsFrameCodec_EncodesBinaryFinsRequestAsAsciiDirectHostLinkFrame()
    {
        var options = new OmronSerialOptions("COM1")
        {
            HostLinkUnitNumber = 0,
            ResponseWaitTime = 0,
        };
        var codec = new HostLinkFinsFrameCodec(options);
        var fins = Convert.FromHexString("800002000100000B00010101820064000002");

        var frame = codec.EncodeRequest(fins);

        Assert.Equal("@00FA00000000101018200640000027C*\r", frame);
    }

    [Fact]
    public void HostLinkFinsFrameCodec_DecodesAsciiDirectHostLinkResponseToBinaryFinsResponse()
    {
        var options = new OmronSerialOptions("COM1")
        {
            HostLinkUnitNumber = 0,
            ResponseWaitTime = 0,
        };
        var codec = new HostLinkFinsFrameCodec(options);
        var payload = "40000001010100001234";
        var body = "@00FA00" + payload;
        var frame = body + HostLinkFinsFrameCodec.CalculateFcs(body) + "*\r";

        var decoded = codec.DecodeResponse(frame);

        Assert.Equal("40000200000000000001010100001234", Convert.ToHexString(decoded.ToArray()));
    }

    [Fact]
    public void HostLinkFinsFrameCodec_RejectsHostLinkEndCodeFailures()
    {
        var codec = new HostLinkFinsFrameCodec(new OmronSerialOptions("COM1"));

        var ex = Assert.Throws<OmronPLCException>(() => codec.DecodeResponse("@00FA014000000101010000123447*\r"));

        Assert.Contains("end code", ex.Message);
    }

    [Fact]
    public void HostLinkFinsFrameCodec_RejectsInvalidFcs()
    {
        var codec = new HostLinkFinsFrameCodec(new OmronSerialOptions("COM1"));

        var ex = Assert.Throws<OmronPLCException>(() => codec.DecodeResponse("@00FA004000000101010000123400*\r"));

        Assert.Contains("FCS", ex.Message);
    }
}
