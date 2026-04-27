# OmronPlcRx Serial Communications Research Plan

## Goal

Assess whether OmronPlcRx should add serial communications for legacy Omron PLCs without Ethernet, and define the safest implementation path.

## Scope constraint

All repository work for this topic/thread is constrained to:

- Windows: `C:\Projects\GitHub\ChrisPulman\OmronPlcRx`
- WSL: `/mnt/c/Projects/GitHub/ChrisPulman/OmronPlcRx`

This plan is analysis-only. No implementation changes are made here.

## Current repository context

Evidence from the current codebase:

- The public client currently accepts only `ConnectionMethod`, host, port, timeout and retry settings, then constructs an internal `OmronPLCConnection` directly: `src/OmronPlcRx/OmronPlcRx.cs:49-53`.
- `ConnectionMethod` currently exposes only `TCP` and `UDP`: `src/OmronPlcRx/Enums/ConnectionMethod.cs:9-20`.
- `OmronPLCConnection` currently selects `UDPChannel` or `TCPChannel` directly in the constructor: `src/OmronPlcRx/Core/OmronPLCConnection.cs:99`.
- Transport is currently represented by `BaseChannel`, which serializes request/response exchange with a semaphore and calls `request.BuildMessage(...)`: `src/OmronPlcRx/Core/Channels/BaseChannel.cs:34-68`.
- TCP has Omron FINS/TCP negotiation and wraps FINS payloads in the Ethernet `FINS` TCP header: `src/OmronPlcRx/Core/Channels/TCPChannel.cs:133-163`, `src/OmronPlcRx/Core/Channels/TCPChannel.cs:166-199`.
- UDP sends the raw FINS frame and validates response headers starting with `0xC0` or `0xC1`: `src/OmronPlcRx/Core/Channels/UDPChannel.cs:78-156`.
- `FINSRequest.BuildMessage(...)` constructs binary FINS frames with a 10-byte FINS header and binary command payload: `src/OmronPlcRx/Core/Requests/FINSRequest.cs:39-88`.
- Existing memory read/write command encoders are binary FINS-oriented; example word read encodes memory area, big-endian address and big-endian length: `src/OmronPlcRx/Core/Requests/ReadMemoryAreaWordRequest.cs:33-54`.
- `FunctionCode.SerialGateway = 0x28` already exists: `src/OmronPlcRx/Core/Enums/FunctionCode.cs:21`.
- `SerialGatewayFunctionCode` currently models CompoWay/F, Modbus-RTU and Modbus-ASCII conversion only, not Host Link FINS: `src/OmronPlcRx/Core/Enums/SerialGatewayFunctionCode.cs:6-11`.
- There is currently no detected test project in this repo (`*Test*` search returned no files).
- The dashboard connection model is Ethernet-shaped: host/port/method only, no serial port, baud, parity, stop bits, handshake, unit number, or protocol mode: `src/OmronPlcRx.Dashboard/ViewModels/ConnectionSettings.cs:10-26`.
- Project targets `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`: `src/OmronPlcRx/OmronPlcRx.csproj:4`.

## External protocol research summary

Primary source used:

- Omron `W342-E1-17` — *SYSMAC CS/CJ/CP Series / NSJ Series Communications Commands Reference Manual*: `https://edata.omron.com.au/eData/Software/FGW/W342-E1-17.pdf`

Key findings:

1. **Two serial-relevant protocol families exist.**
   - C-mode / Host Link commands are a serial command/response system for Host Link Mode, used for direct CPU control operations including I/O memory read/write and operating mode changes.
   - FINS commands can also be sent via Host Link by enclosing the FINS frame in a Host Link header/FCS/terminator.

2. **C-mode Host Link is ASCII and frame-limited.**
   - Manual states Host Link C-mode frames are ASCII, begin with `@`, include unit number, 2-character header code, text, 2-character FCS, and terminate with `*` + CR.
   - Single Host Link frame max is 131 characters.
   - For word transfers, first command frame is limited to 30 words, subsequent frames up to 31 words.
   - FCS is XOR of characters from the first frame character through the last text character, converted to two ASCII hex characters.

3. **FINS-over-Host-Link is likely the best fit for this library.**
   - Manual states that with Host Link communications, FINS command/response frames are sent/received as ASCII.
   - Header code for Host Link FINS is `FA`.
   - Directly connected CPU usage can set ICF `00`; networked/relayed usage uses ICF `80` plus network routing fields.
   - Host Link FINS frames still use Host Link FCS and `*\r` terminator.
   - Response data limit noted in the manual is 1,076 ASCII characters / 538 bytes without response code.

