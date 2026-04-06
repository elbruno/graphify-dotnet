namespace Graphify.Models;

/// <summary>
/// Categorizes detected files by their primary purpose.
/// </summary>
public enum FileCategory
{
    /// <summary>
    /// Source code files (.cs, .py, .js, etc.)
    /// </summary>
    Code,

    /// <summary>
    /// Documentation files (.md, .txt, .rst, .adoc)
    /// </summary>
    Documentation,

    /// <summary>
    /// Media files (.pdf, .png, .jpg, etc.)
    /// </summary>
    Media
}
