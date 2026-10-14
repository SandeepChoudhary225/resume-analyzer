using ResumeAnalyzer.Domain;
namespace ResumeAnalyzer.Application;
public static class SectionEditor
{
    private static readonly HashSet<string> Headings = new(StringComparer.OrdinalIgnoreCase)
    { "Summary", "Professional Summary", "Profile", "Skills", "Technical Skills", "Technical Tools", "Experience", "Work Experience", "Professional Experience", "Education", "Certifications", "Projects", "Achievements", "Languages", "Awards", "Publications", "Volunteer Experience", "Interests" };
    public static List<SectionData> Split(string text)
    {
        var result = new List<SectionData>(); string current = "Contact"; bool hasHeading = false;
        var lines = new List<string>();
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            var trim = line.Trim().TrimEnd(':');
            if (Headings.Contains(trim) || trim.StartsWith("[Section: ", StringComparison.Ordinal) && trim.EndsWith(']'))
            {
                if (lines.Any(l => !string.IsNullOrWhiteSpace(l)) || hasHeading) result.Add(new(current, string.Join('\n', lines).Trim()));
                current = trim.StartsWith("[Section: ", StringComparison.Ordinal) ? trim[10..^1] : trim; lines.Clear(); hasHeading = true;
            }
            else lines.Add(line);
        }
        if (lines.Count > 0 || result.Count == 0 || hasHeading) result.Add(new(current, string.Join('\n', lines).Trim()));
        return result;
    }
    public static string Join(IEnumerable<SectionData> sections) => string.Join("\n\n", sections.Select((s, index) => index == 0 && s.Name.Equals("Contact", StringComparison.OrdinalIgnoreCase) ? s.Text : (Headings.Contains(s.Name) ? s.Name : "[Section: " + s.Name + "]") + "\n" + s.Text));
    public static void Move(List<SectionData> sections, int index, int delta)
    {
        var target = index + delta;
        if (target < 0 || target >= sections.Count) return;
        var section = sections[index]; sections.RemoveAt(index); sections.Insert(target, section);
    }
}
