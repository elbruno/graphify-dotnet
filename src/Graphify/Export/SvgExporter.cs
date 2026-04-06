using System.Text;
using System.Xml;
using Graphify.Graph;

namespace Graphify.Export;

/// <summary>
/// Exports knowledge graph as a simple SVG visualization.
/// Uses a force-directed-like layout with nodes colored by community.
/// </summary>
public sealed class SvgExporter : IGraphExporter
{
    private const int Width = 1600;
    private const int Height = 1200;
    private const int NodeRadius = 8;
    private const int Padding = 50;

    public string Format => "svg";

    public async Task ExportAsync(KnowledgeGraph graph, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var nodes = graph.GetNodes().ToList();
        if (nodes.Count == 0)
        {
            await File.WriteAllTextAsync(outputPath, GenerateEmptySvg(), cancellationToken);
            return;
        }

        // Calculate node positions using simple force-directed layout
        var positions = CalculateLayout(graph, nodes);

        // Generate SVG
        var svg = GenerateSvg(graph, nodes, positions);
        await File.WriteAllTextAsync(outputPath, svg, cancellationToken);
    }

    private static Dictionary<string, (double X, double Y)> CalculateLayout(KnowledgeGraph graph, List<Models.GraphNode> nodes)
    {
        var positions = new Dictionary<string, (double X, double Y)>();
        var random = new Random(42); // Fixed seed for reproducibility

        // Initialize with random positions
        foreach (var node in nodes)
        {
            positions[node.Id] = (
                random.NextDouble() * (Width - 2 * Padding) + Padding,
                random.NextDouble() * (Height - 2 * Padding) + Padding
            );
        }

        // Simple force-directed iterations
        const int iterations = 100;
        const double k = 50.0; // Ideal spring length
        const double damping = 0.9;

        for (int iter = 0; iter < iterations; iter++)
        {
            var forces = new Dictionary<string, (double Fx, double Fy)>();

            // Initialize forces
            foreach (var node in nodes)
            {
                forces[node.Id] = (0.0, 0.0);
            }

            // Repulsive forces between all nodes
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var node1 = nodes[i];
                    var node2 = nodes[j];

                    var pos1 = positions[node1.Id];
                    var pos2 = positions[node2.Id];

                    var dx = pos2.X - pos1.X;
                    var dy = pos2.Y - pos1.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < 1.0) dist = 1.0;

                    var force = k * k / dist;
                    var fx = force * dx / dist;
                    var fy = force * dy / dist;

                    var f1 = forces[node1.Id];
                    var f2 = forces[node2.Id];
                    forces[node1.Id] = (f1.Fx - fx, f1.Fy - fy);
                    forces[node2.Id] = (f2.Fx + fx, f2.Fy + fy);
                }
            }

            // Attractive forces along edges
            foreach (var edge in graph.GetEdges())
            {
                var pos1 = positions[edge.Source.Id];
                var pos2 = positions[edge.Target.Id];

                var dx = pos2.X - pos1.X;
                var dy = pos2.Y - pos1.Y;
                var dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < 1.0) dist = 1.0;

                var force = dist * dist / k;
                var fx = force * dx / dist;
                var fy = force * dy / dist;

                var f1 = forces[edge.Source.Id];
                var f2 = forces[edge.Target.Id];
                forces[edge.Source.Id] = (f1.Fx + fx, f1.Fy + fy);
                forces[edge.Target.Id] = (f2.Fx - fx, f2.Fy - fy);
            }

            // Apply forces with damping
            var maxForce = forces.Values.Max(f => Math.Sqrt(f.Fx * f.Fx + f.Fy * f.Fy));
            if (maxForce < 0.1) break; // Converged

            foreach (var node in nodes)
            {
                var force = forces[node.Id];
                var pos = positions[node.Id];

                var newX = pos.X + force.Fx * damping;
                var newY = pos.Y + force.Fy * damping;

                // Keep within bounds
                newX = Math.Max(Padding, Math.Min(Width - Padding, newX));
                newY = Math.Max(Padding, Math.Min(Height - Padding, newY));

                positions[node.Id] = (newX, newY);
            }
        }

        return positions;
    }

    private static string GenerateSvg(KnowledgeGraph graph, List<Models.GraphNode> nodes, Dictionary<string, (double X, double Y)> positions)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Width}\" height=\"{Height}\" viewBox=\"0 0 {Width} {Height}\">");
        
        // Background
        sb.AppendLine($"  <rect width=\"{Width}\" height=\"{Height}\" fill=\"#ffffff\"/>");
        
        // Styles
        sb.AppendLine("  <style>");
        sb.AppendLine("    .edge { stroke: #999; stroke-width: 1; stroke-opacity: 0.6; }");
        sb.AppendLine("    .node { stroke: #fff; stroke-width: 2; }");
        sb.AppendLine("    .label { font-family: Arial, sans-serif; font-size: 10px; fill: #333; }");
        sb.AppendLine("  </style>");
        
        // Draw edges first (so they appear behind nodes)
        sb.AppendLine("  <g id=\"edges\">");
        foreach (var edge in graph.GetEdges())
        {
            if (positions.TryGetValue(edge.Source.Id, out var pos1) && 
                positions.TryGetValue(edge.Target.Id, out var pos2))
            {
                sb.AppendLine($"    <line class=\"edge\" x1=\"{pos1.X:F2}\" y1=\"{pos1.Y:F2}\" x2=\"{pos2.X:F2}\" y2=\"{pos2.Y:F2}\"/>");
            }
        }
        sb.AppendLine("  </g>");
        
        // Draw nodes
        sb.AppendLine("  <g id=\"nodes\">");
        
        foreach (var node in nodes)
        {
            if (!positions.TryGetValue(node.Id, out var pos)) continue;
            
            var color = GetCommunityColor(node.Community);
            var label = node.Label ?? node.Id;
            var degree = graph.GetDegree(node.Id);
            var radius = NodeRadius + Math.Min(degree / 5.0, 10); // Scale by degree
            
            // Node circle
            sb.AppendLine($"    <circle class=\"node\" cx=\"{pos.X:F2}\" cy=\"{pos.Y:F2}\" r=\"{radius:F2}\" fill=\"{color}\">");
            sb.AppendLine($"      <title>{EscapeXml(label)} ({degree} connections)</title>");
            sb.AppendLine("    </circle>");
            
            // Label for high-degree nodes
            if (degree > 10)
            {
                var labelY = pos.Y + radius + 12;
                sb.AppendLine($"    <text class=\"label\" x=\"{pos.X:F2}\" y=\"{labelY:F2}\" text-anchor=\"middle\">{EscapeXml(TruncateLabel(label))}</text>");
            }
        }
        
        sb.AppendLine("  </g>");
        
        // Legend
        sb.AppendLine("  <g id=\"legend\">");
        sb.AppendLine($"    <text x=\"20\" y=\"30\" font-family=\"Arial\" font-size=\"14\" font-weight=\"bold\">Knowledge Graph</text>");
        sb.AppendLine($"    <text x=\"20\" y=\"50\" font-family=\"Arial\" font-size=\"12\" fill=\"#666\">{nodes.Count} nodes · {graph.EdgeCount} edges</text>");
        sb.AppendLine("  </g>");
        
        sb.AppendLine("</svg>");
        
        return sb.ToString();
    }

    private static string GenerateEmptySvg()
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<svg xmlns=""http://www.w3.org/2000/svg"" width=""{Width}"" height=""{Height}"" viewBox=""0 0 {Width} {Height}"">
  <rect width=""{Width}"" height=""{Height}"" fill=""#ffffff""/>
  <text x=""{Width / 2}"" y=""{Height / 2}"" text-anchor=""middle"" font-family=""Arial"" font-size=""24"" fill=""#999"">
    Empty Graph
  </text>
</svg>";
    }

    private static string GetCommunityColor(int? communityId)
    {
        if (!communityId.HasValue) return "#cccccc";
        
        // Generate distinct colors for communities
        var colors = new[]
        {
            "#4285F4", "#EA4335", "#FBBC04", "#34A853", "#FF6D00",
            "#9C27B0", "#00BCD4", "#8BC34A", "#FF5722", "#795548",
            "#607D8B", "#E91E63", "#3F51B5", "#009688", "#FFC107"
        };
        
        return colors[communityId.Value % colors.Length];
    }

    private static string TruncateLabel(string label, int maxLength = 20)
    {
        if (label.Length <= maxLength) return label;
        return label.Substring(0, maxLength - 3) + "...";
    }

    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
