using System.Runtime.InteropServices;

namespace QueueCutoff.Elevated;

internal static class Program
{
    private const string Prefix = "QueueCutoff";

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: QueueCutoff.Elevated enable <exe>... | disable | status");
                return 2;
            }

            return args[0].ToLowerInvariant() switch
            {
                "enable" => Enable(args.Skip(1).ToArray()),
                "disable" => Disable(),
                "status" => Status(),
                _ => 2
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Enable(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0)
        {
            Console.Error.WriteLine("No executable paths supplied.");
            return 2;
        }

        var policy = CreatePolicy();
        RemoveOwnedRules(policy);

        var index = 1;
        foreach (var path in paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddRule(policy, path, index++);
        }

        Console.WriteLine("enabled");
        return 0;
    }

    private static int Disable()
    {
        RemoveOwnedRules(CreatePolicy());
        Console.WriteLine("disabled");
        return 0;
    }

    private static int Status()
    {
        var enabled = false;
        foreach (dynamic rule in CreatePolicy().Rules)
        {
            string name = rule.Name;
            if (name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && rule.Enabled)
            {
                enabled = true;
                break;
            }
        }

        Console.WriteLine(enabled ? "enabled" : "disabled");
        return 0;
    }

    private static dynamic CreatePolicy()
    {
        var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2")
            ?? throw new COMException("Windows Firewall COM API is unavailable.");
        return Activator.CreateInstance(type)
            ?? throw new COMException("Could not create INetFwPolicy2.");
    }

    private static void AddRule(dynamic policy, string applicationPath, int index)
    {
        var type = Type.GetTypeFromProgID("HNetCfg.FWRule")
            ?? throw new COMException("Windows Firewall rule COM API is unavailable.");
        dynamic rule = Activator.CreateInstance(type)
            ?? throw new COMException("Could not create INetFwRule.");

        rule.Name = $"{Prefix} - {Path.GetFileName(applicationPath)} - {index}";
        rule.Description = "Blocks League queue traffic after the confirmed QueueCutoff time.";
        rule.ApplicationName = applicationPath;
        rule.Protocol = 256;
        rule.Direction = 2;
        rule.Action = 0;
        rule.Enabled = true;
        rule.Profiles = int.MaxValue;
        rule.Grouping = Prefix;

        policy.Rules.Add(rule);
    }

    private static void RemoveOwnedRules(dynamic policy)
    {
        var names = new List<string>();
        foreach (dynamic rule in policy.Rules)
        {
            string name = rule.Name;
            if (name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }

        foreach (var name in names)
        {
            policy.Rules.Remove(name);
        }
    }
}
