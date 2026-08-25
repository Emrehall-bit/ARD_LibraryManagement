namespace LibrarySystem.Tools.CoverImport;

public sealed record CoverImportOptions(int? Limit, bool DryRun)
{
    public static CoverImportOptions Parse(string[] args)
    {
        int? limit = null;
        var dryRun = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            if (string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }

            if (string.Equals(arg, "--limit", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var parsedLimit) || parsedLimit < 0)
                {
                    throw new ArgumentException("--limit must be followed by a non-negative integer.");
                }

                limit = parsedLimit == 0 ? null : parsedLimit;
                index++;
                continue;
            }

            if (arg.StartsWith("--limit=", StringComparison.OrdinalIgnoreCase))
            {
                var rawLimit = arg["--limit=".Length..];
                if (!int.TryParse(rawLimit, out var parsedLimit) || parsedLimit < 0)
                {
                    throw new ArgumentException("--limit must be a non-negative integer.");
                }

                limit = parsedLimit == 0 ? null : parsedLimit;
                continue;
            }

            throw new ArgumentException($"Unknown argument '{arg}'. Supported arguments: --dry-run, --limit <count>.");
        }

        return new CoverImportOptions(limit, dryRun);
    }
}
