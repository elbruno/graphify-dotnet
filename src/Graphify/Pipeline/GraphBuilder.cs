using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Pipeline;

/// <summary>
/// Pipeline stage that merges multiple extraction results into a unified KnowledgeGraph.
/// Handles node deduplication, edge merging, and cross-file relationship discovery.
/// </summary>
public sealed class GraphBuilder : IPipelineStage<IReadOnlyList<ExtractionResult>, KnowledgeGraph>
{
    private readonly GraphBuilderOptions _options;

    public GraphBuilder(GraphBuilderOptions? options = null)
    {
        _options = options ?? new GraphBuilderOptions();
    }

    public Task<KnowledgeGraph> ExecuteAsync(IReadOnlyList<ExtractionResult> input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var graph = new KnowledgeGraph();
        var nodeMetadataAggregator = new Dictionary<string, List<ExtractedNode>>();
        var edgeWeightTracker = new Dictionary<EdgeKey, EdgeData>();
        var fileNodes = new HashSet<string>();
        // Map absolute file paths to relative paths for portability
        var relativePathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Phase 1: Collect all nodes and track duplicates for merging
        foreach (var extraction in input)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Track file for file-level node creation
            if (_options.CreateFileNodes && !string.IsNullOrWhiteSpace(extraction.SourceFilePath))
            {
                fileNodes.Add(extraction.SourceFilePath);
            }

            // Build mapping of absolute to relative paths
            if (!string.IsNullOrWhiteSpace(extraction.SourceFilePath) && !string.IsNullOrWhiteSpace(extraction.RelativeSourceFilePath))
            {
                relativePathMap[extraction.SourceFilePath] = extraction.RelativeSourceFilePath;
            }

            foreach (var node in extraction.Nodes)
            {
                if (!nodeMetadataAggregator.ContainsKey(node.Id))
                {
                    nodeMetadataAggregator[node.Id] = new List<ExtractedNode>();
                }
                nodeMetadataAggregator[node.Id].Add(node);
            }
        }

