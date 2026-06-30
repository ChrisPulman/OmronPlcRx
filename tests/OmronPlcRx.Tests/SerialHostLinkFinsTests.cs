// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Ports;
using OmronPlcRx;
using OmronPlcRx.Enums;
using TUnit.Core;

namespace OmronPlcRx.Tests;

/// <summary>Tests serial Host Link FINS and Toolbus framing behavior.</summary>
public sealed class SerialHostLinkFinsTests
{
    /// <summary>Verifies the connection method enum exposes serial transport.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ConnectionMethod_ExposesSerialTransport()
    {
        await Assert.That(Enum.IsDefined(typeof(ConnectionMethod), "Serial")).IsTrue();
    }

    /// <summary>Verifies default serial options match common Omron Host Link settings.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
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
        await Assert.That(options.RtsEnable).IsFalse();
        await Assert.That(options.DtrEnable).IsFalse();
        await Assert.That(options.Protocol).IsEqualTo(OmronSerialProtocol.HostLinkFins);
        await Assert.That(options.HostLinkUnitNumber).IsEqualTo((byte)0);
        await Assert.That(options.ResponseWaitTime).IsEqualTo((byte)0);
        await Assert.That(options.FrameMode).IsEqualTo(OmronHostLinkFinsFrameMode.Direct);
    }

    /// <summary>Verifies the default maximum frame length matches CS1 Toolbus configuration.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialOptions_DefaultFrameLengthMatchesCs1ToolbusConfiguration()
    {
        var options = new OmronSerialOptions("COM1");

        await Assert.That(options.MaximumFrameLength).IsEqualTo(1004);
    }

    /// <summary>Verifies serial connections accept direct CPU destination node zero.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
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

    /// <summary>Verifies Toolbus protocol selection through serial options.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
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

    /// <summary>Verifies the Toolbus factory applies common Toolbus serial settings.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
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
        await Assert.That(options.RtsEnable).IsTrue();
        await Assert.That(options.DtrEnable).IsFalse();
        await Assert.That(options.MaximumFrameLength).IsEqualTo(1004);
    }

    /// <summary>Verifies Toolbus validation ignores Host Link-only values.</summary>
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

    /// <summary>Verifies the Toolbus synchronization frame is exposed.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_ExposesSynchronizationFrame()
    {
        await Assert.That(Convert.ToHexString(ToolbusFinsFrameCodec.SynchronizationFrame.ToArray())).IsEqualTo("AC01");
    }

    /// <summary>Verifies Toolbus request encoding rejects too-short FINS requests.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsTooShortFinsRequest()
    {
        var fins = new byte[11];

        var ex = CaptureException<ArgumentException>(() => ToolbusFinsFrameCodec.EncodeRequest(fins));

        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Verifies Toolbus request encoding includes length and checksum.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_EncodesBinaryFinsRequestWithLengthAndChecksum()
    {
        var fins = Convert.FromHexString("800002000000000000050101");

        var frame = ToolbusFinsFrameCodec.EncodeRequest(fins);

        await Assert.That(Convert.ToHexString(frame.ToArray())).IsEqualTo("AB000E8000020000000000000501010142");
    }

    /// <summary>Verifies maximum Toolbus request length is accepted by the length field.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_EncodesMaximumLengthRequestAcceptedByLengthField()
    {
        var fins = new byte[ushort.MaxValue - 2];

        var frame = ToolbusFinsFrameCodec.EncodeRequest(fins);

        await Assert.That(frame.Length).IsEqualTo(ushort.MaxValue + 3);
        await Assert.That(Convert.ToHexString(frame.Span[..3])).IsEqualTo("ABFFFF");
        await Assert.That(Convert.ToHexString(frame.Span[^2..])).IsEqualTo("02A9");
    }

    /// <summary>Verifies Toolbus request encoding rejects frames beyond the length field limit.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsRequestLongerThanLengthFieldAllows()
    {
        var fins = new byte[ushort.MaxValue - 1];

        var ex = CaptureException<ArgumentOutOfRangeException>(() => ToolbusFinsFrameCodec.EncodeRequest(fins));

        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Verifies Toolbus responses decode back to binary FINS payloads.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_DecodesToolbusFrameToBinaryFinsResponse()
    {
        var payload = Convert.FromHexString("C0000200000000000005010100001234");
        var frame = ToolbusFinsFrameCodec.EncodeRequest(payload);

        var decoded = ToolbusFinsFrameCodec.DecodeResponse(frame);

        await Assert.That(Convert.ToHexString(decoded.ToArray())).IsEqualTo(Convert.ToHexString(payload));
    }

    /// <summary>Verifies Toolbus response decoding rejects invalid start bytes.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsInvalidStartByte()
    {
        var frame = Convert.FromHexString("AA000E8000020000000000000501010142");

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("0xAB");
    }

    /// <summary>Verifies Toolbus response decoding rejects declared length mismatches.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsDeclaredLengthMismatch()
    {
        var frame = Convert.FromHexString("AB000D8000020000000000000501010142");

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("declared length");
    }

    /// <summary>Verifies Toolbus response decoding rejects too-small declared lengths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsTooSmallDeclaredLength()
    {
        var frame = Convert.FromHexString("AB00010142");

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("length");
    }

    /// <summary>Verifies Toolbus response decoding rejects too-short FINS payloads.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsTooShortFinsResponsePayload()
    {
        var frame = ToolbusFinsFrameCodec.EncodeRequest(Convert.FromHexString("C00002000000000000050101"));

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("payload");
    }

    /// <summary>Verifies Toolbus response decoding rejects invalid FINS headers.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsInvalidFinsResponseHeader()
    {
        var frame = ToolbusFinsFrameCodec.EncodeRequest(Convert.FromHexString("8000020000000000000501010000"));

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message).Contains("FINS header");
    }

    /// <summary>Verifies Toolbus response decoding rejects invalid checksums.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_RejectsInvalidChecksum()
    {
        var frame = Convert.FromHexString("AB000E8000020000000000000501010143");

        var ex = CaptureException<OmronPLCException>(() => ToolbusFinsFrameCodec.DecodeResponse(frame));

        await Assert.That(ex.Message.Contains("checksum", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    /// <summary>Verifies Toolbus checksum calculation wraps at sixteen bits.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ToolbusFinsFrameCodec_CalculatesChecksumWithSixteenBitWraparound()
    {
        var data = new byte[258];
        Array.Fill(data, (byte)0xFF);

        var checksum = ToolbusFinsFrameCodec.CalculateChecksum(data);

        await Assert.That(checksum).IsEqualTo((ushort)0x00FE);
    }

    /// <summary>Verifies Host Link FCS calculation XORs the frame text.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task HostLinkFcs_CalculatesXorAcrossFrameText()
    {
        var fcs = HostLinkFinsFrameCodec.CalculateFcs("@00FA0000000010101820064000002");

        await Assert.That(fcs).IsEqualTo("7C");
    }

    /// <summary>Verifies Host Link FINS requests encode as ASCII direct Host Link frames.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
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

    /// <summary>Verifies Host Link FINS responses decode to binary FINS payloads.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task HostLinkFinsFrameCodec_DecodesAsciiDirectHostLinkResponseToBinaryFinsResponse()
    {
        var options = new OmronSerialOptions("COM1")
        {
            HostLinkUnitNumber = 0,
            ResponseWaitTime = 0,
        };
        var codec = new HostLinkFinsFrameCodec(options);
        const string payload = "40000001010100001234";
        var body = "@00FA00" + payload;
        var frame = body + HostLinkFinsFrameCodec.CalculateFcs(body) + "*\r";

        var decoded = codec.DecodeResponse(frame);

        await Assert.That(Convert.ToHexString(decoded.ToArray())).IsEqualTo("40000200000000000001010100001234");
    }

    /// <summary>Verifies Host Link decoding rejects non-zero end codes.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task HostLinkFinsFrameCodec_RejectsHostLinkEndCodeFailures()
    {
        var codec = new HostLinkFinsFrameCodec(new OmronSerialOptions("COM1"));

        var ex = CaptureException<OmronPLCException>(() => codec.DecodeResponse("@00FA014000000101010000123447*\r"));

        await Assert.That(ex.Message).Contains("end code");
    }

    /// <summary>Verifies Host Link decoding rejects invalid frame check sequences.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
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

        throw new InvalidOperationException($"Expected exception of type {nameof(TException)}.");
    }
}
