namespace Graphify.Validation;

using Graphify.Models;

/// <summary>
/// Validates graph structures and data integrity.
/// </summary>
public interface IGraphValidator
{
    ValidationResult Validate(ExtractionResult extraction);
}
