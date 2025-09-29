# OmronPlcRx

A Reactive Omron PLC communications library for .NET (`netstandard2.0`, `net8.0`, `net9.0`, `net10.0`).

OmronPlcRx provides a high-level, reactive, strongly typed interface for interacting with Omron PLCs over the FINS protocol using TCP or UDP. It handles:

- Connection setup & initialization (controller model / version discovery)
- Bit and word memory area reads & writes
- PLC clock & scan cycle time access (now exposed via public wrapper methods)
- Reactive polling of configured PLC tag addresses
- Type handling for: `bool`, `byte`, `short`, `ushort`, `int` (2 words), `uint` (2 words), `float` (2 words IEEE 754), `double` (4 words IEEE 754), `string` (variable length, ASCII, packed 2 chars/word), BCD numeric wrappers: `Bcd16`, `BcdU16` (1 word), `Bcd32`, `BcdU32` (2 words)
- Error propagation via reactive streams
- Automatic PLC capability limits (read/write length, area availability)

Contents:
- Features
- Installation
- Quick Start
- Addressing Guide
- Supported Types & Encoding
- BCD Types
- Reactive Tag & System API
- Code Samples
- Direct Read/Write Core API Overview
- PLC Clock & Cycle Time
- Error Handling
- PLC Types & Limits
- FAQ
- Contributing
- License / Disclaimer

---
## Features
- TCP or UDP transport selection
- Automatic controller identification (model, version, PLC type classification)
- Reactive `IObservable<T>` streams per tag and an aggregate stream of all tag changes
- Background polling loop with configurable interval (default 100 ms)
- Safe concurrent access with internal caching
- Strong typing for tag values; built-in conversion for common primitives & BCD wrappers
- Bit addressing (e.g. `D10.3`, `W100.7`, `CIO200.0`)
- Word + multi‑word (32/64‑bit) assembly (High word first for multi-word numerics)
- Public accessors for PLC real-time clock & scan cycle time (`ReadClockAsync`, `WriteClockAsync`, `ReadCycleTimeAsync`)
- Exception surfacing via `Errors` observable
- Targets legacy & modern runtimes

---
## Installation
NuGet:
```
dotnet add package OmronPlcRx
```
OR (Package Manager):
```
Install-Package OmronPlcRx
```
---
## Quick Start
```csharp
using System;
using System.Reactive.Linq;
using OmronPlcRx;
using OmronPlcRx.Enums;
using OmronPlcRx.Core.Types; // BCD wrappers

class Program
{
    static async Task Main()
    {
        var plc = new OmronPlcRx.OmronPlcRx(
            localNodeId: 11,
            remoteNodeId: 1,
            connectionMethod: ConnectionMethod.UDP,
            remoteHost: "192.168.250.1",
            port: 9600,
            timeout: 2000,
            retries: 1,
            pollInterval: TimeSpan.FromMilliseconds(200));

        // Register tags (variable = PLC address, tagName = logical name)
        plc.AddUpdateTagItem<bool>("D100.0", "MotorRun");
        plc.AddUpdateTagItem<short>("D200", "TemperatureRaw");
        plc.AddUpdateTagItem<int>("D300", "BatchCounter");          // D300,D301
        plc.AddUpdateTagItem<float>("D400", "TankLevel");            // D400,D401
        plc.AddUpdateTagItem<double>("D500", "TotalizedFlow");       // D500..D503
        plc.AddUpdateTagItem<string>("D600[20]", "LineName");        // 20 ASCII chars max (10 words)
        plc.AddUpdateTagItem<Bcd16>("D700", "BcdTemp");              // 1 word BCD
        plc.AddUpdateTagItem<Bcd32>("D710", "BcdCount");             // 2 word BCD
        plc.AddUpdateTagItem<uint>("D720", "UnsignedCounter");       // 2 words
        plc.AddUpdateTagItem<byte>("D730", "SmallFlagByte");         // stored in low byte of word

        // Observe single tag
        var sub1 = plc.Observe<bool>("MotorRun")
            .DistinctUntilChanged()
            .Subscribe(v => Console.WriteLine($"MotorRun -> {v}"));

        // Observe all tag changes
        var subAll = plc.ObserveAll
            .Subscribe(tag => Console.WriteLine($"Tag {tag?.TagName} = {tag?.Value}"));

        // Observe errors
        var errSub = plc.Errors.Subscribe(e => Console.WriteLine($"ERROR: {e?.Message}"));

        // Write (async fire-and-forget)
        plc.Value("MotorRun", true);
        plc.Value("LineName", "Filling Line 1");
        plc.Value("BcdTemp", new Bcd16(235)); // writes BCD 0235

        // Read last cached value
        var tempRaw = plc.Value<short>("TemperatureRaw");
        Console.WriteLine($"Temp raw = {tempRaw}");

        // Direct clock & cycle time access
        var clock = await plc.ReadClockAsync();
        Console.WriteLine($"PLC Clock: {clock.Clock:o}");
        var cycle = await plc.ReadCycleTimeAsync();
        Console.WriteLine($"Cycle Times: Min={cycle.MinimumCycleTime} Max={cycle.MaximumCycleTime} Avg={cycle.AverageCycleTime}");

        Console.ReadKey();
        plc.Dispose();
    }
}
```

