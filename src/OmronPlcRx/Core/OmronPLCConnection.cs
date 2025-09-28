// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using OmronPlcRx.Core.Channels;
using OmronPlcRx.Core.Requests;
using OmronPlcRx.Core.Responses;
using OmronPlcRx.Enums;
using OmronPlcRx.Results;

namespace OmronPlcRx.Core;

/// <summary>
/// High-level Omron PLC client facilitating initialization and FINS read/write operations over TCP/UDP.
/// </summary>
internal class OmronPLCConnection : IDisposable
{
    private readonly object _isInitializedLock = new();
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmronPLCConnection"/> class.
    /// </summary>
    /// <param name="localNodeId">Local FINS node identifier (1-254).</param>
    /// <param name="remoteNodeId">Remote PLC FINS node identifier (1-254, not equal to local).</param>
    /// <param name="connectionMethod">Transport to use (TCP/UDP).</param>
    /// <param name="remoteHost">PLC hostname or IP address.</param>
    /// <param name="port">PLC service port.</param>
    /// <param name="timeout">Timeout in milliseconds for requests.</param>
    /// <param name="retries">Number of retries for transient failures.</param>
    public OmronPLCConnection(byte localNodeId, byte remoteNodeId, ConnectionMethod connectionMethod, string remoteHost, int port = 9600, int timeout = 2000, int retries = 1)
    {
        if (localNodeId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(localNodeId), "The Local Node ID cannot be set to 0");
        }

        if (localNodeId == 255)
        {
            throw new ArgumentOutOfRangeException(nameof(localNodeId), "The Local Node ID cannot be set to 255");
        }

        LocalNodeID = localNodeId;

