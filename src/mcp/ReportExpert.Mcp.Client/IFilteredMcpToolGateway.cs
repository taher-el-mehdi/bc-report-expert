namespace ReportExpert.Mcp.Client;

/// <summary>
/// A gateway that can hide individual tools the user has turned off.
/// </summary>
public interface IFilteredMcpToolGateway : IMcpToolGateway
{
    /// <summary>
    /// Every tool the underlying servers offer, including ones the user has disabled.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The full catalogue.</returns>
    ValueTask<IReadOnlyList<McpToolDescriptor>> ListAllToolsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Whether the user currently allows the tool to run.</summary>
    /// <param name="qualifiedName">The <c>server__tool</c> name.</param>
    /// <returns><see langword="true"/> when the tool may be offered and executed.</returns>
    bool IsToolEnabled(string qualifiedName);
}
