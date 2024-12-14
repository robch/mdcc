using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

class InputOptions
{
    public InputOptions()
    {
        Debug = false;
        Verbose = false;
        Groups = new List<InputGroup>();
    }

    public bool Debug;
    public bool Verbose;
    public List<InputGroup> Groups;

    public static bool Parse(string[] args, out InputOptions options, out InputException ex)
    {
        options = null;
        ex = null;

        try
        {
            var allInputs = ExpandedInputsFromCommandLine(args);
            options = ParseInputOptions(allInputs);
            return options.Groups.Any();
        }
        catch (InputException e)
        {
            ex = e;
            return false;
        }
    }

    private static IEnumerable<string> InputsFromStdio()
    {
        if (Console.IsInputRedirected)
        {
            while (true)
            {
                var line = Console.ReadLine();
                if (line == null) break;
                yield return line.Trim();
            }
        }
    }

    private static IEnumerable<string> InputsFromCommandLine(string[] args)
    {
        foreach (var line in InputsFromStdio())
        {
            yield return line;
        }

        foreach (var arg in args)
        {
            yield return arg;
        }
    }

    private static IEnumerable<string> ExpandedInputsFromCommandLine(string[] args)
    {
        foreach (var input in InputsFromCommandLine(args))
        {
            foreach (var line in ExpandedInput(input))
            {
                yield return line;
            }
        }
    }
    
    private static IEnumerable<string> ExpandedInput(string input)
    {
        if (input.StartsWith("@") && File.Exists(input.Substring(1)))
        {
            yield return File.ReadAllText(input.Substring(1), Encoding.UTF8);
        }
        else if (input.StartsWith("@@") && File.Exists(input.Substring(2)))
        {
            foreach (var line in File.ReadLines(input.Substring(2)))
            {
                foreach (var expanded in ExpandedInput(line))
                {
                    yield return expanded;
                }
            }
        }
        else
        {
            yield return input;
        }
    }
    