4. **Omron Serial Gateway is adjacent but not the first direct-PC serial feature.**
   - Manual section 3-6 says received FINS messages can be converted to CompoWay/F, Modbus-RTU, Modbus-ASCII, or Host Link FINS by PLC Serial Communications Boards/Units.
   - Existing enum support for `0x28/0x03..0x05` maps to CompoWay/F / Modbus conversion; Host Link FINS conversion is “Any / Any” user-specified FINS command, not a simple single enum member.
   - Serial Gateway matters if OmronPlcRx communicates to a gateway PLC over Ethernet/FINS and that gateway relays to serial devices. It is not the same as the PC directly connecting to a legacy PLC serial port.

5. **SerialPortRx is available but not compatible with all current targets.**
   - NuGet `SerialPortRx` latest found: `3.4.4`.
   - Package assets found for `net8.0`, `net9.0`, `net10.0`, Windows variants, and .NET Framework; no `lib/netstandard2.0` asset was present in the nupkg.
   - `System.IO.Ports` package has a `netstandard2.0` asset, so there are two viable transport-layer strategies:
     - direct `System.IO.Ports` for all targets; or
     - use `SerialPortRx` only for modern `net8+` targets and a fallback/adapter for `netstandard2.0`.

## Option analysis

### Option A — Add direct Host Link FINS serial transport first

Add a serial transport that keeps the existing command model but converts binary FINS frames to Host Link FINS ASCII frames at the channel boundary.

Shape:

- Add `ConnectionMethod.Serial` or a clearer split such as `OmronTransportKind.SerialHostLinkFins`.
- Add `OmronSerialOptions` with:
  - `PortName`
  - `BaudRate`
  - `DataBits`
  - `Parity`
  - `StopBits`
  - `Handshake`
  - `HostLinkUnitNumber` (`0..31`)
  - `ResponseWaitTime` (`0..F`, 10 ms units)
  - direct/networked FINS header mode options (`ICF`, route fields) if needed.
- Add `SerialHostLinkFinsChannel : BaseChannel`.
- Reuse `FINSRequest.BuildMessage(...)` to get binary FINS command bytes.
- Convert binary FINS bytes to ASCII hex inside the serial channel.
- Wrap as `@` + unit number + `FA` + response-wait + ASCII FINS fields + FCS + `*\r`.
- Read until `*\r`, validate FCS, parse end code, decode ASCII hex FINS payload back to binary, then pass binary `Memory<byte>` into `FINSResponse.CreateNew(...)`.

Pros:

- Maximum reuse of current request/response parsing.
- Preserves existing high-level APIs and tag polling.
- Aligns with the existing library identity as a FINS client.
- Avoids duplicating memory area read/write logic in C-mode commands.

Cons / risks:

- Requires exact Host Link FINS frame formatting and response parsing.
- Directly connected serial mode has different FINS header conventions than Ethernet TCP/UDP.
- Existing `FINSRequest` currently derives local/remote node IDs from TCP negotiation or constructor-provided IDs; Host Link FINS needs explicit serial unit number and direct-connected FINS header semantics.

Recommendation: **Best first implementation path**.

### Option B — Add C-mode Host Link commands first

Implement classic Host Link C-mode commands directly (`RD`, `WD`, `RR`, `WR`, etc.).

Pros:

- Strong support for older PLCs and simple direct serial links.
- Maps well to legacy C-series Host Link workflows.
- Simple ASCII commands for initial DM/CIO word read/write.

Cons:

- Creates a second protocol stack beside FINS.
- Data limits and partitioning become command-specific.
- Many existing FINS request/response classes cannot be reused.
- C-mode cannot route beyond the directly connected CPU and lacks broader FINS functionality.

Recommendation: **Second phase** after Host Link FINS, focused on very old PLCs that do not support FINS-over-Host-Link.

### Option C — Add Serial Gateway support only

Expose FINS serial-gateway helper commands for a PLC/network gateway to relay to CompoWay/F, Modbus-RTU, Modbus-ASCII or Host Link FINS.

Pros:

- Existing enum already starts down this path.
- Useful for Ethernet-to-serial gateway PLC topologies.

Cons:

- Does not solve the stated direct legacy PLC without Ethernet case by itself.
- Requires serial gateway hardware/configuration on a PLC Serial Communications Board/Unit.

