using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EquipmentExperience;

// Game assemblies are loaded from the Valheim install rather than copied, so the check always runs against the
// version installed right now.
string valheim = args.Length > 0 ? args[0] : DefaultValheimDir();
string[] searchDirs =
{
    Path.Combine(valheim, "valheim.app", "Contents", "Resources", "Data", "Managed"),
    Path.Combine(valheim, "valheim_Data", "Managed"),
    Path.Combine(valheim, "BepInEx", "core"),
};
if (!searchDirs.Any(d => File.Exists(Path.Combine(d, "assembly_valheim.dll"))))
{
    Console.Error.WriteLine($"No Valheim install found at '{valheim}'. Pass its path as the first argument.");
    return 2;
}
AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    string name = new AssemblyName(e.Name).Name;
    if (name == "0Harmony")
    {
        return null; // use HarmonyX from this tool, not the BepInEx copy
    }
    string path = searchDirs.Select(d => Path.Combine(d, name + ".dll")).FirstOrDefault(File.Exists);
    return path != null ? Assembly.LoadFrom(path) : null;
};
return Check.Run();

static string DefaultValheimDir()
{
    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string[] candidates =
    {
        Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common", "Valheim"),
        Path.Combine(home, ".steam", "steam", "steamapps", "common", "Valheim"),
        @"C:\Program Files (x86)\Steam\steamapps\common\Valheim",
    };
    return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
}

static class Check
{
    // Separate from the top-level code so the game assemblies are only loaded after the resolver is registered.
    public static int Run()
    {
        object version = Type.GetType("Version, assembly_valheim")?.GetMethod("GetVersionString")?.Invoke(null, new object[] { false });
        Console.WriteLine($"Valheim {version ?? "(unknown version)"}");
        int failed = 0;
        foreach (PatchGroup group in PatchGroups.All())
        {
            if (group.Missing.Count == 0)
            {
                Console.WriteLine($"  ok       {group.Name}");
                continue;
            }
            failed++;
            Console.WriteLine($"  MISSING  {group.Name}");
            foreach (string missing in group.Missing)
            {
                Console.WriteLine($"             {missing}");
            }
        }
        Console.WriteLine(failed == 0 ? "All patch groups resolve." : $"{failed} patch group(s) would be disabled.");
        return failed == 0 ? 0 : 1;
    }
}
