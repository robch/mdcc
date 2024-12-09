using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

struct InputGroup
{
    public InputGroup()
    {
        Globs = new List<string>();
        ExcludeGlobs = new List<string>();
        ExcludeFileNamePatternList = new List<Regex>();

        IncludeFileContainsPatternList = new List<Regex>();
        ExcludeFileContainsPatternList = new List<Regex>();

        IncludeLineContainsPatternList = new List<Regex>();
        IncludeLineCountBefore = 0;
        IncludeLineCountAfter = 0;
        IncludeLineNumbers = false;

        RemoveAllLineContainsPatternList = new List<Regex>();
        FileInstructions = new List<string>();
    }

    public List<string> Globs;
    public List<string> ExcludeGlobs;
    public List<Regex> ExcludeFileNamePatternList;

    public List<Regex> IncludeFileContainsPatternList;
    public List<Regex> ExcludeFileContainsPatternList;

    public List<Regex> IncludeLineContainsPatternList;
    public int IncludeLineCountBefore;
    public int IncludeLineCountAfter;
    public bool IncludeLineNumbers;

    public List<Regex> RemoveAllLineContainsPatternList;

    public List<string> FileInstructions;
}

internal class InputException : Exception
{
    public InputException(string message) : base(message)
    {
    }
}

class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (!ParseInputs(args, out var groups, out var ex))
        {
            PrintBanner();
            if (ex != null)
            {
                PrintException(ex);
                return 2;
            }
            else
            {
                PrintUsage();
                return 1;
            }
        }

        var processedFiles = new HashSet<string>();
        foreach (var group in groups)
        {
            var files = FindMatchingFiles(
                group.Globs,
                group.ExcludeGlobs,
                group.ExcludeFileNamePatternList,
                group.IncludeFileContainsPatternList,
                group.ExcludeFileContainsPatternList);

            foreach (var file in files)
            {
                if (!processedFiles.Contains(file))
                {
                    PrintFileContent(
                        file,
                        group.IncludeLineContainsPatternList,
                        group.IncludeLineCountBefore,
                        group.IncludeLineCountAfter,
                        group.IncludeLineNumbers,
                        group.RemoveAllLineContainsPatternList,
                        group.FileInstructions);
                    processedFiles.Add(file);
                }
            }
        }

        return 0;
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
    
    private static bool ParseInputs(string[] args, out List<InputGroup> groups, out InputException ex)
    {
        ex = null;
        groups = null;

        try
        {
            var allInputs = ExpandedInputsFromCommandLine(args);
            groups = ParseInputs(allInputs);
            return groups.Any();
        }
        catch (InputException e)
        {
            ex = e;
            return false;
        }
    }

    private static List<InputGroup> ParseInputs(IEnumerable<string> allInputs)
    {
        var inputGroups = new List<InputGroup>();
        var currentGroup = new InputGroup();

        var args = allInputs.ToArray();
        for (int i = 0; i < args.Count(); i++)
        {
            var arg = args[i];
            if (arg == "--")
            {
                inputGroups.Add(currentGroup);
                currentGroup = new InputGroup();
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
                currentGroup.FileInstructions.AddRange(instructions);
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

        var groupOk = currentGroup.Globs.Any() ||
            currentGroup.IncludeFileContainsPatternList.Any() || 
            currentGroup.ExcludeFileContainsPatternList.Any() ||
            currentGroup.IncludeLineContainsPatternList.Any() ||
            currentGroup.RemoveAllLineContainsPatternList.Any();
        if (groupOk)
        {
            inputGroups.Add(currentGroup);
        }

        return inputGroups;
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
        if (string.IsNullOrEmpty(countStr))
        {
            throw new InputException($"{arg} - Missing line count");
        }

        if (!int.TryParse(countStr, out var count))
        {
            throw new InputException($"{arg} {countStr} - Invalid line count");
        }

        return count;
    }

    private static bool IsFileMatch(string file, List<Regex> includeFileContainsPatternList, List<Regex> excludeFileContainsPatternList)
    {
        var checkContent = includeFileContainsPatternList.Any() || excludeFileContainsPatternList.Any();
        if (!checkContent) return true;

        var content = File.ReadAllText(file, Encoding.UTF8);
        var includeFile = includeFileContainsPatternList.All(regex => regex.IsMatch(content));
        var excludeFile = excludeFileContainsPatternList.Count > 0 && excludeFileContainsPatternList.Any(regex => regex.IsMatch(content));

        return includeFile && !excludeFile;
    }

    private static bool IsLineMatch(string line, List<Regex> includeLineContainsPatternList, List<Regex> removeAllLineContainsPatternList)
    {
        var includeMatch = includeLineContainsPatternList.All(regex => regex.IsMatch(line));
        var excludeMatch = removeAllLineContainsPatternList.Count > 0 && removeAllLineContainsPatternList.Any(regex => regex.IsMatch(line));

        return includeMatch && !excludeMatch;
    }

    private static IEnumerable<string> FindMatchingFiles(
        List<string> globs,
        List<string> excludeGlobs,
        List<Regex> excludeFileNamePatternList,
        List<Regex> includeFileContainsPatternList,
        List<Regex> excludeFileContainsPatternList)
    {
        var excludeFiles = new HashSet<string>(FilesFromGlobs(excludeGlobs));
        var files = FilesFromGlobs(globs)
            .Where(file => !excludeFiles.Contains(file))
            .Where(file => !excludeFileNamePatternList.Any(regex => regex.IsMatch(Path.GetFileName(file))))
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine($"## Pattern: {string.Join(" ", globs)}\n\n - No files found\n");
            return Enumerable.Empty<string>();
        }

        var filtered = files.Where(file => IsFileMatch(file, includeFileContainsPatternList, excludeFileContainsPatternList)).ToList();
        if (filtered.Count == 0)
        {
            Console.WriteLine($"## Pattern: {string.Join(" ", globs)}\n\n - No files matched criteria\n");
            return Enumerable.Empty<string>();
        }

        return filtered.Distinct();
    }

    private static IEnumerable<string> FilesFromGlobs(List<string> globs)
    {
        foreach (var glob in globs)
        {
            foreach (var file in FilesFromGlob(glob))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> FilesFromGlob(string glob)
    {
        try
        {
            var matcher = new Matcher();
            matcher.AddInclude(MakeRelativePath(glob));

            var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(Directory.GetCurrentDirectory()));
            var matchResult = matcher.Execute(directoryInfo);

            return matchResult.Files.Select(file => MakeRelativePath(Path.Combine(Directory.GetCurrentDirectory(), file.Path)));
        }
        catch (Exception)
        {
            return Enumerable.Empty<string>();
        }
    }

    private static string MakeRelativePath(string fullPath)
    {
        var currentDirectory = Directory.GetCurrentDirectory().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        fullPath = Path.GetFullPath(fullPath);

        if (fullPath.StartsWith(currentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath.Substring(currentDirectory.Length);
        }

        Uri fullPathUri = new Uri(fullPath);
        Uri currentDirectoryUri = new Uri(currentDirectory);

        string relativePath = Uri.UnescapeDataString(currentDirectoryUri.MakeRelativeUri(fullPathUri).ToString().Replace('/', Path.DirectorySeparatorChar));

        if (Path.DirectorySeparatorChar == '\\')
        {
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        return relativePath;
    }

    private static void PrintBanner()
    {
        Console.WriteLine(
            "MDCC - Markdown Context Creator CLI, Version 1.0.0\n" +
            "Copyright(c) 2024, Rob Chambers. All rights reserved.\n");
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            "USAGE: mdcc [file1 [file2 [pattern1 [pattern2 [...]]]]] [...]\n\n" +
            "OPTIONS\n\n" +
            "  --contains REGEX             Match only files and lines that contain the specified regex pattern\n\n" +
            "  --file-contains REGEX        Match only files that contain the specified regex pattern\n" +
            "  --file-not-contains REGEX    Exclude files that contain the specified regex pattern\n" +
            "  --exclude PATTERN            Exclude files that match the specified pattern\n\n" +
            "  --line-contains REGEX        Match only lines that contain the specified regex pattern\n" +
            "  --lines-before N             Include N lines before matching lines (default 0)\n" +
            "  --lines-after N              Include N lines after matching lines (default 0)\n" +
            "  --lines N                    Include N lines both before and after matching lines\n\n" +
            "  --line-numbers               Include line numbers in the output\n" +
            "  --remove-all-lines REGEX     Remove lines that contain the specified regex pattern\n\n" +
            "  --file-instructions \"...\"    Apply the specified instructions to each file using AI CLI (e.g., @file)\n\n" +
            "  @ARGUMENTS\n\n" +
            "    Arguments starting with @ (e.g. @file) will use file content as argument.\n" +
            "    Arguments starting with @@ (e.g. @@file) will use file content as arguments line by line.\n\n" +
            "EXAMPLES\n\n" +
            "  mdcc file1.cs\n" +
            "  mdcc file1.md file2.md\n" +
            "  mdcc @@filelist.txt\n\n" +
            "  mdcc \"src/**/*.cs\" \"*.md\"\n" +
            "  mdcc \"src/**/*.js\" --contains \"export\"\n" +
            "  mdcc \"src/**\" --contains \"(?i)LLM\" --lines 2\n" +
            "  mdcc \"src/**\" --file-not-contains \"TODO\" --exclude \"drafts/*\"\n" +
            "  mdcc \"*.cs\" --remove-all-lines \"^\\s*//\"\n\n" +
            "  mdcc \"**/*.json\" --file-instructions \"convert the JSON to YAML\"\n" +
            "  mdcc \"**/*.json\" --file-instructions @instructions.md"
        );
    }
    private static void PrintException(InputException ex)
    {
        Console.WriteLine($"{ex.Message}\n\n");
    }

    private static void PrintFileContent(string fileName, List<Regex> includeLineContainsPatternList, int includeLineCountBefore, int includeLineCountAfter, bool includeLineNumbers, List<Regex> removeAllLineContainsPatternList, List<string> fileInstructions)
    {
        var formatted = GetFormattedFileContent(fileName, includeLineContainsPatternList, includeLineCountBefore, includeLineCountAfter, includeLineNumbers, removeAllLineContainsPatternList);

        var afterInstructions = fileInstructions.Any()
            ? ApplyFileInstructions(fileInstructions, formatted)
            : formatted;

        Console.WriteLine(afterInstructions);
    }

    private static string GetFormattedFileContent(string fileName, List<Regex> includeLineContainsPatternList, int includeLineCountBefore, int includeLineCountAfter, bool includeLineNumbers, List<Regex> removeAllLineContainsPatternList)
    {
        try
        {
            var bytes = File.ReadAllBytes(fileName);
            var isBinary = bytes.Any(x => x == 0);
            if (isBinary)
            {
                return $"## {fileName}\n\nBinary data: {bytes.Length} bytes\n\n";
            }

            var content = File.ReadAllText(fileName, Encoding.UTF8);

            var isMarkdown = fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
            var backticks = isMarkdown
                ? new string('`', GetMaxBacktickCharSequence(content))
                : "```";

            var filterContent = includeLineContainsPatternList.Any() || removeAllLineContainsPatternList.Any();
            if (filterContent)
            {
                content = GetFilteredContent(content, includeLineContainsPatternList, includeLineCountBefore, includeLineCountAfter, includeLineNumbers, removeAllLineContainsPatternList, backticks);
            }
            else if (includeLineNumbers)
            {
                var lines = content.Split('\n');
                content = string.Join('\n', lines.Select((line, index) => $"{index + 1}: {line}"));
            }

            return $"## {fileName}\n\n{backticks}\n{content}\n{backticks}\n";
        }
        catch (Exception ex)
        {
            return $"## {fileName} - Error reading file: {ex.Message}\n\n";
        }
    }

    private static string GetFilteredContent(string content, List<Regex> includeLineContainsPatternList, int includeLineCountBefore, int includeLineCountAfter, bool includeLineNumbers, List<Regex> removeAllLineContainsPatternList, string backticks)
    {
        var allLines = content.Split('\n');
        var matchedLineIndices = new List<int>();

        // 1. Identify which lines match the include/exclude line patterns.
        for (int i = 0; i < allLines.Length; i++)
        {
            var line = allLines[i];
            if (IsLineMatch(line, includeLineContainsPatternList, removeAllLineContainsPatternList))
            {
                matchedLineIndices.Add(i);
            }
        }

        // If no lines matched, we can simply return empty content (or original if you prefer).
        if (matchedLineIndices.Count == 0)
        {
            return string.Empty;
        }

        // 2. Include lines before and after the matched lines.
        //    We'll create a HashSet of all line indices to include to avoid duplicates.
        var linesToInclude = new HashSet<int>();

        foreach (var matchIndex in matchedLineIndices)
        {
            // Add the matched line itself
            linesToInclude.Add(matchIndex);

            // Add lines before
            for (int b = 1; b <= includeLineCountBefore; b++)
            {
                var idxBefore = matchIndex - b;
                if (idxBefore >= 0) linesToInclude.Add(idxBefore);
            }

            // Add lines after
            for (int a = 1; a <= includeLineCountAfter; a++)
            {
                var idxAfter = matchIndex + a;
                if (idxAfter < allLines.Length) linesToInclude.Add(idxAfter);
            }
        }

        // 3. Convert the included lines to a list and sort by line index
        var finalLineIndices = linesToInclude.OrderBy(i => i).ToList();

        // 4. Build the output content with separators between ranges
        var output = new List<string>();
        int? previousIndex = null;

        foreach (var index in finalLineIndices)
        {
            // Add a separator if there's a break in the range
            if (previousIndex != null && index > previousIndex + 1 && includeLineCountBefore + includeLineCountAfter > 0)
            {
                output.Add($"{backticks}\n\n{backticks}");
            }

            var line = allLines[index];
            var shouldRemove = removeAllLineContainsPatternList.Any(regex => regex.IsMatch(line));

            if (includeLineNumbers)
            {
                output.Add(shouldRemove
                    ? $"{index + 1}:"
                    : $"{index + 1}: {line}");
            }
            else if (!shouldRemove)
            {
                output.Add(line);
            }

            previousIndex = index;
        }

        // 5. Join the lines back into a single string
        return string.Join("\n", output);
    }

    private static string ApplyFileInstructions(List<string> instructions, string content)
    {
        return instructions.Aggregate(content, (current, instruction) => ApplyFileInstructions(instruction, current));
    }

    private static string ApplyFileInstructions(string instructions, string content)
    {
        var userPromptFileName = Path.GetTempFileName();
        var systemPromptFileName = Path.GetTempFileName();
        var instructionsFileName = Path.GetTempFileName();
        var contentFileName = Path.GetTempFileName();
        try
        {
            var backticks = new string('`', GetMaxBacktickCharSequence(content) + 3);
            File.WriteAllText(userPromptFileName, GetUserPrompt(backticks, contentFileName, instructionsFileName));
            File.WriteAllText(systemPromptFileName, GetSystemPrompt());
            File.WriteAllText(instructionsFileName, instructions);
            File.WriteAllText(contentFileName, content);

            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "ai";
            process.StartInfo.Arguments = $"chat --user \"@{userPromptFileName}\" --system \"@{systemPromptFileName}\" --quiet true";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return process.ExitCode != 0
                ? $"{output}\n\nEXIT CODE: {process.ExitCode}\n\nERROR: {error}"
                : output;
        }
        catch (Exception ex)
        {
            return $"## {ex.Message}\n\n";
        }
        finally 
        {
            // Console.WriteLine($"user:\n{File.ReadAllText(userPromptFileName)}\n\n");
            // Console.WriteLine($"system:\n{File.ReadAllText(systemPromptFileName)}\n\n");
            // Console.WriteLine($"instructions:\n{File.ReadAllText(instructionsFileName)}\n\n");
            if (File.Exists(userPromptFileName)) File.Delete(userPromptFileName);
            if (File.Exists(systemPromptFileName)) File.Delete(systemPromptFileName);
            if (File.Exists(instructionsFileName)) File.Delete(instructionsFileName);
            if (File.Exists(contentFileName)) File.Delete(contentFileName);
        }
    }
    private static string GetSystemPrompt()
    {
        return "You are a helpful AI assistant.\n\n" +
            "You will be provided with a set of instructions and a markdown file.\n\n" +
            "Your task is to apply the instructions to the text and return the modified text.";
    }

    private static string GetUserPrompt(string backticks, string contentFile, string instructionsFile)
    {
        return 
            "Instructions:\n" + backticks + "\n{@" + instructionsFile + "}\n" + backticks + "\n\n" +
            "Markdown:\n" + backticks + "\n{@" + contentFile + "}\n" + backticks + "\n\n" +
            "Modified markdown (do not enclose in backticks):\n\n";
    }

    static int GetMaxBacktickCharSequence(string content)
    {
        int maxConsecutiveBackticks = 0;
        int currentStreak = 0;

        foreach (char c in content)
        {
            if (c == '`')
            {
                currentStreak++;
                if (currentStreak > maxConsecutiveBackticks)
                {
                    maxConsecutiveBackticks = currentStreak;
                }
            }
            else
            {
                currentStreak = 0;
            }
        }

        return Math.Max(3, maxConsecutiveBackticks + 1);
    }
}
