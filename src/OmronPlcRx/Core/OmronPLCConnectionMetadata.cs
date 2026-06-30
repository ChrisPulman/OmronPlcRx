// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
#if REACTIVE_SHIM
using OmronPlcRx.Reactive.Enums;
#else
using OmronPlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core;
#else
namespace OmronPlcRx.Core;
#endif

/// <summary>Provides validation and metadata helpers for PLC connections.</summary>
internal static class OmronPLCConnectionMetadata
{
    /// <summary>Stores known controller model prefixes.</summary>
    private static readonly (string Prefix, PLCType Type)[] ModelPrefixes =
    [
        ("NJ101", PLCType.NJ101),
        ("NJ301", PLCType.NJ301),
        ("NJ501", PLCType.NJ501),
        ("NX1P2", PLCType.NX1P2),
        ("NX102", PLCType.NX102),
        ("NX701", PLCType.NX701),
        ("CJ2", PLCType.CJ2),
        ("CP1", PLCType.CP1),
        ("C", PLCType.C_Series),
    ];

    /// <summary>Validates FINS node identifiers.</summary>
    /// <param name="localNodeId">The local node identifier.</param>
    /// <param name="remoteNodeId">The remote node identifier.</param>
    /// <param name="connectionMethod">The connection method.</param>
    internal static void ValidateNodeIdentifiers(byte localNodeId, byte remoteNodeId, ConnectionMethod connectionMethod)
    {
        ThrowIfReservedLocalNode(localNodeId);
        ThrowIfReservedRemoteNode(remoteNodeId, connectionMethod);
        ThrowIfSameNode(localNodeId, remoteNodeId);
    }

    /// <summary>Validates the remote host.</summary>
    /// <param name="remoteHost">The remote host.</param>
    /// <returns>The validated remote host.</returns>
    internal static string ValidateRemoteHost(string remoteHost)
    {
        var host = remoteHost ?? throw new ArgumentNullException(nameof(remoteHost), "The Remote Host cannot be Null");
        return host.Length == 0 ? throw new ArgumentException("The Remote Host cannot be Empty", nameof(remoteHost)) : host;
    }

    /// <summary>Validates the network port.</summary>
    /// <param name="connectionMethod">The connection method.</param>
    /// <param name="port">The network port.</param>
    internal static void ValidatePort(ConnectionMethod connectionMethod, int port)
    {
        if (connectionMethod == ConnectionMethod.Serial || port > 0)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(port), "The Port cannot be less than 1");
    }

    /// <summary>Gets the PLC type for a controller model string.</summary>
    /// <param name="controllerModel">The controller model string.</param>
    /// <returns>The inferred PLC type.</returns>
    internal static PLCType GetPLCType(string controllerModel)
    {
        foreach (var (prefix, type) in ModelPrefixes)
        {
            if (controllerModel.StartsWith(prefix, StringComparison.Ordinal))
            {
                return type;
            }
        }

        return IsNSeriesController(controllerModel) ? PLCType.NJ_NX_NY_Series : PLCType.Unknown;
    }

    /// <summary>Throws when the local node identifier is reserved.</summary>
    /// <param name="localNodeId">The local node identifier.</param>
    private static void ThrowIfReservedLocalNode(byte localNodeId)
    {
        switch (localNodeId)
        {
            case 0:
                throw new ArgumentOutOfRangeException(nameof(localNodeId), "The Local Node ID cannot be set to 0");
            case 255:
                throw new ArgumentOutOfRangeException(nameof(localNodeId), "The Local Node ID cannot be set to 255");
        }
    }

    /// <summary>Throws when the remote node identifier is reserved.</summary>
    /// <param name="remoteNodeId">The remote node identifier.</param>
    /// <param name="connectionMethod">The connection method.</param>
    private static void ThrowIfReservedRemoteNode(byte remoteNodeId, ConnectionMethod connectionMethod)
    {
        switch (remoteNodeId)
        {
            case 0 when connectionMethod != ConnectionMethod.Serial:
                throw new ArgumentOutOfRangeException(nameof(remoteNodeId), "The Remote Node ID cannot be set to 0 for Ethernet FINS connections");
            case 255:
                throw new ArgumentOutOfRangeException(nameof(remoteNodeId), "The Remote Node ID cannot be set to 255");
        }
    }

    /// <summary>Throws when local and remote node identifiers match.</summary>
    /// <param name="localNodeId">The local node identifier.</param>
    /// <param name="remoteNodeId">The remote node identifier.</param>
    private static void ThrowIfSameNode(byte localNodeId, byte remoteNodeId)
    {
        if (remoteNodeId != localNodeId)
        {
            return;
        }

        throw new ArgumentException("The Remote Node ID cannot be the same as the Local Node ID", nameof(remoteNodeId));
    }

    /// <summary>Checks whether a controller model is in the N-series family.</summary>
    /// <param name="controllerModel">The controller model string.</param>
    /// <returns>A value indicating whether the model is in the N-series family.</returns>
    private static bool IsNSeriesController(string controllerModel) =>
        controllerModel.StartsWith("NJ", StringComparison.Ordinal) ||
        controllerModel.StartsWith("NX", StringComparison.Ordinal) ||
        controllerModel.StartsWith("NY", StringComparison.Ordinal);
}
