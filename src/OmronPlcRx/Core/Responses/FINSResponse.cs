// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using OmronPlcRx.Core.Enums;
using OmronPlcRx.Core.Requests;

namespace OmronPlcRx.Core.Responses;

/// <summary>Represents the f in sr es po ns e type.</summary>
internal sealed class FINSResponse
{
    /// <summary>Stores the h ea de rl en gt h value.</summary>
    internal const int HeaderLength = 10;

    /// <summary>Stores the c om ma nd le ng th value.</summary>
    internal const int CommandLength = 2;

    /// <summary>Stores the r es po ns ec od el en gt h value.</summary>
    internal const int ResponseCodeLength = 2;

    /// <summary>Initializes a new instance of the <see cref="FINSResponse"/> class.</summary>
    private FINSResponse()
    {
    }

    /// <summary>Gets or sets the service id value.</summary>
    internal byte ServiceID { get; set; }

    /// <summary>Gets the function code value.</summary>
    internal byte FunctionCode { get; private set; }

    /// <summary>Gets or sets the sub function code value.</summary>
    internal byte SubFunctionCode { get; set; }

    /// <summary>Gets or sets the main response code value.</summary>
    internal byte MainResponseCode { get; set; }

    /// <summary>Gets or sets the sub response code value.</summary>
    internal byte SubResponseCode { get; set; }

