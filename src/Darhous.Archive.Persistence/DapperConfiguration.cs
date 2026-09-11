using System.Runtime.CompilerServices;

namespace Darhous.Archive.Persistence;

/// <summary>
/// Every table in the DB Spec uses snake_case columns; every C# DTO uses PascalCase.
/// Set once, globally, so no query anywhere in this project needs an `AS PascalCase` alias.
/// </summary>
internal static class DapperConfiguration
{
#pragma warning disable CA2255 // deliberate: this library is the composition point for all
    // Dapper mapping in the app (Desktop + every Worker reference it), not a shared
    // component consumed by unrelated libraries, so "runs once when this assembly loads"
    // has no surprising cross-consumer effect here.
    [ModuleInitializer]
    internal static void Configure() => Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
#pragma warning restore CA2255
}
