// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using OmronPlcRx.Core;
using OmronPlcRx.Core.Channels;
using OmronPlcRx.Core.Converters;
using OmronPlcRx.Core.Requests;
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Core.Results;
using OmronPlcRx.Core.Types;
using OmronPlcRx.Enums;
using OmronPlcRx.Results;
using OmronPlcRx.Tags;
using TUnit.Core;
using CoreTcpClient = OmronPlcRx.Core.TcpClient;
using CoreUdpClient = OmronPlcRx.Core.UdpClient;
using NetTcpListener = System.Net.Sockets.TcpListener;
using NetUdpClient = System.Net.Sockets.UdpClient;

namespace OmronPlcRx.Tests;

/// <summary>Tests deterministic protocol, conversion and validation paths.</summary>
public sealed class CoreProtocolCoverageTests
{
    /// <summary>Verifies BCD conversion round trips supported scalar widths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BcdConverter_RoundTripsSupportedScalarWidths()
    {
        var bcd16 = BCDConverter.GetBCDWord((short)1234);
        var bcdU16 = BCDConverter.GetBCDWord((ushort)9876);
        var bcd32 = BCDConverter.GetBCDWords(12_345_678);
        var bcdU32 = BCDConverter.GetBCDWords(87_654_321u);

        await Assert.That(BCDConverter.ToByte(0x45)).IsEqualTo((byte)45);
        await Assert.That(BCDConverter.GetBCDByte(45)).IsEqualTo((byte)0x45);
        await Assert.That(BCDConverter.ToInt16(bcd16)).IsEqualTo((short)1234);
        await Assert.That(BCDConverter.ToUInt16(bcdU16)).IsEqualTo((ushort)9876);
        await Assert.That(BCDConverter.ToInt32(bcd32[0], bcd32[1])).IsEqualTo(12_345_678);
        await Assert.That(BCDConverter.ToUInt32(bcdU32[0], bcdU32[1])).IsEqualTo(87_654_321u);
        await Assert.That(Convert.ToHexString(BCDConverter.GetBCDBytes((short)1234))).IsEqualTo("3412");
        await Assert.That(Convert.ToHexString(BCDConverter.GetBCDBytes((ushort)1234))).IsEqualTo("3412");
        await Assert.That(Convert.ToHexString(BCDConverter.GetBCDBytes(12_345_678))).IsEqualTo("78563412");
        await Assert.That(Convert.ToHexString(BCDConverter.GetBCDBytes(12_345_678u))).IsEqualTo("78563412");
    }

    /// <summary>Verifies BCD conversion validates null and invalid byte lengths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BcdConverter_RejectsInvalidByteInputs()
    {
        await Assert.That(CaptureException<ArgumentNullException>(() => BCDConverter.ToInt16(null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToInt16([]))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => BCDConverter.ToUInt16(null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToUInt16([0x12]))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => BCDConverter.ToInt32(null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToInt32([0x12, 0x34]))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => BCDConverter.ToUInt32(null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToUInt32([0x12, 0x34, 0x56]))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => BCDConverter.ToUInt32([0x12, 0x34, 0x56, 0x78, 0x90]))).IsNotNull();
    }

