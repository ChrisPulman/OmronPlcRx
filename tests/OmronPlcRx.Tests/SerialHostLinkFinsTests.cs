// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Ports;
using OmronPlcRx;
using OmronPlcRx.Enums;
using TUnit.Core;

namespace OmronPlcRx.Tests;

public sealed class SerialHostLinkFinsTests
{
    [Test]
    public async Task ConnectionMethod_ExposesSerialTransport()
    {
        await Assert.That(Enum.IsDefined(typeof(ConnectionMethod), "Serial")).IsTrue();
    }

    [Test]
    public async Task SerialOptions_DefaultToCommonOmronHostLinkSettings()
    {
        var options = new OmronSerialOptions("COM1");

        await Assert.That(options.PortName).IsEqualTo("COM1");
        await Assert.That(options.BaudRate).IsEqualTo(9600);
        await Assert.That(options.DataBits).IsEqualTo(7);
        await Assert.That(options.Parity).IsEqualTo(Parity.Even);
        await Assert.That(options.StopBits).IsEqualTo(StopBits.Two);
        await Assert.That(options.Handshake).IsEqualTo(Handshake.None);
        await Assert.That(options.Protocol).IsEqualTo(OmronSerialProtocol.HostLinkFins);
        await Assert.That(options.HostLinkUnitNumber).IsEqualTo((byte)0);
        await Assert.That(options.ResponseWaitTime).IsEqualTo((byte)0);
        await Assert.That(options.FrameMode).IsEqualTo(OmronHostLinkFinsFrameMode.Direct);
    }

    [Test]
    public async Task SerialOptions_DefaultFrameLengthMatchesCs1ToolbusConfiguration()
    {
        var options = new OmronSerialOptions("COM1");

        await Assert.That(options.MaximumFrameLength).IsEqualTo(1004);
    }

    [Test]
    public async Task SerialConnection_AllowsDirectCpuDestinationNodeZero()
    {
        using var plc = new OmronPlcRx(
            localNodeId: 11,
            remoteNodeId: 0,
            serialOptions: new OmronSerialOptions("COM1"),
            timeout: 2000,
            retries: 0,
            pollInterval: TimeSpan.FromSeconds(30));

        await Assert.That(plc.IsDisposed).IsFalse();
    }

    [Test]
    public async Task SerialOptions_CanSelectToolbusProtocol()
    {
        var options = new OmronSerialOptions("COM1")
        {
            Protocol = OmronSerialProtocol.Toolbus,
            BaudRate = 115200,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
        };

        await Assert.That(options.Protocol).IsEqualTo(OmronSerialProtocol.Toolbus);
        await Assert.That(options.MaximumFrameLength).IsEqualTo(1004);
    }

    [Test]
    public async Task SerialOptions_CreateToolbusUsesCommonToolbusSettings()
    {
        var options = OmronSerialOptions.CreateToolbus("COM1");

        await Assert.That(options.PortName).IsEqualTo("COM1");
        await Assert.That(options.Protocol).IsEqualTo(OmronSerialProtocol.Toolbus);
        await Assert.That(options.BaudRate).IsEqualTo(115200);
        await Assert.That(options.DataBits).IsEqualTo(8);
        await Assert.That(options.Parity).IsEqualTo(Parity.None);
        await Assert.That(options.StopBits).IsEqualTo(StopBits.One);
        await Assert.That(options.Handshake).IsEqualTo(Handshake.None);
        await Assert.That(options.MaximumFrameLength).IsEqualTo(1004);
    }