Recommendation: **Separate feature**, not the primary solution for direct PC-to-legacy-PLC serial connectivity.

## Recommended architecture

### 1. Split protocol frame building from physical transport

Keep `FINSRequest` and `FINSResponse` as the canonical FINS command model, but introduce a small framing layer:

```csharp
internal interface IOmronFrameCodec
{
    ReadOnlyMemory<byte> EncodeRequest(FINSRequest request, byte requestId);
    Memory<byte> DecodeResponse(ReadOnlyMemory<byte> transportFrame, FINSRequest request);
}
```

Candidate codecs:

- `FinsUdpFrameCodec` — current raw binary FINS behavior.
- `FinsTcpFrameCodec` — current FINS/TCP header and handshake behavior may remain in `TCPChannel`, but should be isolated eventually.
- `HostLinkFinsFrameCodec` — ASCII hex Host Link FINS wrapper.

### 2. Add serial channel as transport, not as another client

```csharp
internal sealed class SerialHostLinkFinsChannel : BaseChannel
{
    // Owns serial port adapter and HostLinkFinsFrameCodec.
}
```

Serial receive behavior:

- Single in-flight request protected by existing `BaseChannel.Semaphore`.
- Accumulate received bytes until `*\r`.
- Timeout if terminator not received.
- Validate response starts with `@`, correct unit number, header `FA`, end code normal, and valid FCS.
- Decode ASCII hex payload back to binary FINS response frame.
- Feed decoded frame to `FINSResponse.CreateNew(...)` unchanged.

### 3. Transport package strategy

Because the main library targets `netstandard2.0`:

- Prefer a small internal `ISerialPortAdapter` abstraction.
- For all targets, implement baseline adapter with `System.IO.Ports` (`System.IO.Ports` has `netstandard2.0` assets).
- Optionally add `SerialPortRx` for `net8.0+` only via conditional package reference and conditional adapter.
- Do not add `SerialPortRx` unconditionally unless the package gains a true `netstandard2.0` asset.

Suggested abstraction:

```csharp
internal interface ISerialPortAdapter : IDisposable
{
    Task OpenAsync(CancellationToken cancellationToken);
    Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
    IObservable<byte> ReceivedBytes { get; }
}
```

### 4. Public API shape

Avoid overloading the current constructor with many serial-only parameters. Prefer options records:

```csharp
public sealed record OmronPlcConnectionOptions
{
    public byte LocalNodeId { get; init; }
    public byte RemoteNodeId { get; init; }
    public ConnectionMethod Method { get; init; }
    public OmronEthernetOptions? Ethernet { get; init; }
    public OmronSerialOptions? Serial { get; init; }
}

public sealed record OmronSerialOptions
{
    public string PortName { get; init; } = "COM1";
    public int BaudRate { get; init; } = 9600;
    public int DataBits { get; init; } = 7;
    public Parity Parity { get; init; } = Parity.Even;
    public StopBits StopBits { get; init; } = StopBits.Two;
    public Handshake Handshake { get; init; } = Handshake.None;
    public byte HostLinkUnitNumber { get; init; } = 0;
    public byte ResponseWaitTime { get; init; } = 0;
}
```

Keep existing constructor for compatibility and add a new options-based constructor.

### 5. Test-first implementation slices

There is no existing test project, so add tests before implementation.

Minimum useful test suite:

1. `HostLinkFcsTests`
   - XOR FCS calculation against known manual-style examples.
   - Reject bad FCS.
2. `HostLinkFinsCodecTests`
   - Encodes a known binary FINS memory-area read into expected ASCII `@00FA...FCS*\r` frame.
   - Decodes normal response into the same binary response shape expected by `FINSResponse.CreateNew(...)`.
3. `SerialHostLinkFinsChannelTests`
   - Fake serial adapter returns fragmented bytes; channel accumulates until `*\r`.
   - Timeout with no terminator.
   - Bad unit/header/end-code/FCS failures are deterministic.
4. Existing API parity tests
   - `ReadWordsAsync`, `WriteWordsAsync`, `ReadBitsAsync`, `WriteBitsAsync` produce same high-level results over fake serial as fake UDP/TCP.
5. Dashboard tests or smoke checks
   - Serial option fields appear only when serial transport selected.

### 6. Documentation updates after implementation

README should gain:

