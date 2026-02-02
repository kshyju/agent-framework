// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Agents.AI.DurableTask.Workflows;

/// <summary>
/// Represents a message envelope for durable workflow message passing.
/// </summary>
/// <remarks>
/// <para>
/// This is the durable equivalent of <c>MessageEnvelope</c> in the in-process runner.
/// Unlike the in-process version which holds native .NET objects, this envelope
/// contains serialized JSON strings suitable for Durable Task activities.
/// </para>
/// <para>
/// The envelope captures:
/// </para>
/// <list type="bullet">
/// <item><description><strong>Message:</strong> The serialized JSON payload</description></item>
/// <item><description><strong>InputTypeName:</strong> Type information for deserialization</description></item>
/// <item><description><strong>SourceExecutorId:</strong> The executor that produced this message (for tracing)</description></item>
/// </list>
/// </remarks>
internal sealed class DurableMessageEnvelope
{
    /// <summary>
    /// Gets or sets the serialized JSON message content.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the full type name of the message for deserialization.
    /// </summary>
    public string? InputTypeName { get; init; }

    /// <summary>
    /// Gets or sets the ID of the executor that produced this message.
    /// </summary>
    /// <remarks>
    /// Used for tracing and debugging. Can be null for initial workflow input.
    /// </remarks>
    public string? SourceExecutorId { get; init; }

    /// <summary>
    /// Creates a new envelope for the initial workflow input.
    /// </summary>
    public static DurableMessageEnvelope ForInput(string message, string? inputTypeName)
    {
        return new DurableMessageEnvelope
        {
            Message = message,
            InputTypeName = inputTypeName,
            SourceExecutorId = null
        };
    }

    /// <summary>
    /// Creates a new envelope for a message from an executor.
    /// </summary>
    public static DurableMessageEnvelope FromExecutor(string sourceExecutorId, string message, string? inputTypeName)
    {
        return new DurableMessageEnvelope
        {
            Message = message,
            InputTypeName = inputTypeName,
            SourceExecutorId = sourceExecutorId
        };
    }
}
