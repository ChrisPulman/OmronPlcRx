// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Text;

namespace OmronPlcRx;

/// <summary>
/// Encodes and decodes Omron FINS frames carried in Host Link serial frames.
/// </summary>
public sealed class HostLinkFinsFrameCodec
{
    private const string HeaderCode = "FA";
    private readonly OmronSerialOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostLinkFinsFrameCodec"/> class.
    /// </summary>
    /// <param name="options">Serial Host Link options.</param>
    public HostLinkFinsFrameCodec(OmronSerialOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    /// <summary>
    /// Calculates the Host Link frame-check sequence.
    /// </summary>
    /// <param name="frameText">Frame text from @ through the final text character, excluding FCS and terminator.</param>
    /// <returns>Two-character uppercase hexadecimal FCS.</returns>
    public static string CalculateFcs(string frameText)
    {
        if (frameText == null)
        {
            throw new ArgumentNullException(nameof(frameText));
        }

        byte value = 0;
        foreach (var ch in Encoding.ASCII.GetBytes(frameText))
        {
            value ^= ch;
        }

        return value.ToString("X2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Encodes a binary FINS request into an ASCII Host Link FINS frame.
    /// </summary>
    /// <param name="finsMessage">Binary FINS request message.</param>
    /// <returns>ASCII Host Link FINS frame including FCS and terminator.</returns>
    public string EncodeRequest(ReadOnlyMemory<byte> finsMessage)
    {
        if (finsMessage.Length < 12)
        {
            throw new ArgumentException("The FINS request is too short.", nameof(finsMessage));
        }

        var fins = finsMessage.ToArray();
        var body = new StringBuilder()
            .Append('@')
            .Append(_options.HostLinkUnitNumber.ToString("D2", CultureInfo.InvariantCulture))
            .Append(HeaderCode)
            .Append(_options.ResponseWaitTime.ToString("X1", CultureInfo.InvariantCulture));

        if (_options.FrameMode == OmronHostLinkFinsFrameMode.Direct)
        {
            body.Append("00"); // ICF: directly connected CPU Unit.
            body.Append(fins[5].ToString("X2", CultureInfo.InvariantCulture)); // DA2.
            body.Append(fins[8].ToString("X2", CultureInfo.InvariantCulture)); // SA2.
            body.Append(fins[9].ToString("X2", CultureInfo.InvariantCulture)); // SID.
            body.Append(ToHex(fins, 10, fins.Length - 10)); // Command code + text.
        }
        else
        {
            body.Append(ToHex(fins, 0, fins.Length));
        }

        var bodyText = body.ToString();
        return bodyText + CalculateFcs(bodyText) + "*\r";
    }

    /// <summary>
    /// Decodes an ASCII Host Link FINS response into a binary FINS response message.
    /// </summary>
    /// <param name="frame">ASCII Host Link FINS response frame including FCS and terminator.</param>
    /// <returns>Binary FINS response message.</returns>
    public Memory<byte> DecodeResponse(string frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (!frame.EndsWith("*\r", StringComparison.Ordinal))
        {
            throw new OmronPLCException("The Host Link FINS response terminator was invalid.");
        }

        if (frame.Length < 10)
        {
            throw new OmronPLCException("The Host Link FINS response was too short.");
        }

        var withoutTerminator = frame.Substring(0, frame.Length - 2);
        var body = withoutTerminator.Substring(0, withoutTerminator.Length - 2);
        var receivedFcs = withoutTerminator.Substring(withoutTerminator.Length - 2, 2);
        var expectedFcs = CalculateFcs(body);
        if (!string.Equals(receivedFcs, expectedFcs, StringComparison.OrdinalIgnoreCase))
        {
            throw new OmronPLCException($"The Host Link FINS response FCS was invalid. Expected '{expectedFcs}', received '{receivedFcs}'.");
        }

        if (body[0] != '@')
        {
            throw new OmronPLCException("The Host Link FINS response did not start with '@'.");
        }

        var unit = body.Substring(1, 2);
        var expectedUnit = _options.HostLinkUnitNumber.ToString("D2", CultureInfo.InvariantCulture);
        if (!string.Equals(unit, expectedUnit, StringComparison.Ordinal))
        {
            throw new OmronPLCException($"The Host Link FINS response unit number '{unit}' did not match expected unit '{expectedUnit}'.");
        }

        var headerCode = body.Substring(3, 2);
        if (!string.Equals(headerCode, HeaderCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new OmronPLCException($"The Host Link FINS response header code '{headerCode}' was invalid.");
        }

        var payloadStart = 5;
        var hostLinkEndCode = body.Substring(payloadStart, 2);
        if (!string.Equals(hostLinkEndCode, "00", StringComparison.OrdinalIgnoreCase))
        {
            throw new OmronPLCException($"The Host Link FINS response end code was not normal completion: '{hostLinkEndCode}'.");
        }

        var payload = body.Substring(payloadStart + 2);
        return _options.FrameMode == OmronHostLinkFinsFrameMode.Direct
            ? DecodeDirectResponse(payload)
            : DecodeNetworkResponse(payload);
    }

    private static Memory<byte> DecodeNetworkResponse(string payload) => FromHex(payload);

    private static Memory<byte> DecodeDirectResponse(string payload)
    {
        if (payload.Length < 16)
        {
            throw new OmronPLCException("The direct Host Link FINS response payload was too short.");
        }

        var icf = ParseByte(payload, 0);
        var da2 = ParseByte(payload, 2);
        var sa2 = ParseByte(payload, 4);
        var sid = ParseByte(payload, 6);
        var commandAndData = FromHex(payload.Substring(8)).ToArray();
        var message = new byte[10 + commandAndData.Length];
        message[0] = icf;
        message[1] = 0x00;
        message[2] = 0x02;
        message[3] = 0x00;
        message[4] = 0x00;
        message[5] = da2;
        message[6] = 0x00;
        message[7] = 0x00;
        message[8] = sa2;
        message[9] = sid;
        Array.Copy(commandAndData, 0, message, 10, commandAndData.Length);
        return message;
    }

    private static string ToHex(byte[] bytes, int offset, int count)
    {
        var builder = new StringBuilder(count * 2);
        for (var i = offset; i < offset + count; i++)
        {
            builder.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static Memory<byte> FromHex(string value)
    {
        if (value.Length % 2 != 0)
        {
            throw new OmronPLCException("The Host Link FINS hexadecimal payload length was invalid.");
        }

        var bytes = new byte[value.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = ParseByte(value, i * 2);
        }

        return bytes;
    }

    private static byte ParseByte(string value, int startIndex) => byte.Parse(value.Substring(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
