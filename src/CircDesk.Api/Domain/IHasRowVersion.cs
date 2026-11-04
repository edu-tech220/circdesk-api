namespace CircDesk.Api.Domain;

/// <summary>
/// Marks an entity with an app-managed concurrency token. Native SQL Server
/// rowversion columns aren't portable to SQLite, so CircDeskDbContext stamps a
/// fresh value on every update itself, giving identical optimistic-concurrency
/// behavior on both providers.
/// </summary>
public interface IHasRowVersion
{
    byte[] RowVersion { get; set; }
}
