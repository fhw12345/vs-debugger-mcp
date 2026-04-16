using System.ComponentModel;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class BuildTools
{
    [McpServerTool, Description("Build the entire solution")]
    public static async Task<string> BuildSolution()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        try
        {
            var build = await ExecuteBuildAsync(dte);
            return $"Build of '{build.TargetName}' completed. State: {build.State}, Failed projects: {build.FailedProjects}{build.Errors}";
        }
        catch (Exception ex)
        {
            return $"Build failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Rebuild the entire solution")]
    public static async Task<string> RebuildSolution()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        try
        {
            DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Build.RebuildSolution"));
            await WaitForBuildCompletionAsync(dte);

            var state = DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.BuildState);
            var info = DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.LastBuildInfo);
            var errors = CollectBuildErrors(dte);
            return $"Rebuild completed. State: {state}, Failed projects: {info}{errors}";
        }
        catch (Exception ex)
        {
            return $"Rebuild failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Build a specific project by name. The name can be a substring match (e.g. 'MyProject' will match 'MyProject\\MyProject.csproj'). Use ListProjects to see available project names.")]
    public static async Task<string> BuildProject(string projectName, string configuration = "Debug")
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        try
        {
            var build = await ExecuteBuildAsync(dte, projectName, configuration);
            return $"Build of '{build.TargetName}' completed. State: {build.State}, Failed projects: {build.FailedProjects}{build.Errors}";
        }
        catch (Exception ex)
        {
            return $"Build of '{projectName}' failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("List all projects in the current solution with their unique names")]
    public static string ListProjects()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var projects = DteConnector.ExecuteWithComRetry(() => dte.Solution.Projects);
        if (DteConnector.ExecuteWithComRetry(() => projects.Count) == 0)
            return "No projects found in the solution.";

        var lines = new List<string>();
        foreach (Project project in projects)
        {
            TraverseProjects(project, lines, p => $"  - {p.Name} (UniqueName: {p.UniqueName})");
        }

        return "Projects in solution:\n" + string.Join("\n", lines);
    }

    [McpServerTool, Description("Get current build state (idle, building, or done)")]
    public static string GetBuildStatus()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        var state = DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.BuildState);
        var info = DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.LastBuildInfo);
        return $"BuildState: {state}, LastBuildInfo (failed projects): {info}";
    }

    [McpServerTool, Description("Clean the solution")]
    public static string CleanSolution()
    {
        if (!DteConnector.TryGetDte(out var dte, out var dteError)) return dteError;
        DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.Clean(true));
        return "Clean completed.";
    }

    internal static async Task<BuildInvocationResult> ExecuteBuildAsync(DTE2 dte, string? projectName = null, string configuration = "Debug")
    {
        string targetName;

        if (string.IsNullOrWhiteSpace(projectName))
        {
            targetName = "solution";
            DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.Build(true));
        }
        else
        {
            targetName = ResolveProjectUniqueName(dte, projectName);
            DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.BuildProject(configuration, targetName, true));
        }

        await WaitForBuildCompletionAsync(dte);

        var state = DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.BuildState);
        var failedProjects = DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.LastBuildInfo);
        var errors = CollectBuildErrors(dte);
        return new BuildInvocationResult(targetName, configuration, state, failedProjects, errors);
    }

    /// <summary>
    /// Resolves a friendly project name to the UniqueName required by SolutionBuild.BuildProject.
    /// Supports exact match, substring match on Name, and substring match on UniqueName.
    /// </summary>
    private static string ResolveProjectUniqueName(DTE2 dte, string projectName)
    {
        var allProjects = new List<Project>();
        foreach (Project project in dte.Solution.Projects)
        {
            TraverseProjects(project, allProjects, p => p);
        }

        // 1. Exact match on UniqueName
        foreach (var p in allProjects)
        {
            if (string.Equals(p.UniqueName, projectName, StringComparison.OrdinalIgnoreCase))
                return p.UniqueName;
        }

        // 2. Exact match on Name
        foreach (var p in allProjects)
        {
            if (string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase))
                return p.UniqueName;
        }

        // 3. Substring match on Name or UniqueName
        var matches = allProjects
            .Where(p => p.Name.Contains(projectName, StringComparison.OrdinalIgnoreCase)
                     || p.UniqueName.Contains(projectName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1)
            return matches[0].UniqueName;

        if (matches.Count > 1)
        {
            var names = string.Join(", ", matches.Select(p => $"'{p.Name}' ({p.UniqueName})"));
            throw new ArgumentException(
                $"Ambiguous project name '{projectName}'. Multiple matches found: {names}. Please be more specific or use the exact UniqueName.");
        }

        // No match found
        var available = string.Join(", ", allProjects.Select(p => $"'{p.Name}' ({p.UniqueName})"));
        throw new ArgumentException(
            $"Project '{projectName}' not found. Available projects: {available}");
    }

    private static void TraverseProjects<T>(Project project, List<T> result, Func<Project, T> selector)
    {
        try
        {
            // Solution folders have Kind = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}"
            if (project.Kind == "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}")
            {
                if (project.ProjectItems != null)
                {
                    foreach (ProjectItem item in project.ProjectItems)
                    {
                        if (item.SubProject != null)
                            TraverseProjects(item.SubProject, result, selector);
                    }
                }
            }
            else
            {
                result.Add(selector(project));
            }
        }
        catch (Exception)
        {
            // Skip inaccessible projects
        }
    }

    internal static string CollectBuildErrors(DTE2 dte)
    {
        try
        {
            var errorList = DteConnector.ExecuteWithComRetry(() => dte.ToolWindows.ErrorList);
            var errorCount = DteConnector.ExecuteWithComRetry(() => errorList.ErrorItems.Count);
            if (errorCount == 0)
                return "";

            var errors = new List<string>();
            var limit = Math.Min(errorCount, 20);
            for (int i = 1; i <= limit; i++)
            {
                var item = DteConnector.ExecuteWithComRetry(() => errorList.ErrorItems.Item(i));
                var project = DteConnector.ExecuteWithComRetry(() => item.Project);
                var fileName = DteConnector.ExecuteWithComRetry(() => item.FileName);
                var line = DteConnector.ExecuteWithComRetry(() => item.Line);
                var description = DteConnector.ExecuteWithComRetry(() => item.Description);
                errors.Add($"  [{project}] {fileName}({line}): {description}");
            }

            var result = $"\nErrors ({errorCount} total):\n" + string.Join("\n", errors);
            if (errorCount > limit)
                result += $"\n  ... and {errorCount - limit} more errors";
            return result;
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static async Task WaitForBuildCompletionAsync(DTE2 dte, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));
        var sawInProgress = false;
        var stableDoneSamples = 0;

        while (!cts.Token.IsCancellationRequested)
        {
            var state = DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.BuildState);
            if (state == vsBuildState.vsBuildStateInProgress)
            {
                sawInProgress = true;
                stableDoneSamples = 0;
            }
            else
            {
                if (!sawInProgress)
                    return;

                stableDoneSamples++;
                if (stableDoneSamples >= 2)
                    return;
            }

            await Task.Delay(250, cts.Token).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for build completion after 5 minutes.");
    }

    internal readonly record struct BuildInvocationResult(
        string TargetName,
        string Configuration,
        vsBuildState State,
        int FailedProjects,
        string Errors)
    {
        public bool Succeeded => FailedProjects == 0;
    }
}
