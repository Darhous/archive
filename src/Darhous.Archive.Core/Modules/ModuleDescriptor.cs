namespace Darhous.Archive.Core.Modules;

/// <summary>
/// Identity of one registered module (an official Module under <c>Modules/</c>, or later
/// a third-party plugin once the Plugin Host lands in Phase 12).
/// </summary>
public sealed record ModuleDescriptor(string Id, string Name, Version Version);
