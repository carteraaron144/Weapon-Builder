namespace WeaponBuilder.Core.Tests;

/// The repository root (the folder holding project.godot), for tests that check shipped data.
static class RepoFiles
{
    public static string Root { get; } = Find();

    static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("project.godot not found above the test binaries");
    }
}