    private static InputOptions ParseInputOptions(IEnumerable<string> allInputs)
    {
        var inputOptions = new InputOptions();
        var currentGroup = new InputGroup();

        var args = allInputs.ToArray();
        for (int i = 0; i < args.Count(); i++)
        {
            var arg = args[i];
            if (arg == "--" && !currentGroup.IsEmpty())
            {
                inputOptions.Groups.Add(currentGroup);
                currentGroup = new InputGroup();
            }
            else if (arg == "--debug")
            {
                inputOptions.Debug = true;
            }
            else if (arg == "--verbose")
            {
                inputOptions.Verbose = true;
            }
            else if (arg == "--contains")
            {
                var patterns = GetInputOptionArgs(i + 1, args);
                var asRegExs = ValidateRegExPatterns(arg, patterns);
                currentGroup.IncludeFileContainsPatternList.AddRange(asRegExs);
                currentGroup.IncludeLineContainsPatternList.AddRange(asRegExs);
                i += patterns.Count();
            }
            else if (arg == "--file-contains")
            {
                var patterns = GetInputOptionArgs(i + 1, args);
                var asRegExs = ValidateRegExPatterns(arg, patterns);
                currentGroup.IncludeFileContainsPatternList.AddRange(asRegExs);
                i += patterns.Count();
            }
            else if (arg == "--file-not-contains")
            {
                var patterns = GetInputOptionArgs(i + 1, args);
                var asRegExs = ValidateRegExPatterns(arg, patterns);
                currentGroup.ExcludeFileContainsPatternList.AddRange(asRegExs);
                i += patterns.Count();
            }
            else if (arg == "--line-contains")
            {
                var patterns = GetInputOptionArgs(i + 1, args);
                var asRegExs = ValidateRegExPatterns(arg, patterns);
                currentGroup.IncludeLineContainsPatternList.AddRange(asRegExs);
                i += patterns.Count();
            }
            else if (arg == "--lines")
            {
                var countStr = i + 1 < args.Count() ? args.ElementAt(++i) : null;
                var count = ValidateLineCount(arg, countStr);
                currentGroup.IncludeLineCountBefore = count;
                currentGroup.IncludeLineCountAfter = count;
            }
            else if (arg == "--lines-before")
            {
                var countStr = i + 1 < args.Count() ? args.ElementAt(++i) : null;
                currentGroup.IncludeLineCountBefore = ValidateLineCount(arg, countStr);
            }
            else if (arg == "--lines-after")
            {
                var countStr = i + 1 < args.Count() ? args.ElementAt(++i) : null;
                currentGroup.IncludeLineCountAfter = ValidateLineCount(arg, countStr);
            }
            else if (arg == "--remove-all-lines")
            {
                var patterns = GetInputOptionArgs(i + 1, args);
                var asRegExs = ValidateRegExPatterns(arg, patterns);
                currentGroup.RemoveAllLineContainsPatternList.AddRange(asRegExs);
                i += patterns.Count();
            }
            else if (arg == "--line-numbers")
            {
                currentGroup.IncludeLineNumbers = true; 
            }
            else if (arg == "--file-instructions")
            {
                var instructions = GetInputOptionArgs(i + 1, args);
                if (instructions.Count() == 0)
                {
                    throw new InputException($"{arg} - Missing file instructions");
                }
                currentGroup.FileInstructionsList.AddRange(instructions);
                i += instructions.Count();
            }
            else if (arg == "--save-file-output")
            {
                var optionArgs = GetInputOptionArgs(i + 1, args);
                var saveFileOutput = optionArgs.LastOrDefault() ?? "{filePath}/{fileBase}.md";
                currentGroup.SaveFileOutput = saveFileOutput;
                i += optionArgs.Count();
            }
            else if (arg == "--threads")
            {
                var countStr = i + 1 < args.Count() ? args.ElementAt(++i) : null;
                currentGroup.ThreadCount = ValidateInt(arg, countStr, "thread count");
            }
            else if (arg == "--exclude")
            {
                var patterns = GetInputOptionArgs(i + 1, args);
                if (patterns.Count() == 0)
                {
                    throw new InputException($"{arg} - Missing pattern");
                }

                var containsSlash = (string x) => x.Contains('/') || x.Contains('\\');
                var asRegExs = patterns
                    .Where(x => !containsSlash(x))
                    .Select(x => ValidateFilePatternToRegExPattern(arg, x));
                var asGlobs = patterns
                    .Where(x => containsSlash(x))
                    .ToList();

                currentGroup.ExcludeFileNamePatternList.AddRange(asRegExs);
                currentGroup.ExcludeGlobs.AddRange(asGlobs);
                i += patterns.Count();
            }
            else if (arg.StartsWith("--"))
            {
                throw new InputException($"{arg} - Invalid argument");
            }
            else
            {
                currentGroup.Globs.Add(arg);
            }
        }

        if (!currentGroup.IsEmpty())
        {
            inputOptions.Groups.Add(currentGroup);
        }

        foreach (var group in inputOptions.Groups.Where(x => !x.Globs.Any()))
        {
            group.Globs.Add("**");
        }

        return inputOptions;
    }

    private static IEnumerable<string> GetInputOptionArgs(int startAt, string[] args)
    {
        for (int i = startAt; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                yield break;
            }

            yield return args[i];
        }
    }

    private static IEnumerable<Regex> ValidateRegExPatterns(string arg, IEnumerable<string> patterns)
    {
        patterns = patterns.ToList();
        if (!patterns.Any())
        {
            throw new InputException($"{arg} - Missing regular expression pattern");
        }

        return patterns.Select(x => ValidateRegExPattern(arg, x));
    }

    private static Regex ValidateRegExPattern(string arg, string pattern)
    {
        try
        {
            return new Regex(pattern);
        }
        catch (Exception)
        {
            throw new InputException($"{arg} {pattern} - Invalid regular expression pattern");
        }
    }

    private static Regex ValidateFilePatternToRegExPattern(string arg, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            throw new InputException($"{arg} - Missing file pattern");
        }

        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        var patternPrefix = isWindows ? "(?i)^" : "^";
        var regexPattern = patternPrefix + pattern
            .Replace(".", "\\.")
            .Replace("*", ".*")
            .Replace("?", ".") + "$";

        try
        {
            return new Regex(regexPattern);
        }
        catch (Exception)
        {
            throw new InputException($"{arg} {pattern} - Invalid file pattern");
        }
    }

    private static int ValidateLineCount(string arg, string countStr)
    {
        return ValidateInt(arg, countStr, "line count");
    }

    private static int ValidateInt(string arg, string countStr, string argDescription)
    {
        if (string.IsNullOrEmpty(countStr))
        {
            throw new InputException($"{arg} - Missing {argDescription}");
        }

        if (!int.TryParse(countStr, out var count))
        {
            throw new InputException($"{arg} {countStr} - Invalid {argDescription}");
        }

        return count;
    }
}