namespace Darhous.Archive.Contracts.Commands;

/// <summary>
/// Marker for a command (write intent) that produces no result value.
/// Implementation Plan §8: Contracts holds Commands/Queries shapes, never business logic.
/// </summary>
public interface ICommand;

/// <summary>
/// Marker for a command (write intent) that produces <typeparamref name="TResult"/>.
/// </summary>
public interface ICommand<TResult>;