---
## Addressing Guide
Supported memory area prefixes:
- `D` / `DM` : Data Memory (bits & words)
- `C` / `CIO` : Common IO area
- `W` : Work area
- `H` : Holding area
- `A` : Auxiliary area (restricted on some models)

Bit address syntax: `<Area><Word>.<Bit>` where Bit = 0..15  
Examples: `D100.0`, `W50.7`, `CIO200.15`

Word address syntax: `<Area><Word>`  
Examples: `D200`, `H10`, `CIO500`

String address syntax: `<Area><Word>[<Length>]` (length in characters). Example: `D600[20]` reserves 20 ASCII chars (10 words). If no length specified, default = 16 chars. Strings are ASCII, packed 2 chars per word (high-byte then low-byte). Null padding is applied if shorter.

---
## Supported Types & Encoding
| Type | Words | Notes |
|------|-------|-------|
| bool | 1 bit or 1 word | If bit address used reads single bit; if word address treats non-zero as true |
| byte | 1 word | Stored in low byte (high byte = 0). Read masks low 8 bits |
| short | 1 word | Signed 16-bit |
| ushort | 1 word | Unsigned 16-bit |
| int | 2 words | High word first |
| uint | 2 words | High word first |
| float | 2 words | IEEE 754 single; word order high/low; bytes swapped for host endianness |
| double | 4 words | IEEE 754 double; high word first |
| string | N words | ASCII, 2 chars per word, length via `[len]` suffix (default 16) |
| Bcd16 / BcdU16 | 1 word | Packed BCD (4 digits max) |
| Bcd32 / BcdU32 | 2 words | Packed BCD (8 digits max) |

Multi-word numeric order: the library consistently treats the first configured word as the high-order word for 32/64-bit and BCD 32-bit values.

---
## BCD Types
BCD wrappers provide type safety and avoid ambiguity with standard binary-coded integer storage.

- `Bcd16` : signed 16-bit decimal (1 word)  
- `BcdU16`: unsigned 16-bit decimal (1 word)  
- `Bcd32` : signed 32-bit decimal (2 words, high word first)  
- `BcdU32`: unsigned 32-bit decimal (2 words)  

Example:
```csharp
plc.AddUpdateTagItem<Bcd32>("D800", "BatchNumber");
plc.Observe<Bcd32>("BatchNumber")
   .Subscribe(v => Console.WriteLine($"BatchNumber -> {v?.Value}"));
plc.Value("BatchNumber", new Bcd32(12345678));
```

---
## Reactive Tag & System API (`IOmronPlcRx`)
Methods / Properties:
- Tag management: `AddUpdateTagItem<T>(variable, tagName)`
- Tag observation: `IObservable<T?> Observe<T>(tagName)`
- Aggregated tag stream: `IObservable<IPlcTag?> ObserveAll`
- Cached value access: `T? Value<T>(tagName)`
- Async write (fire & forget): `Value<T>(tagName, value)`
- Error stream: `IObservable<OmronPLCException?> Errors`
- Disposal state: `bool IsDisposed`
- PLC identity: `PLCType PLCType`, `ControllerModel`, `ControllerVersion`
- Clock / cycle time: `ReadClockAsync()`, `WriteClockAsync(DateTime)`, `WriteClockAsync(DateTime,int)`, `ReadCycleTimeAsync()`

Clock/cycle methods return strongly typed result structs (Bytes/Packets sent/received, Duration and payload data).

---
## Code Samples
1. Mirror / inverted control
```csharp
plc.AddUpdateTagItem<bool>("D10.0", "SourceFlag");
plc.AddUpdateTagItem<bool>("D11.0", "TargetFlag");
plc.Observe<bool>("SourceFlag")
   .DistinctUntilChanged()
   .Subscribe(v => plc.Value("TargetFlag", !v));
```

