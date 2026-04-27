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
        Assert.Equal(0, options.HostLinkUnitNumber);
        Assert.Equal(0, options.ResponseWaitTime);
        Assert.Equal(OmronHostLinkFinsFrameMode.Direct, options.FrameMode);
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
