using System.ComponentModel;
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
        dte.Solution.SolutionBuild.Build(true);
        var state = dte.Solution.SolutionBuild.BuildState;
        var info = dte.Solution.SolutionBuild.LastBuildInfo;
        var errors = CollectBuildErrors(dte);
        return $"Build completed. State: {state}, Failed projects: {info}{errors}";
    }

    [McpServerTool, Description("Rebuild the entire solution")]
    public static string RebuildSolution()
    {
        var dte = DteConnector.GetDte();
        dte.ExecuteCommand("Build.RebuildSolution");
        System.Threading.Thread.Sleep(1000); // Wait for rebuild to start
        while (dte.Solution.SolutionBuild.BuildState == vsBuildState.vsBuildStateInProgress)
        {
            System.Threading.Thread.Sleep(500);
        }

        var info = dte.Solution.SolutionBuild.LastBuildInfo;
        var errors = CollectBuildErrors(dte);
        return $"Rebuild completed. Failed projects: {info}{errors}";
    }

    [McpServerTool, Description("Build a specific project by name. The name can be a substring match (e.g. 'MyProject' will match 'MyProject\\MyProject.csproj'). Use ListProjects to see available project names.")]
    public static string BuildProject(string projectName, string configuration = "Debug")
    {
        var dte = DteConnector.GetDte();
        var uniqueName = ResolveProjectUniqueName(dte, projectName);
        dte.Solution.SolutionBuild.BuildProject(configuration, uniqueName, true);
        var info = dte.Solution.SolutionBuild.LastBuildInfo;
        var errors = CollectBuildErrors(dte);
        return $"Build of '{uniqueName}' completed. Failed projects: {info}{errors}";
    }

    [McpServerTool, Description("List all projects in the current solution with their unique names")]
    public static string ListProjects()
    {
        var dte = DteConnector.GetDte();
        var projects = dte.Solution.Projects;
        if (projects.Count == 0)
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
        var state = dte.Solution.SolutionBuild.BuildState;
        var info = dte.Solution.SolutionBuild.LastBuildInfo;
        return $"BuildState: {state}, LastBuildInfo (failed projects): {info}";
    }

    [McpServerTool, Description("Clean the solution")]
    public static string CleanSolution()
    {
        var dte = DteConnector.GetDte();
        dte.Solution.SolutionBuild.Clean(true);
        return "Clean completed.";
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

    private static string CollectBuildErrors(DTE2 dte)
    {
        try
        {
            var errorList = dte.ToolWindows.ErrorList;
            var errorCount = errorList.ErrorItems.Count;
            if (errorCount == 0)
                return "";

            var errors = new List<string>();
            var limit = Math.Min(errorCount, 20);
            for (int i = 1; i <= limit; i++)
            {
                var item = errorList.ErrorItems.Item(i);
                errors.Add($"  [{item.Project}] {item.FileName}({item.Line}): {item.Description}");
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
}
