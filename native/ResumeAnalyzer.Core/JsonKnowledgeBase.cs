using System.Text.Json;
using ResumeAnalyzer.Domain;

namespace ResumeAnalyzer.Core;

/// <summary>Loads a local, extensible JSON array of SkillDefinition records. An external file replaces the built-in taxonomy.</summary>
public sealed class JsonKnowledgeBase : IKnowledgeBase
{
    public IReadOnlyList<SkillDefinition> Skills { get; }

    public JsonKnowledgeBase(string? path = null)
    {
        using var stream = path is null
            ? typeof(JsonKnowledgeBase).Assembly.GetManifestResourceStream("ResumeAnalyzer.Core.Knowledge.skills.json")
                ?? throw new InvalidOperationException("The embedded skills knowledge base is unavailable.")
            : File.OpenRead(path);
        if (stream.CanSeek && stream.Length > 5_000_000)
            throw new ArgumentException("The knowledge base must be no larger than 5 MB.", nameof(path));
        List<SkillDefinition>? skills;
        try
        {
            skills = JsonSerializer.Deserialize<List<SkillDefinition>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The knowledge base must contain a JSON array of { name, category, aliases } objects.", nameof(path), exception);
        }
        if (skills is null || skills.Count == 0 || skills.Count > 5_000)
            throw new ArgumentException("The knowledge base must contain between 1 and 5,000 skills.", nameof(path));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in skills)
        {
            if (skill is null || string.IsNullOrWhiteSpace(skill.Name) || string.IsNullOrWhiteSpace(skill.Category)
                || skill.Aliases is null || skill.Aliases.Any(string.IsNullOrWhiteSpace)
                || skill.Name.Length > 100 || skill.Aliases.Any(alias => alias.Length > 100))
                throw new ArgumentException("Every skill needs a name, category, and aliases array; names and aliases must be 1–100 characters.", nameof(path));
            if (!names.Add(skill.Name.Trim()))
                throw new ArgumentException($"Duplicate knowledge-base skill: {skill.Name}.", nameof(path));
        }
        Skills = skills.Select(skill => skill with
        {
            Name = skill.Name.Trim(), Category = skill.Category.Trim(),
            Aliases = skill.Aliases.Select(alias => alias.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        }).ToList().AsReadOnly();
    }
}