- Transport matrix: TCP, UDP, Serial Host Link FINS, and later C-mode Host Link.
- Serial setup example for Windows COM port and Linux `/dev/tty*`.
- Host Link unit number explanation (`0..31`).
- Limitations: ASCII Host Link framing, response length limits, serial throughput, no TCP node negotiation, PLC serial port must be configured for Host Link / Host Link FINS.
- Legacy PLC guidance: use Host Link FINS first where available; use C-mode only for very old PLCs.

## Ranked hypotheses / recommendation

| Rank | Hypothesis | Confidence | Evidence strength | Why it leads |
|---|---|---:|---|---|
| 1 | Direct Host Link FINS serial transport is the best first feature | High | Strong | Omron manual supports FINS via Host Link, current library is already binary FINS-centered, and transport/channel boundary can convert ASCII serial frames back to existing binary response parser. |
| 2 | C-mode Host Link should be implemented first for maximum legacy coverage | Medium | Moderate | C-mode is the classic serial option and supports older PLCs, but it duplicates protocol logic and loses FINS routing/function coverage. |
| 3 | Serial Gateway support alone solves this request | Low | Strong against | Serial Gateway is for PLC/board-mediated conversion; it does not directly connect this PC library to a PLC serial port without Ethernet. |

## Critical unknowns

1. Which legacy PLC models are target devices: CP1, CJ/CS, CQM1, CPM1/CPM2, C200H, etc.
2. Whether the target serial ports support Host Link FINS (`FA`) or only C-mode Host Link.
3. Required physical layer: RS-232C, RS-422A/485 multidrop, USB-to-serial adapters.
4. Whether maintaining `netstandard2.0` is mandatory for serial support or serial can be modern-target-only.

## Discriminating probe before implementation

Pick one real target PLC/serial adapter combination and confirm manually:

- PLC serial mode: Host Link or Host Link FINS.
- Unit number.
- Baud/parity/data/stop/handshake.
- A single manual read command succeeds:
  - Host Link FINS `FA` frame if supported.
  - C-mode `RD` frame if not.

This determines whether phase 1 can be Host Link FINS only, or whether C-mode must be implemented in parallel.

## Proposed implementation phases

### Phase 1 — Host Link FINS serial foundation

Files likely to change/add:

- `src/OmronPlcRx/Enums/ConnectionMethod.cs`
- `src/OmronPlcRx/Core/OmronPLCConnection.cs`
- `src/OmronPlcRx/Core/Channels/BaseChannel.cs`
- `src/OmronPlcRx/Core/Channels/SerialHostLinkFinsChannel.cs`
- `src/OmronPlcRx/Core/Serial/OmronSerialOptions.cs`
- `src/OmronPlcRx/Core/Serial/HostLinkFinsFrameCodec.cs`
- `src/OmronPlcRx/Core/Serial/HostLinkFcs.cs`
- `src/OmronPlcRx/Core/Serial/ISerialPortAdapter.cs`
- `src/OmronPlcRx/Core/Serial/SystemIoPortsSerialPortAdapter.cs`
- `src/OmronPlcRx/OmronPlcRx.csproj`
- test project under `tests/OmronPlcRx.Tests/`
- `README.md`

### Phase 2 — C-mode Host Link subset

Add only after phase 1, or if real hardware proves FINS-over-Host-Link unavailable.

Initial C-mode command subset:

- DM/CIO/WR/H/A word read/write equivalent to current tag operations.
- Bit read/write if supported cleanly for target PLCs.
- PLC mode/status/model read if needed.

### Phase 3 — Serial Gateway helpers

Expose `0x28` helpers for Ethernet/network-to-serial gateway topologies:

- CompoWay/F (`0x2803`)
- Modbus-RTU (`0x2804`)
- Modbus-ASCII (`0x2805`)
- Host Link FINS relay addressing guidance and helpers

## Verification commands

Use Windows `dotnet.exe` from WSL if WSL `dotnet` is unavailable:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build src/OmronPlcRx.sln -v minimal
"/mnt/c/Program Files/dotnet/dotnet.exe" test --project tests/OmronPlcRx.Tests/OmronPlcRx.Tests.csproj -v minimal
```

## Final recommendation

Proceed with **Phase 1: direct Host Link FINS serial transport**, using an internal serial adapter abstraction and preserving the existing FINS request/response model. Treat C-mode Host Link as a follow-up compatibility layer for PLCs that cannot handle Host Link FINS. Treat Serial Gateway as a separate network-to-serial gateway feature, not the answer to direct legacy serial communications.
