using System.ComponentModel;
using System.Diagnostics;
using EnvDTE;
using EnvDTE80;
using ModelContextProtocol.Server;

namespace VsDebuggerMcp.Tools;

[McpServerToolType]
public class BuildTools
{
    [McpServerTool, Description("Build the entire solution")]
    public static string BuildSolution()
    {
        var dte = DteConnector.GetDte();
        try
        {
            var build = ExecuteBuild(dte);
            return $"Build of '{build.TargetName}' completed. State: {build.State}, Failed projects: {build.FailedProjects}{build.Errors}";
        }
        catch (Exception ex)
        {
            return $"Build failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Rebuild the entire solution")]
    public static string RebuildSolution()
    {
        var dte = DteConnector.GetDte();
        try
        {
            DteConnector.ExecuteWithComRetry(() => dte.ExecuteCommand("Build.RebuildSolution"));
            WaitForBuildCompletion(dte);

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
    public static string BuildProject(string projectName, string configuration = "Debug")
    {
        var dte = DteConnector.GetDte();
        try
        {
            var build = ExecuteBuild(dte, projectName, configuration);
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
        var dte = DteConnector.GetDte();
        var projects = DteConnector.ExecuteWithComRetry(() => dte.Solution.Projects);
        if (DteConnector.ExecuteWithComRetry(() => projects.Count) == 0)
            return "No projects found in the solution.";

        var lines = new List<string>();
        foreach (Project project in projects)
        {
            CollectProjects(project, lines);
        }

        return "Projects in solution:\n" + string.Join("\n", lines);
    }

    [McpServerTool, Description("Get current build state (idle, building, or done)")]
    public static string GetBuildStatus()
    {
        var dte = DteConnector.GetDte();
        var state = DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.BuildState);
        var info = DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.LastBuildInfo);
        return $"BuildState: {state}, LastBuildInfo (failed projects): {info}";
    }

    [McpServerTool, Description("Clean the solution")]
    public static string CleanSolution()
    {
        var dte = DteConnector.GetDte();
        DteConnector.ExecuteWithComRetry(() => dte.Solution.SolutionBuild.Clean(true));
        return "Clean completed.";
    }

    internal static BuildInvocationResult ExecuteBuild(DTE2 dte, string? projectName = null, string configuration = "Debug")
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

        WaitForBuildCompletion(dte);

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
            CollectProjectObjects(project, allProjects);
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

    private static void CollectProjectObjects(Project project, List<Project> result)
    {
        try
        {
            // Solution folders have Kind = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}"
            if (project.Kind == "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}")
            {
                // Recurse into solution folder sub-projects
                if (project.ProjectItems != null)
                {
                    foreach (ProjectItem item in project.ProjectItems)
                    {
                        if (item.SubProject != null)
                            CollectProjectObjects(item.SubProject, result);
                    }
                }
            }
            else
            {
                result.Add(project);
            }
        }
        catch (Exception)
        {
            // Skip inaccessible projects
        }
    }

    private static void CollectProjects(Project project, List<string> lines)
    {
        try
        {
            if (project.Kind == "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}")
            {
                // Solution folder - recurse
                if (project.ProjectItems != null)
                {
                    foreach (ProjectItem item in project.ProjectItems)
                    {
                        if (item.SubProject != null)
                            CollectProjects(item.SubProject, lines);
                    }
                }
            }
            else
            {
                lines.Add($"  - {project.Name} (UniqueName: {project.UniqueName})");
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

    private static void WaitForBuildCompletion(DTE2 dte)
    {
        var timeout = TimeSpan.FromMinutes(5);
        var stopwatch = Stopwatch.StartNew();
        var sawInProgress = false;
        var stableDoneSamples = 0;

        while (stopwatch.Elapsed < timeout)
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

            System.Threading.Thread.Sleep(250);
        }

        throw new TimeoutException($"Timed out waiting for build completion after {timeout.TotalMinutes:0} minutes.");
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
