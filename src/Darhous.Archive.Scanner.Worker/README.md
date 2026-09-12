# Scanner worker — Phase 14 Part A

Windows console executable targeting .NET 10 Windows Desktop (framework-dependent).
Only `NAPS2.Sdk` 1.3.0 is added as a direct package. `UseWindowsForms` supplies
System.Drawing for the small local image backend; it does not create an application UI.
NAPS2 1.3.0 is a stable NuGet release but marks its APIs as preview. Scoped opt-in
annotations are confined to the adapter/backend/entrypoint; scanner DTOs/session do not
require callers to opt into preview features.

## Launch contract for Part B

Set these environment variables using the existing supervisor's EnvironmentVariables:

- `DARHOUS_WORKER_PIPE_NAME`: host-created current-user named pipe.
- `DARHOUS_WORKER_SESSION_TOKEN`: token from WorkerHandshakeHost. Never a CLI argument.
- `DARHOUS_WORKER_TEMP_ROOT`: optional absolute managed temporary root shared with the
  host; otherwise WorkerProtocolOptions' default is used.

The token environment variable is cleared after reading. Pipe connection is bounded by
the existing ten-second handshake timeout. Worker ID is
`Darhous.Archive.Scanner.Worker`; worker version is `1.0.0`.
The worker sends the seven existing handshake frames, unchanged. No host acknowledgement
is invented. Startup health is `healthy` with devices or `degraded` without them. The
capability list describes implemented operations, not capabilities of a connected model.
Devices are enumerated again for each scan so later connection of a scanner works.

## Wire contract v1

Use the Phase 13 envelope, framing and transport. Envelope names are camelCase.
**Payload names are PascalCase**, following the actual Phase 13 default serializer
(the execution-plan table uses descriptive camelCase). Enums serialize as strings.
Unknown or incorrectly cased profile properties fail validation.

`scan.request` payload (all properties optional, defaults shown):

```json
{
  "DeviceId": null,
  "Resolution": 300,
  "ColorMode": "Color",
  "Duplex": false,
  "Source": "Adf",
  "Separation": "FullBatch",
  "PagesPerFile": 1,
  "PageSize": "A4"
}
```

ColorMode: Color / Grayscale / BlackWhite. Source: Adf / Flatbed.
Separation: FullBatch / EveryPage / EveryTwoPages / EveryNPages.
PageSize: A4 / Letter / Legal. DPI: 75–1200. N: 1–1000 (only relevant for EveryNPages).
Flatbed plus duplex is rejected. Null DeviceId selects the first device, WIA before
same-architecture TWAIN. Explicit IDs use `Driver:ID` from IScannerEngine enumeration.
There is no device-enumeration IPC message in this phase's requested catalog.

Each valid request receives exactly one `scan.result`, with a fresh requestId and
correlationId equal to the request's requestId:

- Success: the existing LargeDataReference payload, `{"Path":"absolute path"}`.
  One output is a PDF. Multiple separated outputs are an ordered ZIP containing
  `document-0001.pdf`, `document-0002.pdf`, etc. The final short group is retained.
  Thus there is no separate completion message and no inline scanned content.
- Failure: `{"Code":"no_scanner","Message":"..."}`. Codes are no_scanner,
  no_pages, invalid_profile, device_error, cancelled. Test for Code before decoding
  a path reference. A failed batch returns no partial successful output.

Unknown message types or wrong protocol versions receive `protocol.error` with
`{"Code":"unsupported_message","Message":"..."}` and the connection stays usable.
Malformed framing/EOF closes the session. The worker handles one request at a time.
SDK cancellation produces a cancelled result if the connection is still usable;
process shutdown cancels the session without promising a final result. No cancel IPC
message is defined. Exit codes: 0 session end/shutdown, 1 startup/transport failure,
2 missing startup environment.

## Storage and limitations

Each request gets a random subdirectory under the managed root. Failed batch cleanup is
best effort. Successful subdirectories (including component PDFs of ZIPs) belong to the
host, which must validate, import, and clean up the whole batch directory. Host crash,
send failure or forced termination may orphan successful output. No retention sweeper.
The inherited path validator is lexical, not a symlink/reparse-point security boundary.

No physical scanner or TWAIN/WIA driver was exercised. Driver failures during enumeration
are treated as unavailable; native hangs cannot reliably be interrupted. Real hardware
confirmation remains required. No drivers or missing-driver workarounds are installed.
64-bit TWAIN needs a 64-bit DSM/data source; 32-bit-only drivers require future deployment
work, not an automatic bridge download. WIA does not have that TWAIN bitness limitation.

The one-package constraint means the standard NAPS2 image backend is not included. The
local backend supports acquisition image decoding, basic color conversion and PDF
export. It is not a replacement for the full upstream image-processing backend:
deskew/blank-page removal are not exposed, unsupported transforms fail explicitly,
JPEG quality hints are not honored, and full-batch pages remain in memory until export.
There is no UI, profile DB, plugin, supervision or OCR implementation here.