    /// <summary>Verifies PLC tag values convert to write words and back to typed read values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task PlcTagValueCodec_ConvertsSupportedWordTypes()
    {
        await Assert.That(PlcTagValueCodec.TryGetSingleWord(typeof(byte), (byte)12, out var byteWord)).IsTrue();
        await Assert.That(byteWord).IsEqualTo((short)12);
        await Assert.That(PlcTagValueCodec.TryGetSingleWord(typeof(ushort), (ushort)0xFEDC, out var ushortWord)).IsTrue();
        await Assert.That(Convert.ToHexString(BitConverter.GetBytes(ushortWord))).IsEqualTo("DCFE");
        await Assert.That(PlcTagValueCodec.TryGetSingleWord(typeof(short), (short)-7, out var shortWord)).IsTrue();
        await Assert.That(shortWord).IsEqualTo((short)-7);
        await Assert.That(PlcTagValueCodec.TryGetSingleWord(typeof(Bcd16), new Bcd16(1234), out var bcd16Word)).IsTrue();
        await Assert.That(BCDConverter.ToInt16(bcd16Word)).IsEqualTo((short)1234);
        await Assert.That(PlcTagValueCodec.TryGetSingleWord(typeof(BcdU16), new BcdU16(9876), out var bcdU16Word)).IsTrue();
        await Assert.That(BCDConverter.ToUInt16(bcdU16Word)).IsEqualTo((ushort)9876);
        await Assert.That(PlcTagValueCodec.TryGetSingleWord(typeof(string), "x", out _)).IsFalse();

        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(int), 0x12345678, out var intWords)).IsTrue();
        await Assert.That(PlcTagValueCodec.ConvertReadWords(typeof(int), intWords)).IsEqualTo(0x12345678);
        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(uint), 0x89ABCDEFu, out var uintWords)).IsTrue();
        await Assert.That(PlcTagValueCodec.ConvertReadWords(typeof(uint), uintWords)).IsEqualTo(0x89ABCDEFu);
        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(float), 123.5f, out var floatWords)).IsTrue();
        await Assert.That(PlcTagValueCodec.ConvertReadWords(typeof(float), floatWords)).IsEqualTo(123.5f);
        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(double), 123.5d, out var doubleWords)).IsTrue();
        await Assert.That(PlcTagValueCodec.ConvertReadWords(typeof(double), doubleWords)).IsEqualTo(123.5d);
        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(Bcd32), new Bcd32(12_345_678), out var bcd32Words)).IsTrue();
        await Assert.That(((Bcd32)PlcTagValueCodec.ConvertReadWords(typeof(Bcd32), bcd32Words)).Value).IsEqualTo(12_345_678);
        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(BcdU32), new BcdU32(87_654_321), out var bcdU32Words)).IsTrue();
        await Assert.That(((BcdU32)PlcTagValueCodec.ConvertReadWords(typeof(BcdU32), bcdU32Words)).Value).IsEqualTo(87_654_321u);
        await Assert.That(PlcTagValueCodec.TryGetWordArray(typeof(string), "x", out _)).IsFalse();
    }

    /// <summary>Verifies PLC tag string conversions handle null padding and length trimming.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task PlcTagValueCodec_ConvertsStringsAndReadWordCounts()
    {
        var words = PlcTagValueCodec.GetStringWords("ABCD", 5);

        await Assert.That(Convert.ToHexString(ToBigEndianBytes(words))).IsEqualTo("414243440000");
        await Assert.That(PlcTagValueCodec.GetStringFromWords(words, 5, 3)).IsEqualTo("ABCD");
        await Assert.That(PlcTagValueCodec.GetStringFromWords([(short)0x4142, (short)0x4344], 3, 2)).IsEqualTo("ABC");
        await Assert.That(PlcTagValueCodec.GetReadWordCount(typeof(short))).IsEqualTo(1);
        await Assert.That(PlcTagValueCodec.GetReadWordCount(typeof(double))).IsEqualTo(4);
        await Assert.That(PlcTagValueCodec.GetReadWordCount(typeof(string))).IsEqualTo(0);
        await Assert.That(CaptureException<NotSupportedException>(() => PlcTagValueCodec.ThrowIfBitIndexedString(1))).IsNotNull();
        await Assert.That(CaptureException<NotSupportedException>(() => PlcTagValueCodec.ConvertReadWords(typeof(decimal), [1]))).IsNotNull();
    }

    /// <summary>Verifies internal FINS request builders produce stable protocol bytes.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsRequests_BuildExpectedMessageBytes()
    {
        using var plc = CreateConnection();
        var readWords = ReadMemoryAreaWordRequest.CreateNew(plc, 100, 2, MemoryWordDataType.DataMemory);
        var writeWords = WriteMemoryAreaWordRequest.CreateNew(plc, 100, MemoryWordDataType.DataMemory, [0x1234, -2]);
        var readBits = ReadMemoryAreaBitRequest.CreateNew(plc, 100, 3, 2, MemoryBitDataType.CommonIO);
        var writeBits = WriteMemoryAreaBitRequest.CreateNew(plc, 100, 3, MemoryBitDataType.CommonIO, [true, false, true]);
        var writeClock = WriteClockRequest.CreateNew(plc, new DateTime(2026, 6, 30, 14, 25, 59), 2);

        await Assert.That(ToHex(readWords.BuildMessage(0x44))).IsEqualTo("800002000200000100440101820064000002");
        await Assert.That(ToHex(writeWords.BuildMessage(0x45))).IsEqualTo("8000020002000001004501028200640000021234FFFE");
        await Assert.That(ToHex(readBits.BuildMessage(0x46))).IsEqualTo("800002000200000100460101300064030002");
        await Assert.That(ToHex(writeBits.BuildMessage(0x47))).IsEqualTo("800002000200000100470102300064030003010001");
        await Assert.That(ToHex(writeClock.BuildMessage(0x48))).IsEqualTo("80000200020000010048070226063014255902");
    }

    /// <summary>Verifies FINS response creation validates and extracts response payload data.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsResponses_ValidateAndExtractPayloads()
    {
        using var plc = CreateConnection();
        var readWords = ReadMemoryAreaWordRequest.CreateNew(plc, 100, 2, MemoryWordDataType.DataMemory);
        _ = readWords.BuildMessage(0x51);
        var wordResponse = CreateResponse(readWords, [0x12, 0x34, 0xFF, 0xFE]);

        var readBits = ReadMemoryAreaBitRequest.CreateNew(plc, 100, 0, 3, MemoryBitDataType.CommonIO);
        _ = readBits.BuildMessage(0x52);
        var bitResponse = CreateResponse(readBits, [1, 0, 2]);

        var readClock = ReadClockRequest.CreateNew(plc);
        _ = readClock.BuildMessage(0x53);
        var clock = ReadClockResponse.ExtractClock(readClock, CreateResponse(readClock, [0x26, 0x06, 0x30, 0x14, 0x25, 0x59, 0x02]));

        var cycleTime = ReadCycleTimeRequest.CreateNew(plc);
        _ = cycleTime.BuildMessage(0x54);
        var cycle = ReadCycleTimeResponse.ExtractCycleTime(cycleTime, CreateResponse(cycleTime, [0x00, 0x00, 0x01, 0x23, 0x00, 0x00, 0x04, 0x56, 0x00, 0x00, 0x00, 0x00]));

        var cpuData = ReadCPUUnitDataRequest.CreateNew(plc);
        _ = cpuData.BuildMessage(0x55);
        var cpu = ReadCPUUnitDataResponse.ExtractData(CreateResponse(cpuData, BuildCpuUnitData("CS1G-CPU42H", "1.23")));

        await Assert.That(Convert.ToHexString(ToBigEndianBytes(ReadMemoryAreaWordResponse.ExtractValues(readWords, wordResponse)))).IsEqualTo("1234FFFE");
        await Assert.That(ToBitText(ReadMemoryAreaBitResponse.ExtractValues(readBits, bitResponse))).IsEqualTo("1,0,1");
        await Assert.That(clock.ClockDateTime).IsEqualTo(new DateTime(2026, 6, 30, 14, 25, 59));
        await Assert.That(clock.DayOfWeek).IsEqualTo((byte)2);
        await Assert.That(cycle.AverageCycleTime).IsEqualTo(12.3d);
        await Assert.That(cycle.MaximumCycleTime).IsEqualTo(45.6d);
        await Assert.That(cycle.MinimumCycleTime).IsEqualTo(0d);
        await Assert.That(cpu.ControllerModel).IsEqualTo("CS1G-CPU42H");
        await Assert.That(cpu.ControllerVersion).IsEqualTo("1.23");
    }

    /// <summary>Verifies FINS response creation reports protocol mismatches and PLC response errors.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task FinsResponses_RejectInvalidFramesAndResponseCodes()
    {
        using var plc = CreateConnection();
        var request = ReadMemoryAreaWordRequest.CreateNew(plc, 100, 1, MemoryWordDataType.DataMemory);
        _ = request.BuildMessage(0x61);

        await Assert.That(CaptureException<FINSException>(() => FINSResponse.CreateNew(new byte[13], request)).Message).Contains("too short");
        await Assert.That(CaptureException<FINSException>(() => FINSResponse.CreateNew(BuildResponseFrame(request, [], functionCode: 0xFF), request)).Message).Contains("Invalid Function Code");
        await Assert.That(CaptureException<FINSException>(() => FINSResponse.CreateNew(BuildResponseFrame(request, [], subFunctionCode: 0xFF), request)).Message).Contains("Invalid Sub Function Code");
        await Assert.That(CaptureException<FINSException>(() => FINSResponse.CreateNew(BuildResponseFrame(request, [], mainResponseCode: 0x82), request)).Message).Contains("Network Relay");
        await Assert.That(CaptureException<FINSException>(() => FINSResponse.CreateNew(BuildResponseFrame(request, [], mainResponseCode: 0x02, subResponseCode: 0x05), request)).Message).Contains("Response Timeout");
        await Assert.That(CaptureException<FINSException>(() => FINSResponse.CreateNew(BuildResponseFrame(request, [], serviceId: 0x62), request)).Message).Contains("Service ID");
        await Assert.That(FINSResponse.ValidateFunctionCode(0x01)).IsTrue();
        await Assert.That(FINSResponse.ValidateFunctionCode(0xFF)).IsFalse();
        await Assert.That(FINSResponse.ValidateSubFunctionCode(0x01, 0x01)).IsTrue();
        await Assert.That(FINSResponse.ValidateSubFunctionCode(0x01, 0xFF)).IsFalse();
    }

    /// <summary>Verifies response extractors validate short payloads and write acknowledgements validate null arguments.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ResponseExtractors_RejectShortPayloadsAndNullAcknowledgements()
    {
        using var plc = CreateConnection();
        var readWords = ReadMemoryAreaWordRequest.CreateNew(plc, 100, 2, MemoryWordDataType.DataMemory);
        _ = readWords.BuildMessage(0x71);
        var readBits = ReadMemoryAreaBitRequest.CreateNew(plc, 100, 0, 2, MemoryBitDataType.CommonIO);
        _ = readBits.BuildMessage(0x72);
        var readClock = ReadClockRequest.CreateNew(plc);
        _ = readClock.BuildMessage(0x73);
        var cycleTime = ReadCycleTimeRequest.CreateNew(plc);
        _ = cycleTime.BuildMessage(0x74);
        var cpuData = ReadCPUUnitDataRequest.CreateNew(plc);
        _ = cpuData.BuildMessage(0x75);
        var writeWords = WriteMemoryAreaWordRequest.CreateNew(plc, 100, MemoryWordDataType.DataMemory, [1]);
        var writeBits = WriteMemoryAreaBitRequest.CreateNew(plc, 100, 0, MemoryBitDataType.CommonIO, [true]);
        var writeClock = WriteClockRequest.CreateNew(plc, new DateTime(2026, 6, 30), 2);
        var response = CreateResponse(writeWords, []);

        await Assert.That(CaptureException<FINSException>(() => ReadMemoryAreaWordResponse.ExtractValues(readWords, CreateResponse(readWords, [1])))).IsNotNull();
        await Assert.That(CaptureException<FINSException>(() => ReadMemoryAreaBitResponse.ExtractValues(readBits, CreateResponse(readBits, [1])))).IsNotNull();
        await Assert.That(CaptureException<FINSException>(() => ReadClockResponse.ExtractClock(readClock, CreateResponse(readClock, [0x26])))).IsNotNull();
        await Assert.That(CaptureException<FINSException>(() => ReadClockResponse.ExtractClock(readClock, CreateResponse(readClock, [0xA0, 0x01, 0x01, 0, 0, 0, 0])))).IsNotNull();
        await Assert.That(CaptureException<FINSException>(() => ReadCycleTimeResponse.ExtractCycleTime(cycleTime, CreateResponse(cycleTime, [0])))).IsNotNull();
        await Assert.That(CaptureException<FINSException>(() => ReadCPUUnitDataResponse.ExtractData(CreateResponse(cpuData, [0])))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => WriteMemoryAreaWordResponse.Validate(null!, response))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => WriteMemoryAreaWordResponse.Validate(writeWords, null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => WriteMemoryAreaBitResponse.Validate(null!, response))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => WriteMemoryAreaBitResponse.Validate(writeBits, null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => WriteClockResponse.Validate(null!, response))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => WriteClockResponse.Validate(writeClock, null!))).IsNotNull();
    }

    /// <summary>Verifies Host Link codec network framing and additional validation paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task HostLinkFinsFrameCodec_HandlesNetworkModeAndValidationFailures()
    {
        var options = new OmronSerialOptions("COM1") { FrameMode = OmronHostLinkFinsFrameMode.Network };
        var codec = new HostLinkFinsFrameCodec(options);
        var fins = Convert.FromHexString("800002000100000B00010101820064000002");
        var body = "@00FA0" + Convert.ToHexString(fins);
        var frame = body + HostLinkFinsFrameCodec.CalculateFcs(body) + "*\r";
        var responseBody = "@00FA00" + Convert.ToHexString(fins);
        var responseFrame = responseBody + HostLinkFinsFrameCodec.CalculateFcs(responseBody) + "*\r";

        await Assert.That(codec.EncodeRequest(fins)).IsEqualTo(frame);
        await Assert.That(Convert.ToHexString(codec.DecodeResponse(responseFrame).ToArray())).IsEqualTo(Convert.ToHexString(fins));
        await Assert.That(CaptureException<ArgumentNullException>(() => _ = CreateHostLinkCodec(null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => HostLinkFinsFrameCodec.CalculateFcs(null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentException>(() => codec.EncodeRequest(new byte[11]))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => codec.DecodeResponse(null!))).IsNotNull();
        await Assert.That(CaptureException<OmronPLCException>(() => codec.DecodeResponse("@00FA0000"))).IsNotNull();
        await Assert.That(CaptureException<OmronPLCException>(() => codec.DecodeResponse("@00FA0000*\r"))).IsNotNull();
        await Assert.That(CaptureException<OmronPLCException>(() => codec.DecodeResponse("#00FA0000" + HostLinkFinsFrameCodec.CalculateFcs("#00FA0000") + "*\r")).Message).Contains("'@'");
        await Assert.That(CaptureException<OmronPLCException>(() => codec.DecodeResponse("@01FA0000" + HostLinkFinsFrameCodec.CalculateFcs("@01FA0000") + "*\r")).Message).Contains("unit");
        await Assert.That(CaptureException<OmronPLCException>(() => codec.DecodeResponse("@00FB0000" + HostLinkFinsFrameCodec.CalculateFcs("@00FB0000") + "*\r")).Message).Contains("header");

        var directCodec = new HostLinkFinsFrameCodec(new OmronSerialOptions("COM1"));
        const string ShortDirectBody = "@00FA00010203";
        await Assert.That(CaptureException<OmronPLCException>(() => directCodec.DecodeResponse(ShortDirectBody + HostLinkFinsFrameCodec.CalculateFcs(ShortDirectBody) + "*\r")).Message).Contains("too short");
    }

    /// <summary>Verifies serial option and connection metadata validation paths.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialOptionsAndConnectionMetadata_ValidateInputs()
    {
        await Assert.That(CaptureException<ArgumentException>(() => _ = CreateSerialOptions(" "))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => new OmronSerialOptions("COM1") { Protocol = (OmronSerialProtocol)255 }.Validate())).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => new OmronSerialOptions("COM1") { HostLinkUnitNumber = 32 }.Validate())).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => new OmronSerialOptions("COM1") { ResponseWaitTime = 16 }.Validate())).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => new OmronSerialOptions("COM1") { BaudRate = 0 }.Validate())).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => new OmronSerialOptions("COM1") { DataBits = 4 }.Validate())).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => new OmronSerialOptions("COM1") { DataBits = 9 }.Validate())).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => new OmronSerialOptions("COM1") { MaximumFrameLength = 0 }.Validate())).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => OmronPLCConnectionMetadata.ValidateNodeIdentifiers(0, 2, ConnectionMethod.UDP))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => OmronPLCConnectionMetadata.ValidateNodeIdentifiers(1, 0, ConnectionMethod.UDP))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => OmronPLCConnectionMetadata.ValidateNodeIdentifiers(1, 255, ConnectionMethod.UDP))).IsNotNull();
        await Assert.That(CaptureException<ArgumentException>(() => OmronPLCConnectionMetadata.ValidateNodeIdentifiers(1, 1, ConnectionMethod.UDP))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => OmronPLCConnectionMetadata.ValidateRemoteHost(null!))).IsNotNull();
        await Assert.That(CaptureException<ArgumentException>(() => OmronPLCConnectionMetadata.ValidateRemoteHost(string.Empty))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => OmronPLCConnectionMetadata.ValidatePort(ConnectionMethod.UDP, 0))).IsNotNull();
        await Assert.That(OmronPLCConnectionMetadata.ValidateRemoteHost("127.0.0.1")).IsEqualTo("127.0.0.1");
        OmronPLCConnectionMetadata.ValidatePort(ConnectionMethod.Serial, 0);
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("CS1G-CPU42H")).IsEqualTo(PLCType.C_Series);
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("NJ501-1300")).IsEqualTo(PLCType.NJ501);
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("NY532-5400")).IsEqualTo(PLCType.NJ_NX_NY_Series);
        await Assert.That(OmronPLCConnectionMetadata.GetPLCType("UNKNOWN")).IsEqualTo(PLCType.Unknown);
    }

    /// <summary>Verifies serial Toolbus frame-buffer helpers trim noise and find sync frames.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SerialToolbusFrameBuffer_TrimsNoiseAndFindsSynchronizationFrames()
    {
        var noisy = new List<byte> { 0x00, 0x01, 0xAB, 0x02 };
        SerialToolbusFrameBuffer.TrimBeforeFrameStart(noisy);

        var noFrame = new List<byte> { 0x00, 0x01 };
        SerialToolbusFrameBuffer.TrimBeforeFrameStart(noFrame);

        var aligned = new List<byte> { 0xAB, 0x02 };
        SerialToolbusFrameBuffer.TrimBeforeFrameStart(aligned);

        await Assert.That(Convert.ToHexString(noisy.ToArray())).IsEqualTo("AB02");
        await Assert.That(noFrame.Count).IsEqualTo(0);
        await Assert.That(Convert.ToHexString(aligned.ToArray())).IsEqualTo("AB02");
        await Assert.That(SerialToolbusFrameBuffer.ContainsSynchronizationFrame([0x00, 0xAC, 0x01], [0xAC, 0x01])).IsTrue();
        await Assert.That(SerialToolbusFrameBuffer.ContainsSynchronizationFrame([0x00, 0xAC, 0x02], [0xAC, 0x01])).IsFalse();
    }

    /// <summary>Verifies simple value objects expose their initialized values.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task ValueObjects_ExposeConstructorValues()
    {
        var bcd16 = new Bcd16(1234);
        var bcd32 = new Bcd32(12_345_678);
        var bcdU16 = new BcdU16(9876);
        var bcdU32 = new BcdU32(87_654_321);
        var tag = new PlcTag<int>("Speed", "D100") { Value = 42 };
        var expectedClock = new DateTime(2026, 6, 30);
        var attribute = new PlcTagAttribute("D100")
        {
            TagName = "Speed",
            Register = false,
            Observe = false,
            Writable = true,
        };
        var finsInner = new InvalidOperationException("inner");
        var plcInner = new InvalidOperationException("inner");
        var readBits = new ReadBitsResult { BytesSent = 1, PacketsSent = 2, BytesReceived = 3, PacketsReceived = 4, Duration = 5.5d, Values = [true] };
        var readWords = new ReadWordsResult { BytesSent = 1, PacketsSent = 2, BytesReceived = 3, PacketsReceived = 4, Duration = 5.5d, Values = [123] };
        var readClock = new ReadClockResult { BytesSent = 1, PacketsSent = 2, BytesReceived = 3, PacketsReceived = 4, Duration = 5.5d, Clock = expectedClock, DayOfWeek = 2 };
        var readCycleTime = new ReadCycleTimeResult { BytesSent = 1, PacketsSent = 2, BytesReceived = 3, PacketsReceived = 4, Duration = 5.5d, MinimumCycleTime = 1.2d, MaximumCycleTime = 3.4d, AverageCycleTime = 2.3d };
        var writeBits = new WriteBitsResult { BytesSent = 1, PacketsSent = 2, BytesReceived = 3, PacketsReceived = 4, Duration = 5.5d };
        var writeWords = new WriteWordsResult { BytesSent = 1, PacketsSent = 2, BytesReceived = 3, PacketsReceived = 4, Duration = 5.5d };
        var writeClock = new WriteClockResult { BytesSent = 1, PacketsSent = 2, BytesReceived = 3, PacketsReceived = 4, Duration = 5.5d };

        await Assert.That(bcd16.Value).IsEqualTo((short)1234);
        await Assert.That(bcd16.ToString()).IsEqualTo("1234");
        await Assert.That(bcd16.GetHashCode()).IsEqualTo(((short)1234).GetHashCode());
        await Assert.That(bcd32.Value).IsEqualTo(12_345_678);
        await Assert.That(bcd32.ToString()).IsEqualTo("12345678");
        await Assert.That(bcd32.GetHashCode()).IsEqualTo(12_345_678.GetHashCode());
        await Assert.That(bcdU16.Value).IsEqualTo((ushort)9876);
        await Assert.That(bcdU16.ToString()).IsEqualTo("9876");
        await Assert.That(bcdU16.GetHashCode()).IsEqualTo(((ushort)9876).GetHashCode());
        await Assert.That(bcdU32.Value).IsEqualTo(87_654_321u);
        await Assert.That(bcdU32.ToString()).IsEqualTo("87654321");
        await Assert.That(bcdU32.GetHashCode()).IsEqualTo(87_654_321u.GetHashCode());
        await Assert.That(tag.TagName).IsEqualTo("Speed");
        await Assert.That(tag.Address).IsEqualTo("D100");
        await Assert.That(tag.TagType).IsEqualTo(typeof(int));
        await Assert.That(tag.Value).IsEqualTo(42);
        await Assert.That(((IPlcTag)tag).Value).IsEqualTo(42);
        await Assert.That(attribute.Address).IsEqualTo("D100");
        await Assert.That(attribute.TagName).IsEqualTo("Speed");
        await Assert.That(attribute.Register).IsFalse();
        await Assert.That(attribute.Observe).IsFalse();
        await Assert.That(attribute.Writable).IsTrue();
        await Assert.That(new PlcTagAttribute("D101").Register).IsTrue();
        await Assert.That(new PlcTagAttribute("D101").Observe).IsTrue();
        await Assert.That(new FINSException().Message).IsEqualTo("Exception of type 'OmronPlcRx.FINSException' was thrown.");
        await Assert.That(new FINSException("fins").Message).IsEqualTo("fins");
        await Assert.That(new FINSException("fins", finsInner).InnerException).IsEqualTo(finsInner);
        await Assert.That(new OmronPLCException().Message).IsEqualTo("Exception of type 'OmronPlcRx.OmronPLCException' was thrown.");
        await Assert.That(new OmronPLCException("plc").Message).IsEqualTo("plc");
        await Assert.That(new OmronPLCException("plc", plcInner).InnerException).IsEqualTo(plcInner);
        await Assert.That(readBits.BytesSent + readBits.PacketsSent + readBits.BytesReceived + readBits.PacketsReceived).IsEqualTo(10);
        await Assert.That(readBits.Duration).IsEqualTo(5.5d);
        await Assert.That(readBits.Values[0]).IsTrue();
        await Assert.That(readWords.Values[0]).IsEqualTo((short)123);
        await Assert.That(readClock.Clock).IsEqualTo(expectedClock);
        await Assert.That(readClock.DayOfWeek).IsEqualTo(2);
        await Assert.That(readCycleTime.MinimumCycleTime).IsEqualTo(1.2d);
        await Assert.That(readCycleTime.MaximumCycleTime).IsEqualTo(3.4d);
        await Assert.That(readCycleTime.AverageCycleTime).IsEqualTo(2.3d);
        await Assert.That(writeBits.Duration + writeWords.Duration + writeClock.Duration).IsEqualTo(16.5d);
        await Assert.That(CaptureException<ArgumentNullException>(() => _ = CreateTag<int>(null!, "D100"))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => _ = CreateTag<int>("Speed", null!))).IsNotNull();
    }

    /// <summary>Verifies base channel processing maps a valid request attempt to metrics and response data.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BaseChannel_ProcessRequestReturnsMetricsAndParsedResponse()
    {
        using var plc = CreateConnection();
        using var channel = new TestChannel { ResponseData = [0x12, 0x34] };
        var request = ReadMemoryAreaWordRequest.CreateNew(plc, 100, 1, MemoryWordDataType.DataMemory);

        var result = await channel.ProcessRequestAsync(request, 100, 0, CancellationToken.None);

        await Assert.That(result.BytesSent).IsEqualTo(18);
        await Assert.That(result.PacketsSent).IsEqualTo(1);
        await Assert.That(result.BytesReceived).IsEqualTo(16);
        await Assert.That(result.PacketsReceived).IsEqualTo(1);
        await Assert.That(result.Duration >= 0).IsTrue();
        await Assert.That(result.Response.ServiceID).IsEqualTo((byte)1);
        await Assert.That(Convert.ToHexString(result.Response.Data!)).IsEqualTo("1234");
        await Assert.That(channel.SendCount).IsEqualTo(1);
        await Assert.That(channel.ReceiveCount).IsEqualTo(1);
    }

    /// <summary>Verifies base channel processing reinitializes the client before retry attempts.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BaseChannel_ProcessRequestRetriesAfterTransientSendFailure()
    {
        using var plc = CreateConnection();
        using var channel = new TestChannel { ResponseData = [0x12, 0x34], FailFirstSend = true };
        var request = ReadMemoryAreaWordRequest.CreateNew(plc, 100, 1, MemoryWordDataType.DataMemory);

        var result = await channel.ProcessRequestAsync(request, 100, 1, CancellationToken.None);

        await Assert.That(Convert.ToHexString(result.Response.Data!)).IsEqualTo("1234");
        await Assert.That(channel.SendCount).IsEqualTo(2);
        await Assert.That(channel.DestroyCount).IsEqualTo(1);
    }

    /// <summary>Verifies base channel processing purges stale responses when service identifiers mismatch.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task BaseChannel_ProcessRequestPurgesServiceIdMismatches()
    {
        using var plc = CreateConnection();
        using var channel = new TestChannel { ResponseData = [0x12, 0x34], ForceServiceIdMismatch = true, ThrowDuringPurge = true };
        var request = ReadMemoryAreaWordRequest.CreateNew(plc, 100, 1, MemoryWordDataType.DataMemory);

        var ex = await CaptureExceptionAsync<OmronPLCException>(() => channel.ProcessRequestAsync(request, 100, 0, CancellationToken.None));

        await Assert.That(ex.Message).Contains("FINS Error Response");
        await Assert.That(channel.PurgeCount).IsEqualTo(1);
    }

    /// <summary>Verifies TCP client loopback send, receive, properties and endpoint helpers.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task TcpClient_ConnectsSendsReceivesAndExposesSocketState()
    {
        var listener = new NetTcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = EchoTcpAsync(listener);
            using var client = new CoreTcpClient("127.0.0.1", port);

            client.NoDelay = true;
            client.LingerState = new(true, 0);
            client.KeepAliveEnabled = true;
            await client.ConnectAsync(1000, CancellationToken.None);
            var bytesSent = await client.SendAsync([1, 2, 3], 1000, CancellationToken.None);
            var receiveBuffer = new byte[2];
            var bytesReceived = await client.ReceiveAsync(receiveBuffer, 1000, CancellationToken.None);
            var serverReceived = await serverTask.ConfigureAwait(false);
            var dnsEndpoint = new System.Net.DnsEndPoint("localhost", 9600);

            await Assert.That(bytesSent).IsEqualTo(3);
            await Assert.That(bytesReceived).IsEqualTo(2);
            await Assert.That(Convert.ToHexString(receiveBuffer)).IsEqualTo("0405");
            await Assert.That(serverReceived).IsEqualTo(3);
            await Assert.That(client.Connected).IsTrue();
            await Assert.That(client.Socket).IsNotNull();
            await Assert.That(client.NoDelay).IsTrue();
            await Assert.That(client.LingerState!.Enabled).IsTrue();
            await Assert.That(client.KeepAliveEnabled).IsTrue();
            await Assert.That(TcpSocketConfiguration.GetRemoteHostAndPort(dnsEndpoint)).IsEqualTo(("localhost", 9600));
            await Assert.That(TcpSocketConfiguration.GetRemoteHostAndPort(null)).IsEqualTo((string.Empty, 0));

            client.Dispose();

            await Assert.That(client.Connected).IsFalse();
            await Assert.That(client.Socket).IsNull();
            await Assert.That(client.NoDelay).IsFalse();
            await Assert.That(client.LingerState).IsNull();
            await Assert.That(client.KeepAliveEnabled).IsFalse();
            var disposedConnect = await CaptureExceptionAsync<ObjectDisposedException>(() => client.ConnectAsync(1, CancellationToken.None));
            await Assert.That(disposedConnect).IsNotNull();
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Verifies UDP client loopback send, receive, properties and disposed guards.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task UdpClient_SendsReceivesAndExposesSocketState()
    {
        using var server = new NetUdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var endpoint = (System.Net.IPEndPoint)server.Client.LocalEndPoint!;
        var serverTask = EchoUdpAsync(server);
        using var client = new CoreUdpClient(System.Net.IPAddress.Loopback, endpoint.Port);

        var bytesSent = await client.SendAsync([1, 2, 3], 1000, CancellationToken.None);
        var receiveBuffer = new byte[2];
        var bytesReceived = await client.ReceiveAsync(receiveBuffer, 1000, CancellationToken.None);
        var serverReceived = await serverTask.ConfigureAwait(false);

        await Assert.That(bytesSent).IsEqualTo(3);
        await Assert.That(bytesReceived).IsEqualTo(2);
        await Assert.That(Convert.ToHexString(receiveBuffer)).IsEqualTo("0708");
        await Assert.That(serverReceived).IsEqualTo(3);
        await Assert.That(client.Socket).IsNotNull();

        client.Dispose();

        await Assert.That(client.Available).IsEqualTo(0);
        await Assert.That(client.Socket).IsNull();
        var disposedSend = await CaptureExceptionAsync<ObjectDisposedException>(() => client.SendAsync([1], 1, CancellationToken.None));
        await Assert.That(disposedSend).IsNotNull();
    }

    /// <summary>Verifies socket wrapper constructors validate their inputs.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SocketClients_ValidateConstructorInputs()
    {
        await Assert.That(CaptureException<ArgumentNullException>(() => _ = CreateTcpClient((string)null!, 9600))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => _ = CreateTcpClient("127.0.0.1", -1))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => _ = CreateTcpClient((System.Net.IPAddress)null!, 9600))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => _ = CreateTcpClient(System.Net.IPAddress.Loopback, 65_536))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => _ = CreateUdpClient((string)null!, 9600))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => _ = CreateUdpClient("127.0.0.1", -1))).IsNotNull();
        await Assert.That(CaptureException<ArgumentNullException>(() => _ = CreateUdpClient((System.Net.IPAddress)null!, 9600))).IsNotNull();
        await Assert.That(CaptureException<ArgumentOutOfRangeException>(() => _ = CreateUdpClient(System.Net.IPAddress.Loopback, 65_536))).IsNotNull();
    }

    /// <summary>Verifies socket cleanup helpers observe canceled work without surfacing expected cancellation exceptions.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task SocketOperationCleanup_ObservesCancellationExceptions()
    {
        using var delayCts = new CancellationTokenSource();
        var delayTask = Task.Delay(TimeSpan.FromMinutes(1), delayCts.Token);

        await SocketOperationCleanup.CancelDelayAsync(delayCts, delayTask);

        using var operationCts = new CancellationTokenSource();
        var operationTask = Task.Delay(TimeSpan.FromMinutes(1), operationCts.Token);

        await SocketOperationCleanup.CancelSocketOperationAsync(operationCts, operationTask);
    }

    /// <summary>Verifies injected PLC connections initialize by reading controller information.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task OmronPlcConnection_InitializeReadsControllerInformation()
    {
        using var channel = new TestChannel { ResponseData = BuildCpuUnitData("CS1G-CPU42H", "1.23") };
        using var plc = CreateInjectedConnection(channel, isInitialized: false);

        await plc.InitializeAsync(CancellationToken.None);

        await Assert.That(plc.IsInitialized).IsTrue();
        await Assert.That(plc.ControllerModel).IsEqualTo("CS1G-CPU42H");
        await Assert.That(plc.ControllerVersion).IsEqualTo("1.23");
        await Assert.That(plc.PLCType).IsEqualTo(PLCType.C_Series);
        await Assert.That(channel.InitializeCount).IsEqualTo(1);
    }

    /// <summary>Verifies injected PLC connections execute read and write operations through the channel.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task OmronPlcConnection_ReadsAndWritesThroughInjectedChannel()
    {
        using var readWordsChannel = new TestChannel { ResponseData = [0x12, 0x34, 0xFF, 0xFE] };
        using var readWordsPlc = CreateInjectedConnection(readWordsChannel);
        var words = await readWordsPlc.ReadWordsAsync(100, 2, MemoryWordDataType.DataMemory, CancellationToken.None);

        using var readBitsChannel = new TestChannel { ResponseData = [1, 0] };
        using var readBitsPlc = CreateInjectedConnection(readBitsChannel);
        var bits = await readBitsPlc.ReadBitsAsync(100, 0, 2, MemoryBitDataType.CommonIO, CancellationToken.None);

        using var clockChannel = new TestChannel { ResponseData = [0x26, 0x06, 0x30, 0x14, 0x25, 0x59, 0x02] };
        using var clockPlc = CreateInjectedConnection(clockChannel);
        var clock = await clockPlc.ReadClockAsync(CancellationToken.None);

        using var cycleChannel = new TestChannel { ResponseData = [0x00, 0x00, 0x01, 0x23, 0x00, 0x00, 0x04, 0x56, 0x00, 0x00, 0x00, 0x00] };
        using var cyclePlc = CreateInjectedConnection(cycleChannel);
        var cycle = await cyclePlc.ReadCycleTimeAsync(CancellationToken.None);

        using var writeWordsChannel = new TestChannel();
        using var writeWordsPlc = CreateInjectedConnection(writeWordsChannel);
        var writeWords = await writeWordsPlc.WriteWordsAsync([1, 2], 100, MemoryWordDataType.DataMemory, CancellationToken.None);

        using var writeBitsChannel = new TestChannel();
        using var writeBitsPlc = CreateInjectedConnection(writeBitsChannel);
        var writeBits = await writeBitsPlc.WriteBitsAsync([true, false], 100, 0, MemoryBitDataType.CommonIO, CancellationToken.None);

        using var writeClockChannel = new TestChannel();
        using var writeClockPlc = CreateInjectedConnection(writeClockChannel);
        var writeClock = await writeClockPlc.WriteClockAsync(new(2026, 6, 30, 14, 25, 59), 2, CancellationToken.None);

        await Assert.That(Convert.ToHexString(ToBigEndianBytes(words.Values))).IsEqualTo("1234FFFE");
        await Assert.That(ToBitText(bits.Values)).IsEqualTo("1,0");
        await Assert.That(clock.Clock).IsEqualTo(new DateTime(2026, 6, 30, 14, 25, 59));
        await Assert.That(clock.DayOfWeek).IsEqualTo(2);
        await Assert.That(cycle.AverageCycleTime).IsEqualTo(12.3d);
        await Assert.That(cycle.MaximumCycleTime).IsEqualTo(45.6d);
        await Assert.That(cycle.MinimumCycleTime).IsEqualTo(0d);
        await Assert.That(writeWords.BytesSent > 0).IsTrue();
        await Assert.That(writeBits.BytesSent > 0).IsTrue();
        await Assert.That(writeClock.BytesSent > 0).IsTrue();
    }

    /// <summary>Verifies PLC connection validation rejects invalid operation inputs.</summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Test]
    public async Task OmronPlcConnection_RejectsInvalidOperationInputs()
    {
        using var channel = new TestChannel();
        using var plc = CreateInjectedConnection(channel);
        using var uninitialized = CreateInjectedConnection(new TestChannel(), isInitialized: false);
        using var seriesPlc = CreateInjectedConnection(new TestChannel(), plcType: PLCType.NX102);

        await AssertThrowsAsync<OmronPLCException>(() => uninitialized.ReadWordsAsync(100, 1, MemoryWordDataType.DataMemory, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadBitsAsync(100, 16, 1, MemoryBitDataType.CommonIO, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadBitsAsync(100, 0, 0, MemoryBitDataType.CommonIO, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadBitsAsync(100, 15, 2, MemoryBitDataType.CommonIO, CancellationToken.None));
        await AssertThrowsAsync<ArgumentException>(() => plc.ReadBitsAsync(100, 0, 1, MemoryBitDataType.None, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadBitsAsync(32_768, 0, 1, MemoryBitDataType.DataMemory, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadWordsAsync(100, 0, MemoryWordDataType.DataMemory, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadWordsAsync(100, 1000, MemoryWordDataType.DataMemory, CancellationToken.None));
        await AssertThrowsAsync<ArgumentException>(() => plc.ReadWordsAsync(100, 1, MemoryWordDataType.None, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadWordsAsync(32_768, 1, MemoryWordDataType.DataMemory, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(() => plc.WriteBitsAsync(null!, 100, 0, MemoryBitDataType.CommonIO, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.WriteBitsAsync([], 100, 0, MemoryBitDataType.CommonIO, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.WriteBitsAsync([true, false], 100, 15, MemoryBitDataType.CommonIO, CancellationToken.None));
        await AssertThrowsAsync<ArgumentNullException>(() => plc.WriteWordsAsync(null!, 100, MemoryWordDataType.DataMemory, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.WriteWordsAsync([], 100, MemoryWordDataType.DataMemory, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.WriteWordsAsync(new short[997], 100, MemoryWordDataType.DataMemory, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.WriteClockAsync(new(1997, 12, 31), CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.WriteClockAsync(new(2070, 1, 1), CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.WriteClockAsync(new(2026, 6, 30), -1, CancellationToken.None));
        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => plc.WriteClockAsync(new(2026, 6, 30), 7, CancellationToken.None));
        await AssertThrowsAsync<OmronPLCException>(() => seriesPlc.ReadCycleTimeAsync(CancellationToken.None));
    }

    /// <summary>Creates a connection instance without opening a channel.</summary>
    /// <returns>The connection instance.</returns>
    private static OmronPLCConnection CreateConnection() => new(1, 2, ConnectionMethod.UDP, "127.0.0.1", retries: 0);

    /// <summary>Creates an injected PLC connection for unit tests.</summary>
    /// <param name="channel">The test channel.</param>
    /// <param name="plcType">The PLC type.</param>
    /// <param name="isInitialized">A value indicating whether the connection starts initialized.</param>
    /// <returns>The injected connection.</returns>
    private static OmronPLCConnection CreateInjectedConnection(TestChannel channel, PLCType plcType = PLCType.CJ2, bool isInitialized = true) =>
        new(1, 2, ConnectionMethod.UDP, "127.0.0.1", channel, timeout: 100, retries: 0, plcType: plcType, controllerModel: "CJ2M", controllerVersion: "1.0", isInitialized: isInitialized);

    /// <summary>Creates a TCP client for constructor validation tests.</summary>
    /// <param name="host">The remote host.</param>
    /// <param name="port">The remote port.</param>
    /// <returns>The TCP client.</returns>
    private static CoreTcpClient CreateTcpClient(string host, int port) => new(host, port);

    /// <summary>Creates a TCP client for constructor validation tests.</summary>
    /// <param name="address">The remote address.</param>
    /// <param name="port">The remote port.</param>
    /// <returns>The TCP client.</returns>
    private static CoreTcpClient CreateTcpClient(System.Net.IPAddress address, int port) => new(address, port);

    /// <summary>Creates a UDP client for constructor validation tests.</summary>
    /// <param name="host">The remote host.</param>
    /// <param name="port">The remote port.</param>
    /// <returns>The UDP client.</returns>
    private static CoreUdpClient CreateUdpClient(string host, int port) => new(host, port);

    /// <summary>Creates a UDP client for constructor validation tests.</summary>
    /// <param name="address">The remote address.</param>
    /// <param name="port">The remote port.</param>
    /// <returns>The UDP client.</returns>
    private static CoreUdpClient CreateUdpClient(System.Net.IPAddress address, int port) => new(address, port);

    /// <summary>Echoes a TCP message using the core TCP socket wrapper.</summary>
    /// <param name="listener">The TCP listener.</param>
    /// <returns>The number of bytes received from the client.</returns>
    private static async Task<int> EchoTcpAsync(NetTcpListener listener)
    {
        using var acceptedSocket = await listener.AcceptSocketAsync().ConfigureAwait(false);
        using var server = new CoreTcpClient(acceptedSocket);
        var receiveBuffer = new byte[3];
        var received = await server.ReceiveAsync(receiveBuffer, 1000, CancellationToken.None).ConfigureAwait(false);
        _ = await server.SendAsync([4, 5], 1000, CancellationToken.None).ConfigureAwait(false);
        return received;
    }

    /// <summary>Echoes a UDP message using the framework UDP server.</summary>
    /// <param name="server">The UDP server.</param>
    /// <returns>The number of bytes received from the client.</returns>
    private static async Task<int> EchoUdpAsync(NetUdpClient server)
    {
        var result = await server.ReceiveAsync(CancellationToken.None).ConfigureAwait(false);
        var response = new byte[] { 7, 8 };
        _ = await server.SendAsync(response, response.Length, result.RemoteEndPoint).ConfigureAwait(false);
        return result.Buffer.Length;
    }

    /// <summary>Creates Host Link codec instances for constructor validation tests.</summary>
    /// <param name="options">The serial options.</param>
    /// <returns>The codec.</returns>
    private static HostLinkFinsFrameCodec CreateHostLinkCodec(OmronSerialOptions options) => new(options);

    /// <summary>Creates serial options for constructor validation tests.</summary>
    /// <param name="portName">The port name.</param>
    /// <returns>The serial options.</returns>
    private static OmronSerialOptions CreateSerialOptions(string portName) => new(portName);

    /// <summary>Creates a PLC tag for constructor validation tests.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The tag address.</param>
    /// <returns>The tag.</returns>
    private static PlcTag<T> CreateTag<T>(string tagName, string address) => new(tagName, address);

    /// <summary>Creates a successful FINS response for a request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="data">The response data.</param>
    /// <returns>The parsed response.</returns>
    private static FINSResponse CreateResponse(FINSRequest request, byte[] data) => FINSResponse.CreateNew(BuildResponseFrame(request, data), request);

    /// <summary>Builds a raw FINS response frame.</summary>
    /// <param name="request">The request.</param>
    /// <param name="data">The response data.</param>
    /// <param name="serviceId">The optional service id.</param>
    /// <param name="functionCode">The optional function code.</param>
    /// <param name="subFunctionCode">The optional sub-function code.</param>
    /// <param name="mainResponseCode">The main response code.</param>
    /// <param name="subResponseCode">The sub response code.</param>
    /// <returns>The raw frame.</returns>
    private static byte[] BuildResponseFrame(
        FINSRequest request,
        byte[] data,
        byte? serviceId = null,
        byte? functionCode = null,
        byte? subFunctionCode = null,
        byte mainResponseCode = 0,
        byte subResponseCode = 0)
    {
        var message = new byte[FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength + data.Length];
        message[9] = serviceId ?? request.ServiceID;
        message[10] = functionCode ?? request.FunctionCode;
        message[11] = subFunctionCode ?? request.SubFunctionCode;
        message[12] = mainResponseCode;
        message[13] = subResponseCode;
        Array.Copy(data, 0, message, FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength, data.Length);
        return message;
    }

    /// <summary>Builds CPU unit data with fixed controller model and version fields.</summary>
    /// <param name="model">The controller model.</param>
    /// <param name="version">The controller version.</param>
    /// <returns>The response payload.</returns>
    private static byte[] BuildCpuUnitData(string model, string version)
    {
        var data = new byte[ReadCPUUnitDataResponse.TotalResponseLength];
        CopyAscii(model, data, 0, ReadCPUUnitDataResponse.ControllerModelLength);
        CopyAscii(version, data, ReadCPUUnitDataResponse.ControllerModelLength, ReadCPUUnitDataResponse.ControllerVersionLength);
        return data;
    }

    /// <summary>Copies ASCII text to a fixed-width field.</summary>
    /// <param name="text">The text.</param>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="offset">The destination offset.</param>
    /// <param name="length">The maximum field length.</param>
    private static void CopyAscii(string text, byte[] buffer, int offset, int length)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, length));
    }

    /// <summary>Converts PLC words to big-endian protocol bytes.</summary>
    /// <param name="words">The PLC words.</param>
    /// <returns>The protocol bytes.</returns>
    private static byte[] ToBigEndianBytes(short[] words)
    {
        var bytes = new byte[words.Length * 2];
        for (var i = 0; i < words.Length; i++)
        {
            var word = (ushort)words[i];
            bytes[i * 2] = (byte)(word >> 8);
            bytes[(i * 2) + 1] = (byte)(word & 0xFF);
        }

        return bytes;
    }

    /// <summary>Converts bit values to compact assertion text.</summary>
    /// <param name="values">The bit values.</param>
    /// <returns>The bit text.</returns>
    private static string ToBitText(bool[] values)
    {
        var text = new System.Text.StringBuilder(values.Length + Math.Max(0, values.Length - 1));
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                _ = text.Append(',');
            }

            _ = text.Append(values[i] ? '1' : '0');
        }

        return text.ToString();
    }

    /// <summary>Converts a memory value to uppercase hexadecimal.</summary>
    /// <param name="memory">The memory value.</param>
    /// <returns>The hexadecimal text.</returns>
    private static string ToHex(ReadOnlyMemory<byte> memory) => Convert.ToHexString(memory.ToArray());

    /// <summary>Captures an expected exception from an action.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to invoke.</param>
    /// <returns>The captured exception.</returns>
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

    /// <summary>Captures an expected exception from an asynchronous action.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to invoke.</param>
    /// <returns>The captured exception.</returns>
    private static async Task<TException> CaptureExceptionAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new InvalidOperationException($"Expected exception of type {nameof(TException)}.");
    }

    /// <summary>Asserts that an asynchronous action throws the expected exception type.</summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action to invoke.</param>
    /// <returns>A task that represents the asynchronous assertion.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        var ex = await CaptureExceptionAsync<TException>(action).ConfigureAwait(false);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Fake channel used to exercise base-channel orchestration without sockets.</summary>
    private sealed class TestChannel : BaseChannel
    {
        /// <summary>Stores the last sent request message.</summary>
        private byte[] _lastSent = [];

        /// <summary>Initializes a new instance of the <see cref="TestChannel"/> class.</summary>
        public TestChannel()
            : base("test", 9600)
        {
        }

        /// <summary>Gets or sets the response data to emit.</summary>
        public byte[] ResponseData { get; set; } = [];

        /// <summary>Gets or sets a value indicating whether the first send should fail.</summary>
        public bool FailFirstSend { get; set; }

        /// <summary>Gets or sets a value indicating whether the response service id should mismatch.</summary>
        public bool ForceServiceIdMismatch { get; set; }

        /// <summary>Gets or sets a value indicating whether purge should throw.</summary>
        public bool ThrowDuringPurge { get; set; }

        /// <summary>Gets the initialize call count.</summary>
        public int InitializeCount { get; private set; }

        /// <summary>Gets the destroy call count.</summary>
        public int DestroyCount { get; private set; }

        /// <summary>Gets the send call count.</summary>
        public int SendCount { get; private set; }

        /// <summary>Gets the receive call count.</summary>
        public int ReceiveCount { get; private set; }

        /// <summary>Gets the purge call count.</summary>
        public int PurgeCount { get; private set; }

        /// <inheritdoc />
        internal override Task InitializeAsync(int timeout, CancellationToken cancellationToken)
        {
            InitializeCount++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override Task DestroyAndInitializeClient(int timeout, CancellationToken cancellationToken)
        {
            DestroyCount++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override Task<SendMessageResult> SendMessageAsync(ReadOnlyMemory<byte> message, int timeout, CancellationToken cancellationToken)
        {
            SendCount++;
            if (FailFirstSend && SendCount == 1)
            {
                throw new TimeoutException("first send failed");
            }

            _lastSent = message.ToArray();
            return Task.FromResult(new SendMessageResult
            {
                Bytes = _lastSent.Length,
                Packets = 1,
            });
        }

        /// <inheritdoc />
        protected override Task<ReceiveMessageResult> ReceiveMessageAsync(int timeout, CancellationToken cancellationToken)
        {
            ReceiveCount++;
            var response = BuildResponseFromLastSent();
            return Task.FromResult(new ReceiveMessageResult
            {
                Bytes = response.Length,
                Packets = 1,
                Message = response,
            });
        }

        /// <inheritdoc />
        protected override Task PurgeReceiveBuffer(int timeout, CancellationToken cancellationToken)
        {
            PurgeCount++;
            return ThrowDuringPurge ? throw new TimeoutException("purge failed") : Task.CompletedTask;
        }

        /// <summary>Builds a response frame from the last request message.</summary>
        /// <returns>The response frame.</returns>
        private byte[] BuildResponseFromLastSent()
        {
            var response = new byte[FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength + ResponseData.Length];
            response[9] = (byte)(ForceServiceIdMismatch ? _lastSent[9] + 1 : _lastSent[9]);
            response[10] = _lastSent[10];
            response[11] = _lastSent[11];
            Array.Copy(ResponseData, 0, response, FINSResponse.HeaderLength + FINSResponse.CommandLength + FINSResponse.ResponseCodeLength, ResponseData.Length);
            return response;
        }
    }
}