        if (remoteNodeId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remoteNodeId), "The Remote Node ID cannot be set to 0");
        }

        if (remoteNodeId == 255)
        {
            throw new ArgumentOutOfRangeException(nameof(remoteNodeId), "The Remote Node ID cannot be set to 255");
        }

        if (remoteNodeId == localNodeId)
        {
            throw new ArgumentException("The Remote Node ID cannot be the same as the Local Node ID", nameof(remoteNodeId));
        }

        RemoteNodeID = remoteNodeId;

        ConnectionMethod = connectionMethod;

        if (remoteHost == null)
        {
            throw new ArgumentNullException(nameof(remoteHost), "The Remote Host cannot be Null");
        }

        if (remoteHost.Length == 0)
        {
            throw new ArgumentException("The Remote Host cannot be Empty", nameof(remoteHost));
        }

        RemoteHost = remoteHost;

        if (port <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "The Port cannot be less than 1");
        }

        Port = port;

        if (timeout <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The Timeout Value cannot be less than 1");
        }

        Timeout = timeout;

        if (retries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retries), "The Retries Value cannot be Negative");
        }

        Retries = retries;

        Channel = ConnectionMethod == ConnectionMethod.UDP ? new UDPChannel(RemoteHost, Port) : new TCPChannel(RemoteHost, Port);
    }

    /// <summary>
    /// Gets the local FINS node ID used by the client.
    /// </summary>
    public byte LocalNodeID { get; }

    /// <summary>
    /// Gets the remote FINS node ID of the PLC.
    /// </summary>
    public byte RemoteNodeID { get; }

    /// <summary>
    /// Gets the transport connection method (TCP/UDP).
    /// </summary>
    public ConnectionMethod ConnectionMethod { get; }

    /// <summary>
    /// Gets the PLC hostname or IP address.
    /// </summary>
    public string RemoteHost { get; }

    /// <summary>
    /// Gets the PLC service port.
    /// </summary>
    public int Port { get; } = 9600;

    /// <summary>
    /// Gets or sets the request timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; }

    /// <summary>
    /// Gets or sets the number of retries for transient failures.
    /// </summary>
    public int Retries { get; set; }

    /// <summary>
    /// Gets the detected PLC type.
    /// </summary>
    public PLCType PLCType { get; private set; } = PLCType.Unknown;

    /// <summary>
    /// Gets a value indicating whether the PLC client is initialized.
    /// </summary>
    public bool IsInitialized
    {
        get
        {
            lock (_isInitializedLock)
            {
                return _isInitialized;
            }
        }
    }

    /// <summary>
    /// Gets the PLC controller model string.
    /// </summary>
    public string? ControllerModel { get; private set; }

    /// <summary>
    /// Gets the PLC controller version string.
    /// </summary>
    public string? ControllerVersion { get; private set; }

    /// <summary>
    /// Gets the maximum number of words that can be read in a single request for the detected PLC type.
    /// </summary>
    public ushort MaximumReadWordLength => PLCType == PLCType.CP1 ? (ushort)499 : (ushort)999;

    /// <summary>
    /// Gets the maximum number of words that can be written in a single request for the detected PLC type.
    /// </summary>
    public ushort MaximumWriteWordLength => PLCType == PLCType.CP1 ? (ushort)496 : (ushort)996;

    internal BaseChannel Channel { get; }

    internal bool IsNSeries => PLCType switch
    {
        PLCType.NJ101 => true,
        PLCType.NJ301 => true,
        PLCType.NJ501 => true,
        PLCType.NX1P2 => true,
        PLCType.NX102 => true,
        PLCType.NX701 => true,
        PLCType.NY512 => true,
        PLCType.NY532 => true,
        PLCType.NJ_NX_NY_Series => true,
        _ => false,
    };

    internal bool IsCSeries => PLCType switch
    {
        PLCType.CP1 => true,
        PLCType.CJ2 => true,
        PLCType.C_Series => true,
        _ => false,
    };

    /// <summary>
    /// Initializes the communication channel and queries controller information.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        lock (_isInitializedLock)
        {
            if (_isInitialized)
            {
                return;
            }
        }

        // Initialize the Channel
        try
        {
            await Channel.InitializeAsync(Timeout, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            throw new OmronPLCException("Failed to Create the Ethernet UDP Communication Channel for Omron PLC '" + RemoteHost + ":" + Port + "' - The underlying Socket Connection has been Closed");
        }
        catch (TimeoutException)
        {
            throw new OmronPLCException("Failed to Create the Ethernet UDP Communication Channel within the Timeout Period for Omron PLC '" + RemoteHost + ":" + Port + "'");
        }
        catch (System.Net.Sockets.SocketException e)
        {
            throw new OmronPLCException("Failed to Create the Ethernet UDP Communication Channel for Omron PLC '" + RemoteHost + ":" + Port + "'", e);
        }

        await RequestControllerInformation(cancellationToken);

        lock (_isInitializedLock)
        {
            _isInitialized = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Read a single bit value.
    /// </summary>
    /// <param name="address">The word address containing the target bit.</param>
    /// <param name="bitIndex">The bit index within the word (0-15).</param>
    /// <param name="dataType">The bit memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read bit result.</returns>
    public Task<ReadBitsResult> ReadBitAsync(ushort address, byte bitIndex, MemoryBitDataType dataType, CancellationToken cancellationToken) => ReadBitsAsync(address, bitIndex, 1, dataType, cancellationToken);

    /// <summary>
    /// Read a sequence of bit values.
    /// </summary>
    /// <param name="address">The word address containing the first bit.</param>
    /// <param name="startBitIndex">The starting bit index within the word (0-15).</param>
    /// <param name="length">Number of bits to read (1-16, not crossing word boundary).</param>
    /// <param name="dataType">The bit memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read bits result including values and transmission metrics.</returns>
    public async Task<ReadBitsResult> ReadBitsAsync(ushort address, byte startBitIndex, byte length, MemoryBitDataType dataType, CancellationToken cancellationToken)
    {
        lock (_isInitializedLock)
        {
            if (!_isInitialized)
            {
                throw new OmronPLCException("This Omron PLC must be Initialized first before any Requests can be Processed");
            }
        }

        if (startBitIndex > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(startBitIndex), "The Start Bit Index cannot be greater than 15");
        }

        if (length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The Length cannot be Zero");
        }

        if (startBitIndex + length > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The Start Bit Index and Length combined are greater than the Maximum Allowed of 16 Bits");
        }

        if (!ValidateBitDataType(dataType))
        {
            throw new ArgumentException("The Data Type '" + Enum.GetName(typeof(MemoryBitDataType), dataType) + "' is not Supported on this PLC", nameof(dataType));
        }

        if (!ValidateBitAddress(address, dataType))
        {
            throw new ArgumentOutOfRangeException(nameof(address), "The Address is greater than the Maximum Address for the '" + Enum.GetName(typeof(MemoryBitDataType), dataType) + "' Data Type");
        }

        var request = ReadMemoryAreaBitRequest.CreateNew(this, address, startBitIndex, length, dataType);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        return new ReadBitsResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
            Values = ReadMemoryAreaBitResponse.ExtractValues(request, requestResult.Response),
        };
    }

    /// <summary>
    /// Read a single word value.
    /// </summary>
    /// <param name="address">The starting address to read.</param>
    /// <param name="dataType">The word memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read words result including values and transmission metrics.</returns>
    public Task<ReadWordsResult> ReadWordAsync(ushort address, MemoryWordDataType dataType, CancellationToken cancellationToken) => ReadWordsAsync(address, 1, dataType, cancellationToken);

    /// <summary>
    /// Read a sequence of word values.
    /// </summary>
    /// <param name="startAddress">The starting address to read.</param>
    /// <param name="length">Number of words to read.</param>
    /// <param name="dataType">The word memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read words result including values and transmission metrics.</returns>
    public async Task<ReadWordsResult> ReadWordsAsync(ushort startAddress, ushort length, MemoryWordDataType dataType, CancellationToken cancellationToken)
    {
        lock (_isInitializedLock)
        {
            if (!_isInitialized)
            {
                throw new OmronPLCException("This Omron PLC must be Initialized first before any Requests can be Processed");
            }
        }

        if (length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The Length cannot be Zero");
        }

        if (length > MaximumReadWordLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The Length cannot be greater than " + MaximumReadWordLength.ToString());
        }

        if (!ValidateWordDataType(dataType))
        {
            throw new ArgumentException("The Data Type '" + Enum.GetName(typeof(MemoryWordDataType), dataType) + "' is not Supported on this PLC", nameof(dataType));
        }

        if (!ValidateWordStartAddress(startAddress, length, dataType))
        {
            throw new ArgumentOutOfRangeException(nameof(startAddress), "The Start Address and Length combined are greater than the Maximum Address for the '" + Enum.GetName(typeof(MemoryWordDataType), dataType) + "' Data Type");
        }

        var request = ReadMemoryAreaWordRequest.CreateNew(this, startAddress, length, dataType);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        return new ReadWordsResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
            Values = ReadMemoryAreaWordResponse.ExtractValues(request, requestResult.Response),
        };
    }

    /// <summary>
    /// Write a single bit value.
    /// </summary>
    /// <param name="value">The bit value to write.</param>
    /// <param name="address">The word address containing the target bit.</param>
    /// <param name="bitIndex">The bit index within the word (0-15).</param>
    /// <param name="dataType">The bit memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write bits result containing transmission metrics.</returns>
    public Task<WriteBitsResult> WriteBitAsync(bool value, ushort address, byte bitIndex, MemoryBitDataType dataType, CancellationToken cancellationToken) => WriteBitsAsync(new bool[] { value }, address, bitIndex, dataType, cancellationToken);

    /// <summary>
    /// Write a sequence of bit values.
    /// </summary>
    /// <param name="values">The bit values to write.</param>
    /// <param name="address">The word address containing the first bit.</param>
    /// <param name="startBitIndex">The starting bit index within the word (0-15).</param>
    /// <param name="dataType">The bit memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write bits result containing transmission metrics.</returns>
    public async Task<WriteBitsResult> WriteBitsAsync(bool[] values, ushort address, byte startBitIndex, MemoryBitDataType dataType, CancellationToken cancellationToken)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        lock (_isInitializedLock)
        {
            if (!_isInitialized)
            {
                throw new OmronPLCException("This Omron PLC must be Initialized first before any Requests can be Processed");
            }
        }

        if (startBitIndex > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(startBitIndex), "The Start Bit Index cannot be greater than 15");
        }

        if (values.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(values), "The Values Array cannot be Empty");
        }

        if (startBitIndex + values.Length > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(values), "The Values Array Length was greater than the Maximum Allowed of 16 Bits");
        }

        if (!ValidateBitDataType(dataType))
        {
            throw new ArgumentException("The Data Type '" + Enum.GetName(typeof(MemoryBitDataType), dataType) + "' is not Supported on this PLC", nameof(dataType));
        }

        if (!ValidateBitAddress(address, dataType))
        {
            throw new ArgumentOutOfRangeException(nameof(address), "The Address is greater than the Maximum Address for the '" + Enum.GetName(typeof(MemoryBitDataType), dataType) + "' Data Type");
        }

        var request = WriteMemoryAreaBitRequest.CreateNew(this, address, startBitIndex, dataType, values);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        WriteMemoryAreaBitResponse.Validate(request, requestResult.Response);

        return new WriteBitsResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
        };
    }

    /// <summary>
    /// Write a single word value.
    /// </summary>
    /// <param name="value">The word value to write.</param>
    /// <param name="address">The starting address to write.</param>
    /// <param name="dataType">The word memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write words result containing transmission metrics.</returns>
    public Task<WriteWordsResult> WriteWordAsync(short value, ushort address, MemoryWordDataType dataType, CancellationToken cancellationToken) => WriteWordsAsync(new short[] { value }, address, dataType, cancellationToken);

    /// <summary>
    /// Write a sequence of word values.
    /// </summary>
    /// <param name="values">The word values to write.</param>
    /// <param name="startAddress">The starting address to write.</param>
    /// <param name="dataType">The word memory area.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write words result containing transmission metrics.</returns>
    public async Task<WriteWordsResult> WriteWordsAsync(short[] values, ushort startAddress, MemoryWordDataType dataType, CancellationToken cancellationToken)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        lock (_isInitializedLock)
        {
            if (!_isInitialized)
            {
                throw new OmronPLCException("This Omron PLC must be Initialized first before any Requests can be Processed");
            }
        }

        if (values.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(values), "The Values Array cannot be Empty");
        }

        if (values.Length > MaximumWriteWordLength)
        {
            throw new ArgumentOutOfRangeException(nameof(values), "The Values Array Length cannot be greater than " + MaximumWriteWordLength.ToString());
        }

        if (!ValidateWordDataType(dataType))
        {
            throw new ArgumentException("The Data Type '" + Enum.GetName(typeof(MemoryWordDataType), dataType) + "' is not Supported on this PLC", nameof(dataType));
        }

        if (!ValidateWordStartAddress(startAddress, values.Length, dataType))
        {
            throw new ArgumentOutOfRangeException(nameof(startAddress), "The Start Address and Values Array Length combined are greater than the Maximum Address for the '" + Enum.GetName(typeof(MemoryWordDataType), dataType) + "' Data Type");
        }

        var request = WriteMemoryAreaWordRequest.CreateNew(this, startAddress, dataType, values);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        WriteMemoryAreaWordResponse.Validate(request, requestResult.Response);

        return new WriteWordsResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
        };
    }

    /// <summary>
    /// Read the current PLC real-time clock value.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read clock result.</returns>
    public async Task<ReadClockResult> ReadClockAsync(CancellationToken cancellationToken)
    {
        lock (_isInitializedLock)
        {
            if (!_isInitialized)
            {
                throw new OmronPLCException("This Omron PLC must be Initialized first before any Requests can be Processed");
            }
        }

        var request = ReadClockRequest.CreateNew(this);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        var result = ReadClockResponse.ExtractClock(request, requestResult.Response);

        return new ReadClockResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
            Clock = result.ClockDateTime,
            DayOfWeek = result.DayOfWeek
        };
    }

    /// <summary>
    /// Write the PLC real-time clock value.
    /// </summary>
    /// <param name="newDateTime">The new date and time.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write clock result.</returns>
    public Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, CancellationToken cancellationToken) => WriteClockAsync(newDateTime, (int)newDateTime.DayOfWeek, cancellationToken);

    /// <summary>
    /// Write the PLC real-time clock value with a specific day-of-week.
    /// </summary>
    /// <param name="newDateTime">The new date and time.</param>
    /// <param name="newDayOfWeek">The day of week (0-6).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The write clock result.</returns>
    public async Task<WriteClockResult> WriteClockAsync(DateTime newDateTime, int newDayOfWeek, CancellationToken cancellationToken)
    {
        lock (_isInitializedLock)
        {
            if (!_isInitialized)
            {
                throw new OmronPLCException("This Omron PLC must be Initialized first before any Requests can be Processed");
            }
        }

        var minDateTime = new DateTime(1998, 1, 1, 0, 0, 0);

        if (newDateTime < minDateTime)
        {
            throw new ArgumentOutOfRangeException(nameof(newDateTime), "The Date Time Value cannot be less than '" + minDateTime.ToString() + "'");
        }

        var maxDateTime = new DateTime(2069, 12, 31, 23, 59, 59);

        if (newDateTime > maxDateTime)
        {
            throw new ArgumentOutOfRangeException(nameof(newDateTime), "The Date Time Value cannot be greater than '" + maxDateTime.ToString() + "'");
        }

        if (newDayOfWeek < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newDayOfWeek), "The Day of Week Value cannot be less than 0");
        }

        if (newDayOfWeek > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(newDayOfWeek), "The Day of Week Value cannot be greater than 6");
        }

        var request = WriteClockRequest.CreateNew(this, newDateTime, (byte)newDayOfWeek);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        WriteClockResponse.Validate(request, requestResult.Response);

        return new WriteClockResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
        };
    }

    /// <summary>
    /// Read the PLC scan cycle time statistics.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The read cycle time result with minimum/maximum/average values.</returns>
    public async Task<ReadCycleTimeResult> ReadCycleTimeAsync(CancellationToken cancellationToken)
    {
        lock (_isInitializedLock)
        {
            if (!_isInitialized)
            {
                throw new OmronPLCException("This Omron PLC must be Initialized first before any Requests can be Processed");
            }
        }

        if (IsNSeries && PLCType != PLCType.NJ101 && PLCType != PLCType.NJ301 && PLCType != PLCType.NJ501)
        {
            throw new OmronPLCException("Read Cycle Time is not Supported on the NX/NY Series PLC");
        }

        var request = ReadCycleTimeRequest.CreateNew(this);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        var result = ReadCycleTimeResponse.ExtractCycleTime(request, requestResult.Response);

        return new ReadCycleTimeResult
        {
            BytesSent = requestResult.BytesSent,
            PacketsSent = requestResult.PacketsSent,
            BytesReceived = requestResult.BytesReceived,
            PacketsReceived = requestResult.PacketsReceived,
            Duration = requestResult.Duration,
            MinimumCycleTime = result.MinimumCycleTime,
            MaximumCycleTime = result.MaximumCycleTime,
            AverageCycleTime = result.AverageCycleTime,
        };
    }

    /// <summary>
    /// Releases resources used by the client and channel.
    /// </summary>
    /// <param name="disposing">True to dispose managed resources; otherwise, false.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Channel.Dispose();

            lock (_isInitializedLock)
            {
                _isInitialized = false;
            }
        }
    }

    private bool ValidateBitAddress(ushort address, MemoryBitDataType dataType) => dataType switch
    {
        MemoryBitDataType.DataMemory => address < (PLCType == PLCType.NX1P2 ? 16000 : 32768),
        MemoryBitDataType.CommonIO => address < 6144,
        MemoryBitDataType.Work => address < 512,
        MemoryBitDataType.Holding => address < 1536,
        MemoryBitDataType.Auxiliary => address < (PLCType == PLCType.CJ2 ? 11536 : 960),
        _ => false,
    };

    private bool ValidateBitDataType(MemoryBitDataType dataType) => dataType switch
    {
        MemoryBitDataType.DataMemory => PLCType != PLCType.CP1,
        MemoryBitDataType.CommonIO => true,
        MemoryBitDataType.Work => true,
        MemoryBitDataType.Holding => true,
        MemoryBitDataType.Auxiliary => !IsNSeries,
        _ => false,
    };

    private bool ValidateWordStartAddress(ushort startAddress, int length, MemoryWordDataType dataType) => dataType switch
    {
        MemoryWordDataType.DataMemory => startAddress + (length - 1) < (PLCType == PLCType.NX1P2 ? 16000 : 32768),
        MemoryWordDataType.CommonIO => startAddress + (length - 1) < 6144,
        MemoryWordDataType.Work => startAddress + (length - 1) < 512,
        MemoryWordDataType.Holding => startAddress + (length - 1) < 1536,
        MemoryWordDataType.Auxiliary => startAddress + (length - 1) < (PLCType == PLCType.CJ2 ? 11536 : 960),
        _ => false,
    };

    private bool ValidateWordDataType(MemoryWordDataType dataType) => dataType switch
    {
        MemoryWordDataType.DataMemory => true,
        MemoryWordDataType.CommonIO => true,
        MemoryWordDataType.Work => true,
        MemoryWordDataType.Holding => true,
        MemoryWordDataType.Auxiliary => !IsNSeries,
        _ => false,
    };

    private async Task RequestControllerInformation(CancellationToken cancellationToken)
    {
        var request = ReadCPUUnitDataRequest.CreateNew(this);

        var requestResult = await Channel.ProcessRequestAsync(request, Timeout, Retries, cancellationToken);

        var result = ReadCPUUnitDataResponse.ExtractData(requestResult.Response);

        if (result.ControllerModel?.Length > 0)
        {
            ControllerModel = result.ControllerModel;

            if (ControllerModel.StartsWith("NJ101"))
            {
                PLCType = PLCType.NJ101;
            }
            else if (ControllerModel.StartsWith("NJ301"))
            {
                PLCType = PLCType.NJ301;
            }
            else if (ControllerModel.StartsWith("NJ501"))
            {
                PLCType = PLCType.NJ501;
            }
            else if (ControllerModel.StartsWith("NX1P2"))
            {
                PLCType = PLCType.NX1P2;
            }
            else if (ControllerModel.StartsWith("NX102"))
            {
                PLCType = PLCType.NX102;
            }
            else if (ControllerModel.StartsWith("NX701"))
            {
                PLCType = PLCType.NX701;
            }
            else if (ControllerModel.StartsWith("NJ") || ControllerModel.StartsWith("NX") || ControllerModel.StartsWith("NY"))
            {
                PLCType = PLCType.NJ_NX_NY_Series;
            }
            else if (ControllerModel.StartsWith("CJ2"))
            {
                PLCType = PLCType.CJ2;
            }
            else if (ControllerModel.StartsWith("CP1"))
            {
                PLCType = PLCType.CP1;
            }
            else if (ControllerModel.StartsWith("C"))
            {
                PLCType = PLCType.C_Series;
            }
            else
            {
                PLCType = PLCType.Unknown;
            }
        }

        if (result.ControllerVersion?.Length > 0)
        {
            ControllerVersion = result.ControllerVersion;
        }
    }
}
