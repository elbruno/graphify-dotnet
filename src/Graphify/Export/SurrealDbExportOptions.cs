namespace Graphify.Export;

/// <summary>
/// Connection settings for exporting a knowledge graph to SurrealDB.
/// When <see cref="Endpoint"/> is set, the exporter uses remote mode; otherwise
/// it falls back to embedded (RocksDB) mode. Lives in the core project so
/// <see cref="Graphify.Pipeline.WatchMode"/> can drive SurrealDB exports without
/// depending on the CLI's configuration types.
/// </summary>
public sealed record SurrealDbExportOptions(
    string? Endpoint = null,
    string? Username = null,
    string? Password = null,
    string? Namespace = null,
    string? Database = null);