2. Unsigned counter & rollover detection
```csharp
plc.AddUpdateTagItem<uint>("D300", "PulseCounter");
plc.Observe<uint>("PulseCounter")
   .Pairwise()
   .Subscribe(p =>
   {
       var (prev, curr) = (p.FirstOrDefault(), p.Last());
       if (curr < prev) Console.WriteLine("Counter rollover detected");
   });
```

3. BCD production count
```csharp
plc.AddUpdateTagItem<Bcd32>("D500", "ProdCount");
plc.Observe<Bcd32>("ProdCount")
   .DistinctUntilChanged()
   .Subscribe(v => Console.WriteLine($"Production Count = {v?.Value}"));
```

4. Double precision accumulation
```csharp
plc.AddUpdateTagItem<double>("D600", "EnergyKWh");
plc.Observe<double>("EnergyKWh")
   .Sample(TimeSpan.FromSeconds(5))
   .Subscribe(v => Console.WriteLine($"Energy = {v:F3} kWh"));
```

5. ASCII string with fixed length
```csharp
plc.AddUpdateTagItem<string>("D700[16]", "OperatorName");
plc.Value("OperatorName", "ALICE");
plc.Observe<string>("OperatorName")
   .DistinctUntilChanged()
   .Subscribe(n => Console.WriteLine($"Operator = {n}"));
```

6. Byte & UShort handling
```csharp
plc.AddUpdateTagItem<byte>("D720", "StatusByte");
plc.AddUpdateTagItem<ushort>("D721", "RawWord");
plc.Value("StatusByte", (byte)0x3A);
plc.Value("RawWord", (ushort)1234);
```

7. Reading the PLC clock & writing adjustments
```csharp
var clockResult = await plc.ReadClockAsync();
Console.WriteLine($"Clock: {clockResult.Clock:o}");
await plc.WriteClockAsync(DateTime.UtcNow); // sync PLC clock to current time
```

8. Cycle time monitoring
```csharp
var cycle = await plc.ReadCycleTimeAsync();
Console.WriteLine($"Scan Cycle ms -> Min:{cycle.MinimumCycleTime} Max:{cycle.MaximumCycleTime} Avg:{cycle.AverageCycleTime}");
```

---
## Direct Read/Write Core API Overview
The internal `OmronPLCConnection` (used by the reactive layer) exposes low-level async methods: `ReadBitsAsync`, `ReadWordsAsync`, `WriteBitsAsync`, `WriteWordsAsync`, `ReadClockAsync`, `WriteClockAsync`, `ReadCycleTimeAsync`.

Extend by adding new FINS request/response types under `Core/Requests` & `Core/Responses`.

---
## PLC Clock & Cycle Time
Clock & cycle time can be read/written via public async methods:
```csharp
var clock = await plc.ReadClockAsync();
await plc.WriteClockAsync(DateTime.UtcNow);
var cycle = await plc.ReadCycleTimeAsync();
```
Returned structs include transmission statistics and payload data.

---
## Error Handling
Operational exceptions (initialization failure, invalid address, read/write errors, unsupported type) are pushed into `Errors`:
```csharp
plc.Errors.Subscribe(e => Console.WriteLine($"[PLC ERR] {e?.Message}"));
```
Write errors also surface here because `Value<T>(...)` is fire-and-forget.

---
## PLC Types & Limits
Controller type is inferred automatically. Impacts maximum read/write word lengths & area availability.

`PLCType` enum includes: `CP1`, `CJ2`, `NJ101`, `NJ301`, `NJ501`, `NX1P2`, `NX102`, `NX701`, `NY512`, `NY532`, `NJ_NX_NY_Series`, `C_Series`, `Unknown`.

---
## FAQ
Q: Multi-word numeric reads seem incorrect.  
A: Ensure the base word matches how the PLC stores multi-word values. Library expects high word first.

Q: String garbage characters?  
A: Verify length spec `[len]` matches actual reserved word space and encoding is ASCII.

Q: BCD value off?  
A: Confirm the PLC really stores the value in packed BCD and digits do not exceed capacity (4 or 8 digits).

Q: Change polling interval after creation?  
A: Not currently. Dispose and recreate with new interval.

Q: Add new areas (e.g., EM / AR)?  
A: Extend mapping logic in `OmronPlcRx`.

---
## Contributing
PRs welcome. Ideas:
- Additional data types (arrays, DateTime wrapper enhancements)
- Auto-reconnect & health monitoring
- Structured logging integration hooks

---
## License
MIT License — see `LICENSE`.

---
## Disclaimer
Not affiliated with Omron. Use at your own risk. Validate in a safe test environment before deployment to production systems.

---
**OmronPlcRx** - Empowering Industrial Automation with Reactive Technology ⚡🏭
