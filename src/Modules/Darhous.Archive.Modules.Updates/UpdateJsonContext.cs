using System.Text.Json.Serialization;
using Darhous.Archive.Modules.Updates.Packaging;
using Darhous.Archive.Modules.Updates.Sources;

namespace Darhous.Archive.Modules.Updates;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UpdateFeedManifest))]
[JsonSerializable(typeof(UpdatePackageManifest))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(UpdateRollbackManifest))]
[JsonSerializable(typeof(UpdateJobPayload))]
internal sealed partial class UpdateJsonContext : JsonSerializerContext;