        // Phase 2: Merge nodes according to strategy
        foreach (var (nodeId, duplicates) in nodeMetadataAggregator)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mergedNode = MergeNodes(nodeId, duplicates, relativePathMap);
            graph.AddNode(mergedNode);
        }

        // Phase 3: Create file-level nodes if enabled
        if (_options.CreateFileNodes)
        {
            foreach (var filePath in fileNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileNodeId = $"file:{filePath}";
                if (graph.GetNode(fileNodeId) == null)
                {
                    // Resolve relative path from mapping
                    var relativePath = relativePathMap.TryGetValue(filePath, out var relPath) ? relPath : null;
                    // Normalize path separators to forward slashes for cross-platform compatibility
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        relativePath = relativePath.Replace('\\', '/');
                    }

                    var fileNode = new GraphNode
                    {
                        Id = fileNodeId,
                        Label = Path.GetFileName(filePath),
                        Type = "File",
                        FilePath = filePath,
                        RelativePath = relativePath,
                        Confidence = Confidence.Extracted,
                        Metadata = new Dictionary<string, string>
                        {
                            ["full_path"] = filePath
                        }
                    };
                    graph.AddNode(fileNode);
                }
            }
        }

        // Phase 4: Collect and merge edges
        foreach (var extraction in input)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var edge in extraction.Edges)
            {
                // Skip edges to nodes that don't exist (external/stdlib imports)
                if (graph.GetNode(edge.Source) == null || graph.GetNode(edge.Target) == null)
                {
                    continue;
                }

                var key = new EdgeKey(edge.Source, edge.Target, edge.Relation);

                if (!edgeWeightTracker.ContainsKey(key))
                {
                    edgeWeightTracker[key] = new EdgeData
                    {
                        Weight = edge.Weight,
                        Confidence = edge.Confidence,
                        SourceFile = edge.SourceFile,
                        SourceLocation = edge.SourceLocation,
                        Count = 1
                    };
                }
                else
                {
                    // Merge edges: increment weight and keep highest confidence
                    var existing = edgeWeightTracker[key];
                    existing.Weight += edge.Weight;
                    existing.Count++;
                    if (edge.Confidence < existing.Confidence) // Lower enum value = higher confidence
                    {
                        existing.Confidence = edge.Confidence;
                    }
                }
            }

            // Create "contains" edges from file nodes to entities in that file
            if (_options.CreateFileNodes && !string.IsNullOrWhiteSpace(extraction.SourceFilePath))
            {
                var fileNodeId = $"file:{extraction.SourceFilePath}";
                var fileNode = graph.GetNode(fileNodeId);

                if (fileNode != null)
                {
                    foreach (var node in extraction.Nodes)
                    {
                        var entityNode = graph.GetNode(node.Id);
                        if (entityNode != null && entityNode.Id != fileNodeId)
                        {
                            var containsKey = new EdgeKey(fileNodeId, node.Id, "contains");
                            if (!edgeWeightTracker.ContainsKey(containsKey))
                            {
                                edgeWeightTracker[containsKey] = new EdgeData
                                {
                                    Weight = 1.0,
                                    Confidence = Confidence.Extracted,
                                    SourceFile = extraction.SourceFilePath,
                                    Count = 1
                                };
                            }
                        }
                    }
                }
            }
        }

        // Phase 5: Add edges to graph (with weight filtering)
        foreach (var (key, data) in edgeWeightTracker)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (data.Weight < _options.MinEdgeWeight)
            {
                continue;
            }

            var sourceNode = graph.GetNode(key.Source);
            var targetNode = graph.GetNode(key.Target);

            if (sourceNode != null && targetNode != null)
            {
                var metadata = new Dictionary<string, string>
                {
                    ["merge_count"] = data.Count.ToString(),
                };

                if (!string.IsNullOrWhiteSpace(data.SourceFile))
                {
                    metadata["source_file"] = data.SourceFile;
                }

                if (!string.IsNullOrWhiteSpace(data.SourceLocation))
                {
                    metadata["source_location"] = data.SourceLocation;
                }

                var graphEdge = new GraphEdge
                {
                    Source = sourceNode,
                    Target = targetNode,
                    Relationship = key.Relationship,
                    Weight = data.Weight,
                    Confidence = data.Confidence,
                    Metadata = metadata
                };

                graph.AddEdge(graphEdge);
            }
        }

        return Task.FromResult(graph);
    }

    private GraphNode MergeNodes(string nodeId, List<ExtractedNode> duplicates, Dictionary<string, string> relativePathMap)
    {
        ExtractedNode selected;

        switch (_options.MergeStrategy)
        {
            case MergeStrategy.HighestConfidence:
                // For nodes extracted from AST, we don't have confidence on the node itself
                // AST nodes should be treated as Extracted, semantic nodes as Inferred
                // Since we process in order (AST first, then semantic), last = semantic = potentially richer
                // But Python logic says "semantic overwrites AST" via NetworkX add_node
                selected = duplicates.Last();
                break;

            case MergeStrategy.MostRecent:
                selected = duplicates.Last();
                break;

            case MergeStrategy.Aggregate:
                // Aggregate all metadata from all duplicates
                selected = duplicates.Last(); // Use last as base
                break;

            default:
                selected = duplicates.Last();
                break;
        }

        // Build metadata dictionary
        var metadata = new Dictionary<string, string>();

        if (_options.MergeStrategy == MergeStrategy.Aggregate)
        {
            // Merge metadata from all duplicates
            foreach (var dup in duplicates)
            {
                if (dup.Metadata != null)
                {
                    foreach (var (key, value) in dup.Metadata)
                    {
                        metadata[key] = value?.ToString() ?? string.Empty;
                    }
                }
            }
        }
        else if (selected.Metadata != null)
        {
            foreach (var (key, value) in selected.Metadata)
            {
                metadata[key] = value?.ToString() ?? string.Empty;
            }
        }

        // Add source location if present
        if (!string.IsNullOrWhiteSpace(selected.SourceLocation))
        {
            metadata["source_location"] = selected.SourceLocation;
        }

        // Add merge count if there were duplicates
        if (duplicates.Count > 1)
        {
            metadata["merge_count"] = duplicates.Count.ToString();
        }

        // Determine type from FileType enum
        var type = selected.FileType switch
        {
            FileType.Code => "Entity", // Generic code entity (could be function, class, etc.)
            FileType.Document => "Document",
            FileType.Paper => "Paper",
            FileType.Image => "Image",
            _ => "Unknown"
        };

        // Override type if metadata has more specific information
        if (metadata.TryGetValue("type", out var specificType))
        {
            type = specificType;
        }

        // Resolve relative path from mapping
        var relativePath = relativePathMap.TryGetValue(selected.SourceFile, out var relPath) ? relPath : null;
        // Normalize path separators to forward slashes for cross-platform compatibility
        if (!string.IsNullOrEmpty(relativePath))
        {
            relativePath = relativePath.Replace('\\', '/');
        }

        return new GraphNode
        {
            Id = nodeId,
            Label = selected.Label,
            Type = type,
            FilePath = selected.SourceFile,
            RelativePath = relativePath,
            Confidence = Confidence.Extracted, // Default to Extracted (AST-based)
            Metadata = metadata
        };
    }

    private record EdgeKey(string Source, string Target, string Relationship);

    private class EdgeData
    {
        public double Weight { get; set; }
        public Confidence Confidence { get; set; }
        public string? SourceFile { get; set; }
        public string? SourceLocation { get; set; }
        public int Count { get; set; }
    }
}
