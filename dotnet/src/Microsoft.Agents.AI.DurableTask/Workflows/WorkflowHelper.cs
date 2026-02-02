// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.Workflows;

namespace Microsoft.Agents.AI.DurableTask.Workflows;

/// <summary>
/// Represents an executor in the workflow with its metadata.
/// </summary>
/// <param name="ExecutorId">The unique identifier of the executor.</param>
/// <param name="IsAgenticExecutor">Indicates whether this executor is an agentic executor.</param>
/// <param name="RequestPort">The request port if this executor is a request port executor; otherwise, null.</param>
/// <param name="SubWorkflow">The sub-workflow if this executor is a sub-workflow executor; otherwise, null.</param>
internal sealed record WorkflowExecutorInfo(string ExecutorId, bool IsAgenticExecutor, RequestPort? RequestPort = null, Workflow? SubWorkflow = null)
{
    /// <summary>
    /// Gets a value indicating whether this executor is a request port executor (human-in-the-loop).
    /// </summary>
    public bool IsRequestPortExecutor => this.RequestPort is not null;

    /// <summary>
    /// Gets a value indicating whether this executor is a sub-workflow executor.
    /// </summary>
    public bool IsSubworkflowExecutor => this.SubWorkflow is not null;
}

/// <summary>
/// Provides helper methods for analyzing and executing workflows.
/// </summary>
internal static class WorkflowHelper
{
    /// <summary>
    /// Accepts a workflow instance and returns a list of executors with metadata.
    /// </summary>
    /// <param name="workflow">The workflow instance to analyze.</param>
    /// <returns>A list of executor information.</returns>
    public static List<WorkflowExecutorInfo> GetExecutorsFromWorkflowInOrder(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        Dictionary<string, ExecutorBinding> executors = workflow.ReflectExecutors();
        List<WorkflowExecutorInfo> result = [];

        foreach (KeyValuePair<string, ExecutorBinding> executor in executors)
        {
            bool isAgentic = IsAgentExecutorType(executor.Value.ExecutorType);
            RequestPort? requestPort = (executor.Value is RequestPortBinding rpb) ? rpb.Port : null;
            Workflow? subWorkflow = (executor.Value is SubworkflowBinding swb) ? swb.WorkflowInstance : null;
            result.Add(new WorkflowExecutorInfo(executor.Key, isAgentic, requestPort, subWorkflow));
        }

        return result;
    }

    /// <summary>
    /// Builds the workflow graph information needed for message-driven execution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method extracts only the information needed for routing messages:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Successors and predecessors for each executor</description></item>
    /// <item><description>Edge conditions for conditional routing</description></item>
    /// <item><description>Output types for deserialization</description></item>
    /// </list>
    /// <para>
    /// Unlike level-based execution plans, this approach supports cyclic workflows
    /// naturally through message-driven superstep execution.
    /// </para>
    /// </remarks>
    /// <param name="workflow">The workflow instance to analyze.</param>
    /// <returns>A graph info object containing routing information.</returns>
    public static WorkflowGraphInfo BuildGraphInfo(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        Dictionary<string, ExecutorBinding> executors = workflow.ReflectExecutors();
        Dictionary<string, HashSet<Edge>> edges = workflow.Edges;

        WorkflowGraphInfo graphInfo = new()
        {
            StartExecutorId = workflow.StartExecutorId
        };

        // Initialize successors, predecessors, and extract output types for all executors
        foreach (KeyValuePair<string, ExecutorBinding> executor in executors)
        {
            graphInfo.Successors[executor.Key] = [];
            graphInfo.Predecessors[executor.Key] = [];
            graphInfo.ExecutorOutputTypes[executor.Key] = GetExecutorOutputType(executor.Value.ExecutorType);
        }

        // Build the graph from edges and extract edge conditions
        foreach (KeyValuePair<string, HashSet<Edge>> edgeGroup in edges)
        {
            string sourceId = edgeGroup.Key;
            List<string> sourceSuccessors = graphInfo.Successors[sourceId];

            foreach (Edge edge in edgeGroup.Value)
            {
                foreach (string sinkId in edge.Data.Connection.SinkIds)
                {
                    if (graphInfo.Successors.ContainsKey(sinkId))
                    {
                        sourceSuccessors.Add(sinkId);
                        graphInfo.Predecessors[sinkId].Add(sourceId);
                    }
                }

                // Extract condition from DirectEdgeData if present
                DirectEdgeData? directEdge = edge.DirectEdgeData;
                if (directEdge?.Condition is not null)
                {
                    graphInfo.EdgeConditions[(directEdge.SourceId, directEdge.SinkId)] = directEdge.Condition;
                }
            }
        }

        return graphInfo;
    }

    /// <summary>
    /// Determines whether the specified executor type is an agentic executor.
    /// </summary>
    /// <param name="executorType">The executor type to check.</param>
    /// <returns><c>true</c> if the executor is an agentic executor; otherwise, <c>false</c>.</returns>
    internal static bool IsAgentExecutorType(Type executorType)
    {
        // Check if the type name or assembly indicates it's an agent executor
        string typeName = executorType.FullName ?? executorType.Name;
        string assemblyName = executorType.Assembly.GetName().Name ?? string.Empty;

        return typeName.Contains("AIAgentHostExecutor", StringComparison.OrdinalIgnoreCase) &&
                assemblyName.Contains("Microsoft.Agents.AI", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the output type from an executor type.
    /// For Executor&lt;TInput, TOutput&gt;, returns TOutput.
    /// For Executor&lt;TInput&gt;, returns null (void output).
    /// </summary>
    /// <param name="executorType">The executor type to analyze.</param>
    /// <returns>The output type, or null if the executor has no typed output.</returns>
    private static Type? GetExecutorOutputType(Type executorType)
    {
        // Walk up the inheritance chain to find Executor<TInput, TOutput> or Executor<TInput>
        Type? currentType = executorType;
        while (currentType is not null)
        {
            if (currentType.IsGenericType)
            {
                Type genericDefinition = currentType.GetGenericTypeDefinition();
                Type[] genericArgs = currentType.GetGenericArguments();

                // Check for Executor<TInput, TOutput> (2 type parameters)
                if (genericArgs.Length == 2 && genericDefinition.Name.StartsWith("Executor", StringComparison.Ordinal))
                {
                    return genericArgs[1]; // TOutput
                }

                // Check for Executor<TInput> (1 type parameter) - void return
                if (genericArgs.Length == 1 && genericDefinition.Name.StartsWith("Executor", StringComparison.Ordinal))
                {
                    return null;
                }
            }

            currentType = currentType.BaseType;
        }

        return null;
    }
}
