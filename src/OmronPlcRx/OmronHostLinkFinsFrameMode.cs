// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace OmronPlcRx;

/// <summary>
/// Specifies the Host Link FINS frame layout used over serial communications.
/// </summary>
public enum OmronHostLinkFinsFrameMode
{
    /// <summary>
    /// Directly connected host-computer-to-CPU format using ICF/DA2/SA2/SID fields.
    /// </summary>
    Direct,

    /// <summary>
    /// Network-capable format using the complete FINS header.
    /// </summary>
    Network,
}
