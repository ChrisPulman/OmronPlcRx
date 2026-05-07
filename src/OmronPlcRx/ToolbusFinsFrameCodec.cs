// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace OmronPlcRx;

/// <summary>
/// Encodes and decodes Omron Toolbus serial frames carrying binary FINS messages.
/// </summary>
public static class ToolbusFinsFrameCodec
{
    private const int MinimumFinsRequestLength = 12;
    private const int MinimumFinsResponseLength = 14;

    /// <summary>
    /// Gets the Toolbus synchronization frame exchanged before normal 0xAB frames.
    /// </summary>
    public static ReadOnlyMemory<byte> SynchronizationFrame => new byte[] { 0xAC, 0x01 };

    /// <summary>
    /// Encodes a binary FINS message into a Toolbus frame.
    /// </summary>
    /// <param name="finsMessage">Binary FINS request message.</param>
    /// <returns>Toolbus frame: 0xAB + length + FINS payload + checksum.</returns>
    public static Memory<byte> EncodeRequest(ReadOnlyMemory<byte> finsMessage)
    {
        if (finsMessage.Length < MinimumFinsRequestLength)
        {
            throw new ArgumentException("The FINS request is too short.", nameof(finsMessage));
        }

        if (finsMessage.Length > ushort.MaxValue - 2)
        {
            throw new ArgumentOutOfRangeException(nameof(finsMessage), "The FINS message is too long for a Toolbus frame.");
        }

        var frameLength = finsMessage.Length + 2;
        var frame = new byte[3 + frameLength];
        frame[0] = 0xAB;
        frame[1] = (byte)((frameLength >> 8) & 0xFF);
        frame[2] = (byte)(frameLength & 0xFF);
        finsMessage.CopyTo(frame.AsMemory(3));
        var checksum = CalculateChecksum(frame.AsSpan(0, frame.Length - 2));
        frame[frame.Length - 2] = (byte)((checksum >> 8) & 0xFF);
        frame[frame.Length - 1] = (byte)(checksum & 0xFF);
        return frame;
    }

    /// <summary>
    /// Decodes a Toolbus response frame into the contained binary FINS response message.
    /// </summary>
    /// <param name="frame">Complete Toolbus frame.</param>
    /// <returns>Binary FINS response message.</returns>
    public static Memory<byte> DecodeResponse(ReadOnlyMemory<byte> frame)
    {
        if (frame.Length < 5)
        {
            throw new OmronPLCException("The Toolbus response frame was too short.");
        }

        var span = frame.Span;
        if (span[0] != 0xAB)
        {
            throw new OmronPLCException("The Toolbus response frame did not start with 0xAB.");
        }

        var declaredLength = (span[1] << 8) | span[2];
        if (declaredLength < 2)
        {
            throw new OmronPLCException("The Toolbus response frame length was invalid.");
        }

        if (frame.Length != declaredLength + 3)
        {
            throw new OmronPLCException($"The Toolbus response frame length {frame.Length} did not match declared length {declaredLength}.");
        }

        var expectedChecksum = CalculateChecksum(span[..^2]);
        var receivedChecksum = (ushort)((span[^2] << 8) | span[^1]);
        if (expectedChecksum != receivedChecksum)
        {
            throw new OmronPLCException($"The Toolbus response checksum was invalid. Expected 0x{expectedChecksum:X4}, received 0x{receivedChecksum:X4}.");
        }

        var payloadLength = declaredLength - 2;
        if (payloadLength < MinimumFinsResponseLength)
        {
            throw new OmronPLCException("The Toolbus response FINS payload was too short.");
        }

        if (span[3] != 0xC0 && span[3] != 0xC1)
        {
            throw new OmronPLCException("The Toolbus response FINS header was invalid.");
        }

        var payload = new byte[payloadLength];
        span.Slice(3, payloadLength).CopyTo(payload);
        return payload;
    }

    /// <summary>
    /// Calculates the Toolbus additive checksum over the supplied bytes.
    /// </summary>
    /// <param name="data">Frame bytes excluding the checksum tail.</param>
    /// <returns>16-bit additive checksum.</returns>
    public static ushort CalculateChecksum(ReadOnlySpan<byte> data)
    {
        uint checksum = 0;
        foreach (var value in data)
        {
            checksum += value;
        }

        return (ushort)(checksum & 0xFFFF);
    }
}