    [Test]
    public void SerialOptions_ValidateIgnoresHostLinkOnlyValuesForToolbus()
    {
        var options = OmronSerialOptions.CreateToolbus("COM1") with
        {
            HostLinkUnitNumber = 255,
            ResponseWaitTime = 255,
        };

        options.Validate();
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_ExposesSynchronizationFrame()
    {
        await Assert.That(Convert.ToHexString(ToolbusFinsFrameCodec.SynchronizationFrame.ToArray())).IsEqualTo("AC01");
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsTooShortFinsRequest()
    {
        var fins = new byte[11];

        var ex = CaptureException<ArgumentException>(() => ToolbusFinsFrameCodec.EncodeRequest(fins));

        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_EncodesBinaryFinsRequestWithLengthAndChecksum()
    {
        var fins = Convert.FromHexString("800002000000000000050101");

        var frame = ToolbusFinsFrameCodec.EncodeRequest(fins);

        await Assert.That(Convert.ToHexString(frame.ToArray())).IsEqualTo("AB000E8000020000000000000501010142");
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_EncodesMaximumLengthRequestAcceptedByLengthField()
    {
        var fins = new byte[ushort.MaxValue - 2];

        var frame = ToolbusFinsFrameCodec.EncodeRequest(fins);

        await Assert.That(frame.Length).IsEqualTo(ushort.MaxValue + 3);
        await Assert.That(Convert.ToHexString(frame.Span[..3])).IsEqualTo("ABFFFF");
        await Assert.That(Convert.ToHexString(frame.Span[^2..])).IsEqualTo("02A9");
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsRequestLongerThanLengthFieldAllows()
    {
        var fins = new byte[ushort.MaxValue - 1];

        var ex = CaptureException<ArgumentOutOfRangeException>(() => ToolbusFinsFrameCodec.EncodeRequest(fins));

        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_DecodesToolbusFrameToBinaryFinsResponse()
    {
        var payload = Convert.FromHexString("C0000200000000000005010100001234");
        var frame = ToolbusFinsFrameCodec.EncodeRequest(payload);

        var decoded = ToolbusFinsFrameCodec.DecodeResponse(frame);

        await Assert.That(Convert.ToHexString(decoded.ToArray())).IsEqualTo(Convert.ToHexString(payload));
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsInvalidStartByte()
    {
        var frame = Convert.FromHexString("AA000E8000020000000000000501010142");

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("0xAB");
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsDeclaredLengthMismatch()
    {
        var frame = Convert.FromHexString("AB000D8000020000000000000501010142");

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("declared length");
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsTooSmallDeclaredLength()
    {
        var frame = Convert.FromHexString("AB00010142");

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("length");
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsTooShortFinsResponsePayload()
    {
        var frame = ToolbusFinsFrameCodec.EncodeRequest(Convert.FromHexString("C00002000000000000050101"));

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("payload");
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsInvalidFinsResponseHeader()
    {
        var frame = ToolbusFinsFrameCodec.EncodeRequest(Convert.FromHexString("8000020000000000000501010000"));

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("FINS header");
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsInvalidChecksum()
    {
        var frame = Convert.FromHexString("AB000E8000020000000000000501010143");

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message.Contains("checksum", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task ToolbusFinsFrameCodec_CalculatesChecksumWithSixteenBitWraparound()
    {
        var data = new byte[258];
        Array.Fill(data, (byte)0xFF);

        var checksum = ToolbusFinsFrameCodec.CalculateChecksum(data);

        await Assert.That(checksum).IsEqualTo((ushort)0x00FE);
    }

    [Test]
    public async Task HostLinkFcs_CalculatesXorAcrossFrameText()
    {
        var fcs = HostLinkFinsFrameCodec.CalculateFcs("@00FA0000000010101820064000002");

        await Assert.That(fcs).IsEqualTo("7C");
    }

    [Test]
    public async Task HostLinkFinsFrameCodec_EncodesBinaryFinsRequestAsAsciiDirectHostLinkFrame()
    {
        var options = new OmronSerialOptions("COM1")
        {
            HostLinkUnitNumber = 0,
            ResponseWaitTime = 0,
        };
        var codec = new HostLinkFinsFrameCodec(options);
        var fins = Convert.FromHexString("800002000100000B00010101820064000002");

        var frame = codec.EncodeRequest(fins);

        await Assert.That(frame).IsEqualTo("@00FA00000000101018200640000027C*\r");
    }

    [Test]
    public async Task HostLinkFinsFrameCodec_DecodesAsciiDirectHostLinkResponseToBinaryFinsResponse()
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

        await Assert.That(Convert.ToHexString(decoded.ToArray())).IsEqualTo("40000200000000000001010100001234");
    }

    [Test]
    public async Task HostLinkFinsFrameCodec_RejectsHostLinkEndCodeFailures()
    {
        var codec = new HostLinkFinsFrameCodec(new OmronSerialOptions("COM1"));

        var ex = CaptureException<OmronPLCException>(() => codec.DecodeResponse("@00FA014000000101010000123447*\r"));

        await Assert.That(ex.Message).Contains("end code");
    }

    [Test]
    public async Task HostLinkFinsFrameCodec_RejectsInvalidFcs()
    {
        var codec = new HostLinkFinsFrameCodec(new OmronSerialOptions("COM1"));

        var ex = CaptureException<OmronPLCException>(() => codec.DecodeResponse("@00FA004000000101010000123400*\r"));

        await Assert.That(ex.Message).Contains("FCS");
    }

    private static TException CaptureException<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected exception of type {typeof(TException).Name}.");
    }
}