    /// <summary>Gets the data value.</summary>
    internal byte[]? Data { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="CreateNew"/> class.</summary>
    /// <param name="message">The m es sa ge value.</param>
    /// <param name="request">The r eq ue st value.</param>
    /// <returns>The result produced by the operation.</returns>
    internal static FINSResponse CreateNew(Memory<byte> message, FINSRequest request)
    {
        if (message.Length < HeaderLength + CommandLength + ResponseCodeLength)
        {
            throw new FINSException("The FINS Response Message Length was too short");
        }

        var response = new FINSResponse();

        var header = message.Slice(0, HeaderLength).ToArray();

        response.ServiceID = header[9];

        var command = message.Slice(HeaderLength, CommandLength).ToArray();

        if (!ValidateFunctionCode(command[0]))
        {
            throw new FINSException("Invalid Function Code '" + command[0].ToString() + "'");
        }

        response.FunctionCode = command[0];

        if (response.FunctionCode != request.FunctionCode)
        {
            throw new FINSException("Unexpected Function Code '" + Enum.GetName(typeof(FunctionCode), response.FunctionCode) + "' - Expecting '" + Enum.GetName(typeof(FunctionCode), request.FunctionCode) + "'");
        }

        if (!ValidateSubFunctionCode(command[0], command[1]))
        {
            throw new FINSException("Invalid Sub Function Code '" + command[1].ToString() + "' for Function Code '" + command[0].ToString() + "'");
        }

        response.SubFunctionCode = command[1];

        if (response.SubFunctionCode != request.SubFunctionCode)
        {
            throw new FINSException("Unexpected Sub Function Code '" + GetSubFunctionCodeName(response.FunctionCode, response.SubFunctionCode) + "' - Expecting '" + GetSubFunctionCodeName(request.FunctionCode, request.SubFunctionCode) + "'");
        }

        var responseCode = message.Slice(HeaderLength + CommandLength, ResponseCodeLength).ToArray();

        if (HasNetworkRelayError(responseCode[0]))
        {
            throw new FINSException("A Network Relay Error has occurred");
        }

        response.MainResponseCode = GetMainResponseCode(responseCode[0]);

        response.SubResponseCode = GetSubResponseCode(responseCode[1]);

        ThrowIfResponseError(response.MainResponseCode, response.SubResponseCode);

        if (request.ServiceID != response.ServiceID)
        {
            throw new FINSException("The Service ID for the FINS Request '" + request.ServiceID + "' did not match the FINS Response '" + response.ServiceID + "'");
        }

        response.Data = message.Length > HeaderLength + CommandLength + ResponseCodeLength ? message.Slice(HeaderLength + CommandLength + ResponseCodeLength).ToArray() : Array.Empty<byte>();

        return response;
    }

    /// <summary>Represents the v al id at ef un ct io nc od e enumeration.</summary>
    /// <param name="functionCode">The f un ct io nc od e value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    internal static bool ValidateFunctionCode(byte functionCode) => Enum.IsDefined(typeof(FunctionCode), functionCode);

    /// <summary>Initializes a new instance of the <see cref="ValidateSubFunctionCode"/> class.</summary>
    /// <param name="functionCode">The f un ct io nc od e value.</param>
    /// <param name="subFunctionCode">The s ub fu nc ti on co de value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    internal static bool ValidateSubFunctionCode(byte functionCode, byte subFunctionCode) => (FunctionCode)functionCode switch
    {
        Enums.FunctionCode.AccessRights => Enum.IsDefined(typeof(AccessRightsFunctionCode), subFunctionCode),
        Enums.FunctionCode.Debugging => Enum.IsDefined(typeof(DebuggingFunctionCode), subFunctionCode),
        Enums.FunctionCode.ErrorLog => Enum.IsDefined(typeof(ErrorLogFunctionCode), subFunctionCode) || Enum.IsDefined(typeof(FinsWriteLogFunctionCode), subFunctionCode),
        Enums.FunctionCode.FileMemory => Enum.IsDefined(typeof(FileMemoryFunctionCode), subFunctionCode),
        Enums.FunctionCode.MachineConfiguration => Enum.IsDefined(typeof(MachineConfigurationFunctionCode), subFunctionCode),
        Enums.FunctionCode.MemoryArea => Enum.IsDefined(typeof(MemoryAreaFunctionCode), subFunctionCode),
        Enums.FunctionCode.MessageDisplay => Enum.IsDefined(typeof(MessageDisplayFunctionCode), subFunctionCode),
        Enums.FunctionCode.OperatingMode => Enum.IsDefined(typeof(OperatingModeFunctionCode), subFunctionCode),
        Enums.FunctionCode.ParameterArea => Enum.IsDefined(typeof(ParameterAreaFunctionCode), subFunctionCode),
        Enums.FunctionCode.ProgramArea => Enum.IsDefined(typeof(ProgramAreaFunctionCode), subFunctionCode),
        Enums.FunctionCode.SerialGateway => Enum.IsDefined(typeof(SerialGatewayFunctionCode), subFunctionCode),
        Enums.FunctionCode.Status => Enum.IsDefined(typeof(StatusFunctionCode), subFunctionCode),
        Enums.FunctionCode.TimeData => Enum.IsDefined(typeof(TimeDataFunctionCode), subFunctionCode),
        _ => false,
    };

    /// <summary>Initializes a new instance of the <see cref="GetSubFunctionCodeName"/> class.</summary>
    /// <param name="functionCode">The f un ct io nc od e value.</param>
    /// <param name="subFunctionCode">The s ub fu nc ti on co de value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static string? GetSubFunctionCodeName(byte functionCode, byte subFunctionCode) => (FunctionCode)functionCode switch
    {
        Enums.FunctionCode.AccessRights => Enum.GetName(typeof(AccessRightsFunctionCode), subFunctionCode),
        Enums.FunctionCode.Debugging => Enum.GetName(typeof(DebuggingFunctionCode), subFunctionCode),
        Enums.FunctionCode.ErrorLog => Enum.IsDefined(typeof(ErrorLogFunctionCode), subFunctionCode) ? Enum.GetName(typeof(ErrorLogFunctionCode), subFunctionCode) : Enum.GetName(typeof(FinsWriteLogFunctionCode), subFunctionCode),
        Enums.FunctionCode.FileMemory => Enum.GetName(typeof(FileMemoryFunctionCode), subFunctionCode),
        Enums.FunctionCode.MachineConfiguration => Enum.GetName(typeof(MachineConfigurationFunctionCode), subFunctionCode),
        Enums.FunctionCode.MemoryArea => Enum.GetName(typeof(MemoryAreaFunctionCode), subFunctionCode),
        Enums.FunctionCode.MessageDisplay => Enum.GetName(typeof(MessageDisplayFunctionCode), subFunctionCode),
        Enums.FunctionCode.OperatingMode => Enum.GetName(typeof(OperatingModeFunctionCode), subFunctionCode),
        Enums.FunctionCode.ParameterArea => Enum.GetName(typeof(ParameterAreaFunctionCode), subFunctionCode),
        Enums.FunctionCode.ProgramArea => Enum.GetName(typeof(ProgramAreaFunctionCode), subFunctionCode),
        Enums.FunctionCode.SerialGateway => Enum.GetName(typeof(SerialGatewayFunctionCode), subFunctionCode),
        Enums.FunctionCode.Status => Enum.GetName(typeof(StatusFunctionCode), subFunctionCode),
        Enums.FunctionCode.TimeData => Enum.GetName(typeof(TimeDataFunctionCode), subFunctionCode),
        _ => "Unknown",
    };

    /// <summary>Initializes a new instance of the <see cref="HasNetworkRelayError"/> class.</summary>
    /// <param name="responseCode">The r es po ns ec od e value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool HasNetworkRelayError(byte responseCode) => (responseCode & (1 << 7)) != 0;

    /// <summary>Initializes a new instance of the <see cref="GetMainResponseCode"/> class.</summary>
    /// <param name="value">The v al ue value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static byte GetMainResponseCode(byte value)
    {
        const byte includedBits = 0x7F;

        return (byte)(value & includedBits);
    }

    /// <summary>Initializes a new instance of the <see cref="GetSubResponseCode"/> class.</summary>
    /// <param name="value">The v al ue value.</param>
    /// <returns>The result produced by the operation.</returns>
    private static byte GetSubResponseCode(byte value)
    {
        const byte includedBits = 0x3F;

        return (byte)(value & includedBits);
    }

    /// <summary>Initializes a new instance of the <see cref="ThrowIfResponseError"/> class.</summary>
    /// <param name="mainCode">The m ai nc od e value.</param>
    /// <param name="subCode">The s ub co de value.</param>
    private static void ThrowIfResponseError(byte mainCode, byte subCode)
    {
        if (mainCode == 0 && subCode == 0)
        {
            return;
        }

        var exception = mainCode switch
        {
            0x00 => subCode switch
            {
                0x01 => new FINSException("Normal Completion (0x00) - Service was Canceled (0x01)"),
                _ => null,
            },
            0x01 => subCode switch
            {
                0x01 => new FINSException("Local Node Error (0x01) - The Local Node was not found within the Network (0x01)"),
                _ => new FINSException("Local Node Error (0x01) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x02 => subCode switch
            {
                0x01 => new FINSException("Destination Node Error (0x02) - The Destination Node was not found within the Network (0x01)"),
                0x02 => new FINSException("Destination Node Error (0x02) - The Destination Unit could not be found (0x02)"),
                0x04 => new FINSException("Destination Node Error (0x02) - The Destination Node was Busy (0x04)"),
                0x05 => new FINSException("Destination Node Error (0x02) - Response Timeout (0x05)"),
                _ => new FINSException("Destination Node Error (0x02) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x03 => subCode switch
            {
                0x01 => new FINSException("Controller Error (0x03) - Communications Controller Error (0x01)"),
                0x02 => new FINSException("Controller Error (0x03) - CPU Unit Error (0x02)"),
                0x03 => new FINSException("Controller Error (0x03) - Controller Board Error (0x03)"),
                0x04 => new FINSException("Controller Error (0x03) - Unit Number Error (0x04)"),
                _ => new FINSException("Controller Error (0x03) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x04 => subCode switch
            {
                0x01 => new FINSException("Service Unsupported Error (0x04) - Undefined Command (0x01)"),
                0x02 => new FINSException("Service Unsupported Error (0x04) - Command Not Supported by Model/Version (0x02)"),
                _ => new FINSException("Service Unsupported Error (0x04) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x05 => subCode switch
            {
                0x01 => new FINSException("Routing Table Error (0x05) - Destination Address Setting Error (0x01)"),
                0x02 => new FINSException("Routing Table Error (0x05) - No Routing Tables (0x02)"),
                0x03 => new FINSException("Routing Table Error (0x05) - Routing Table Error (0x03)"),
                0x04 => new FINSException("Routing Table Error (0x05) - Too Many Relays (0x04)"),
                _ => new FINSException("Routing Table Error (0x05) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x10 => subCode switch
            {
                0x01 => new FINSException("Command Format Error (0x10) - Command Data is too Long (0x01)"),
                0x02 => new FINSException("Command Format Error (0x10) - Command Data is too Short (0x02)"),
                0x03 => new FINSException("Command Format Error (0x10) - Elements Length and Values Length do not Match (0x03)"),
                0x04 => new FINSException("Command Format Error (0x10) - Command Format Error (0x04)"),
                0x05 => new FINSException("Command Format Error (0x10) - Header Error (0x05)"),
                _ => new FINSException("Command Format Error (0x10) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x11 => subCode switch
            {
                0x01 => new FINSException("Parameter Error (0x11) - No Memory Area Specified (0x01)"),
                0x02 => new FINSException("Parameter Error (0x11) - Access Size Error (0x02)"),
                0x03 => new FINSException("Parameter Error (0x11) - Address Range Error (0x03)"),
                0x04 => new FINSException("Parameter Error (0x11) - Address Range Exceeded (0x04)"),
                0x06 => new FINSException("Parameter Error (0x11) - Program Missing (0x06)"),
                0x09 => new FINSException("Parameter Error (0x11) - Relational Error (0x09)"),
                0x0A => new FINSException("Parameter Error (0x11) - Duplicate Data Access (0x0A)"),
                0x0B => new FINSException("Parameter Error (0x11) - Response Data is too Long (0x0B)"),
                0x0C => new FINSException("Parameter Error (0x11) - Parameter Error (0x0C)"),
                _ => new FINSException("Parameter Error (0x11) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x20 => subCode switch
            {
                0x02 => new FINSException("Read not Possible Error (0x20) - The Program Area is Protected (0x02)"),
                0x03 => new FINSException("Read not Possible Error (0x20) - Table Missing (0x03)"),
                0x04 => new FINSException("Read not Possible Error (0x20) - Data Missing (0x04)"),
                0x05 => new FINSException("Read not Possible Error (0x20) - Program Missing (0x05)"),
                0x06 => new FINSException("Read not Possible Error (0x20) - File Missing (0x06)"),
                0x07 => new FINSException("Read not Possible Error (0x20) - Data Mismatch (0x07)"),
                _ => new FINSException("Read not Possible Error (0x20) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x21 => subCode switch
            {
                0x01 => new FINSException("Write not Possible Error (0x21) - The Specified Area is Read-Only (0x01)"),
                0x02 => new FINSException("Write not Possible Error (0x21) - The Program Area is Protected (0x02)"),
                0x03 => new FINSException("Write not Possible Error (0x21) - Cannot Register (0x03)"),
                0x05 => new FINSException("Write not Possible Error (0x21) - Program Missing (0x05)"),
                0x06 => new FINSException("Write not Possible Error (0x21) - File Missing (0x06)"),
                0x07 => new FINSException("Write not Possible Error (0x21) - File Name already Exists (0x07)"),
                0x08 => new FINSException("Write not Possible Error (0x21) - Cannot Change (0x08)"),
                _ => new FINSException("Write not Possible Error (0x21) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x22 => subCode switch
            {
                0x01 => new FINSException("Not Executable in Current Mode (0x22) - Not Possible during Execution (0x01)"),
                0x02 => new FINSException("Not Executable in Current Mode (0x22) - Not Possible while Running (0x02)"),
                0x03 => new FINSException("Not Executable in Current Mode (0x22) - PLC is in Program Mode (0x03)"),
                0x04 => new FINSException("Not Executable in Current Mode (0x22) - PLC is in Debug Mode (0x04)"),
                0x05 => new FINSException("Not Executable in Current Mode (0x22) - PLC is in Monitor Mode (0x05)"),
                0x06 => new FINSException("Not Executable in Current Mode (0x22) - PLC is in Run Mode (0x06)"),
                0x07 => new FINSException("Not Executable in Current Mode (0x22) - Specified Node is not a Polling Node (0x07)"),
                0x08 => new FINSException("Not Executable in Current Mode (0x22) - Step Cannot be Executed (0x08)"),
                _ => new FINSException("Not Executable in Current Mode (0x22) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x23 => subCode switch
            {
                0x01 => new FINSException("No Such Device (0x23) - File Device Missing (0x01)"),
                0x02 => new FINSException("No Such Device (0x23) - Memory Missing (0x02)"),
                0x03 => new FINSException("No Such Device (0x23) - Clock Missing (0x03)"),
                _ => new FINSException("No Such Device (0x23) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            0x24 => subCode switch
            {
                0x01 => new FINSException("Cannot Start/Stop (0x24) - Table Missing (0x01)"),
                _ => new FINSException("Cannot Start/Stop (0x24) - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
            },
            _ => new FINSException("Unknown Error - Main Response Code (0x" + mainCode.ToString("X2") + ") - Sub Response Code (0x" + subCode.ToString("X2") + ")"),
        };

        if (exception is null)
        {
            return;
        }

        throw exception;
    }
}
