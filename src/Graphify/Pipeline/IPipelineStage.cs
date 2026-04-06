namespace Graphify.Pipeline;

/// <summary>
/// Represents a stage in the processing pipeline.
/// </summary>
/// <typeparam name="TInput">The input type for this stage.</typeparam>
/// <typeparam name="TOutput">The output type produced by this stage.</typeparam>
public interface IPipelineStage<TInput, TOutput>
{
    /// <summary>
    /// Executes the pipeline stage asynchronously.
    /// </summary>
    /// <param name="input">The input data for this stage.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <returns>The output data produced by this stage.</returns>
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}
