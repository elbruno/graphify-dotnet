using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Graphify.Ingest;

/// <summary>
/// Fetches and processes content from URLs (web pages, papers, social media).
/// Converts HTML to plain text and returns markdown-formatted content ready for extraction.
/// </summary>
public sealed class UrlIngester : IDataIngester
{
    private readonly HttpClient _httpClient;

    public UrlIngester(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Set a user agent to avoid blocking
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "graphify-dotnet/1.0");
        }
    }

    /// <summary>
    /// Fetch content from a URL and return it as formatted markdown.
    /// </summary>
    public async Task<string> IngestAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        ValidateUrl(url);

        var urlType = DetectUrlType(url);

        return urlType switch
        {
            UrlType.ArxivPaper => await FetchArxivPaperAsync(url, cancellationToken),
            UrlType.GitHubRepo => await FetchGitHubRepoAsync(url, cancellationToken),
            UrlType.Webpage => await FetchWebpageAsync(url, cancellationToken),
            _ => await FetchWebpageAsync(url, cancellationToken)
        };
    }

    /// <summary>
    /// Ingest content from a URL and save it to a file.
    /// </summary>
    public async Task<string> IngestToFileAsync(
        string url, 
        string outputDirectory,
        string? author = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var content = await IngestAsync(url, cancellationToken);
        
        // Add contributor metadata if provided
        if (!string.IsNullOrWhiteSpace(author))
        {
            content = AddContributorMetadata(content, author);
        }

        var filename = GenerateSafeFilename(url) + ".md";
        var outputPath = Path.Combine(outputDirectory, filename);

        // Avoid overwriting - append counter if needed
        var counter = 1;
        while (File.Exists(outputPath))
        {
            var stem = Path.GetFileNameWithoutExtension(filename);
            outputPath = Path.Combine(outputDirectory, $"{stem}_{counter}.md");
            counter++;
        }

        await File.WriteAllTextAsync(outputPath, content, cancellationToken);
        return outputPath;
    }

    private async Task<string> FetchWebpageAsync(string url, CancellationToken cancellationToken)
    {
        var html = await _httpClient.GetStringAsync(url, cancellationToken);
        
        // Extract title
        var title = ExtractTitle(html) ?? url;
        
        // Convert to markdown
        var markdown = HtmlToMarkdown(html);
        
        // Generate frontmatter
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"source_url: {url}");
        sb.AppendLine("type: webpage");
        sb.AppendLine($"title: \"{EscapeYaml(title)}\"");
        sb.AppendLine($"captured_at: {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"Source: {url}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        // Limit content size (12KB max)
        var contentLimit = Math.Min(markdown.Length, 12000);
        sb.Append(markdown.Substring(0, contentLimit));
        
        if (markdown.Length > contentLimit)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("*[Content truncated]*");
        }

        return sb.ToString();
    }

    private async Task<string> FetchArxivPaperAsync(string url, CancellationToken cancellationToken)
    {
        // Extract arXiv ID
        var arxivIdMatch = Regex.Match(url, @"(\d{4}\.\d{4,5})");
        if (!arxivIdMatch.Success)
        {
            return await FetchWebpageAsync(url, cancellationToken);
        }

        var arxivId = arxivIdMatch.Groups[1].Value;
        var absUrl = $"https://export.arxiv.org/abs/{arxivId}";

        try
        {
            var html = await _httpClient.GetStringAsync(absUrl, cancellationToken);
            
            // Extract paper details
            var title = ExtractArxivTitle(html) ?? arxivId;
            var authors = ExtractArxivAuthors(html);
            var abstractText = ExtractArxivAbstract(html);

            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"source_url: {url}");
            sb.AppendLine($"arxiv_id: {arxivId}");
            sb.AppendLine("type: paper");
            sb.AppendLine($"title: \"{EscapeYaml(title)}\"");
            
            if (!string.IsNullOrWhiteSpace(authors))
            {
                sb.AppendLine($"paper_authors: \"{EscapeYaml(authors)}\"");
            }
            
            sb.AppendLine($"captured_at: {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            
            if (!string.IsNullOrWhiteSpace(authors))
            {
                sb.AppendLine($"**Authors:** {authors}");
            }
            
            sb.AppendLine($"**arXiv:** {arxivId}");
            sb.AppendLine();
            sb.AppendLine("## Abstract");
            sb.AppendLine();
            sb.AppendLine(abstractText);
            sb.AppendLine();
            sb.AppendLine($"Source: {url}");

            return sb.ToString();
        }
        catch
        {
            return await FetchWebpageAsync(url, cancellationToken);
        }
    }

    private async Task<string> FetchGitHubRepoAsync(string url, CancellationToken cancellationToken)
    {
        // For GitHub repos, fetch README
        var repoMatch = Regex.Match(url, @"github\.com/([^/]+)/([^/]+)");
        if (!repoMatch.Success)
        {
            return await FetchWebpageAsync(url, cancellationToken);
        }

        var owner = repoMatch.Groups[1].Value;
        var repo = repoMatch.Groups[2].Value.TrimEnd('/');

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"source_url: {url}");
        sb.AppendLine("type: github_repo");
        sb.AppendLine($"github_owner: {owner}");
        sb.AppendLine($"github_repo: {repo}");
        sb.AppendLine($"captured_at: {DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {owner}/{repo}");
        sb.AppendLine();
        sb.AppendLine($"GitHub Repository: {url}");
        sb.AppendLine();
        sb.AppendLine("*Use GitHub API or clone the repository for full code analysis.*");

        return sb.ToString();
    }

    private static UrlType DetectUrlType(string url)
    {
        var lower = url.ToLowerInvariant();
        
        if (lower.Contains("arxiv.org"))
            return UrlType.ArxivPaper;
        
        if (lower.Contains("github.com"))
            return UrlType.GitHubRepo;
        
        return UrlType.Webpage;
    }

    private static void ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid URL: {url}", nameof(url));
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException($"URL must use HTTP or HTTPS: {url}", nameof(url));
        }

        // Security: block local/private IPs
        if (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host.StartsWith("192.168.") || uri.Host.StartsWith("10."))
        {
            throw new ArgumentException($"Cannot fetch from local/private network: {url}", nameof(url));
        }
    }

    private static string HtmlToMarkdown(string html)
    {
        // Remove script and style tags
        var text = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Extract main content (heuristic: look for article, main, or large content divs)
        var articleMatch = Regex.Match(text, @"<article[^>]*>(.*?)</article>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (articleMatch.Success)
        {
            text = articleMatch.Groups[1].Value;
        }
        else
        {
            var mainMatch = Regex.Match(text, @"<main[^>]*>(.*?)</main>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (mainMatch.Success)
            {
                text = mainMatch.Groups[1].Value;
            }
        }
        
        // Convert headers
        text = Regex.Replace(text, @"<h1[^>]*>(.*?)</h1>", "\n# $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h2[^>]*>(.*?)</h2>", "\n## $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<h3[^>]*>(.*?)</h3>", "\n### $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Convert paragraphs
        text = Regex.Replace(text, @"<p[^>]*>(.*?)</p>", "$1\n\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Convert breaks
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        
        // Remove remaining tags
        text = Regex.Replace(text, @"<[^>]+>", " ");
        
        // Clean up whitespace
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n\n\n+", "\n\n");
        text = text.Trim();
        
        return text;
    }

    private static string? ExtractTitle(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        return null;
    }

    private static string? ExtractArxivTitle(string html)
    {
        var match = Regex.Match(html, @"class=""title[^""]*""[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, @"<[^>]+>", "").Trim();
            title = Regex.Replace(title, @"\s+", " ");
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        return null;
    }

    private static string ExtractArxivAuthors(string html)
    {
        var match = Regex.Match(html, @"class=""authors""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
        {
            var authors = Regex.Replace(match.Groups[1].Value, @"<[^>]+>", "").Trim();
            authors = Regex.Replace(authors, @"\s+", " ");
            return authors;
        }
        return "";
    }

    private static string ExtractArxivAbstract(string html)
    {
        var match = Regex.Match(html, @"class=""abstract[^""]*""[^>]*>(.*?)</blockquote>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
        {
            var abstract_ = Regex.Replace(match.Groups[1].Value, @"<[^>]+>", "").Trim();
            abstract_ = Regex.Replace(abstract_, @"\s+", " ");
            return abstract_;
        }
        return "";
    }

    private static string GenerateSafeFilename(string url)
    {
        var uri = new Uri(url);
        var name = uri.Host + uri.PathAndQuery;
        name = Regex.Replace(name, @"[^\w\-]", "_");
        name = Regex.Replace(name, @"_+", "_");
        name = name.Trim('_');
        
        if (name.Length > 80)
        {
            name = name.Substring(0, 80);
        }
        
        return name;
    }

    private static string EscapeYaml(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }

    private static string AddContributorMetadata(string content, string author)
    {
        // Insert contributor line after the first "---" block
        var lines = content.Split('\n');
        var result = new StringBuilder();
        var inFrontmatter = false;
        var addedContributor = false;

        foreach (var line in lines)
        {
            if (line.Trim() == "---")
            {
                if (!inFrontmatter)
                {
                    inFrontmatter = true;
                    result.AppendLine(line);
                }
                else
                {
                    if (!addedContributor)
                    {
                        result.AppendLine($"contributor: \"{EscapeYaml(author)}\"");
                        addedContributor = true;
                    }
                    result.AppendLine(line);
                    inFrontmatter = false;
                }
            }
            else
            {
                result.AppendLine(line);
            }
        }

        return result.ToString();
    }

    private enum UrlType
    {
        Webpage,
        ArxivPaper,
        GitHubRepo
    }
}
