using System.Collections;
using FormAutomation.Discovery;

namespace FormAutomation;

/// <summary>Semantic resolver that scores meaning and prefers a complete primary form scope.</summary>
public sealed class SemanticResolver
{
    private readonly CandidateCollector _collector = new();
    private CandidateCollection? _lastCollection;

    /// <summary>Collects controls in the current document and its nested browsing contexts.</summary>
    public IReadOnlyList<DomCandidate> Gather(IWebDriver driver) =>
        (_lastCollection = _collector.Collect(driver)).Candidates;

    /// <summary>Resolves a single field using semantic evidence and whole-word matching.</summary>
    public ScoredCandidate Resolve(IWebDriver driver, SemanticField field, Action<string>? log = null)
    {
        var scored = Gather(driver).Select(candidate => CandidateScorer.Score(candidate, field))
            .Where(item => !item.Rejected)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.Descriptor.InModal)
            .ThenBy(item => item.Candidate.Descriptor.Order)
            .ToList();
        if (scored.Count == 0 || scored[0].Score < field.MinimumScore)
            throw ResolutionFailure(field, scored);
        if (scored.Count > 1 && scored[0].Score - scored[1].Score <= ScoringConfig.AmbiguityMargin)
            log?.Invoke($"AMBIGUOUS '{field.Name}': top scores {scored[0].Score:0.0}/{scored[1].Score:0.0}; preferring non-modal then earlier DOM order.");
        if (log is not null)
        {
            foreach (var candidate in scored.Take(5))
                log($"  {field.Name} candidate {candidate.Score:0.0}: <{candidate.Candidate.Descriptor.Tag}> label='{Short(candidate.Candidate.Descriptor.Label)}' signals=[{string.Join("; ", candidate.Signals)}]");
        }
        return scored[0];
    }

    /// <summary>Re-enters the candidate's nested frame context before interaction.</summary>
    public void SwitchToCandidateContext(IWebDriver driver, DomCandidate candidate) =>
        _collector.EnterFramePath(driver, candidate.FramePath);

    /// <summary>Returns the latest frame and shadow-root counts for diagnostics.</summary>
    public CandidateCollection? LastCollection => _lastCollection;

    private static InvalidOperationException ResolutionFailure(SemanticField field, IReadOnlyList<ScoredCandidate> scored)
    {
        var summary = scored.Count == 0 ? "  none visible or compatible" : string.Join(Environment.NewLine,
            scored.Take(3).Select(item => $"  score={item.Score:0.0} <{item.Candidate.Descriptor.Tag}> label='{Short(item.Candidate.Descriptor.Label)}' {string.Join("; ", item.Signals)}"));
        return new InvalidOperationException($"Could not resolve {field.Name}; top candidates:{Environment.NewLine}{summary}{Environment.NewLine}Use --verbose or --dump-html to inspect the page.");
    }

    private static string Short(string text) => text.Length > 70 ? text[..70] + "…" : text;

    internal static IReadOnlyList<object> AsList(object? value) => value switch
    {
        IReadOnlyList<object> list => list,
        IEnumerable<object> enumerable => enumerable.ToArray(),
        IList list => list.Cast<object>().ToArray(),
        _ => []
    };

    internal static string Str(object? value) => value?.ToString() ?? "";
}
