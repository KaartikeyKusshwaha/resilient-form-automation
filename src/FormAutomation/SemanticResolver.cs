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

    /// <summary>Chooses one form scope that best contains all requested field families.</summary>
    public ResolutionSet ResolveAll(IWebDriver driver, IReadOnlyList<SemanticField> fields, Action<string>? log = null)
    {
        var candidates = Gather(driver);
        var perField = fields.ToDictionary(field => field.Name, field =>
        {
            var scored = new List<ScoredCandidate>();
            foreach (var candidate in candidates)
            {
                var genderVocabulary = field.Kind == FieldKind.Radio && candidate.Descriptor.GroupKey.Length > 0 &&
                    CandidateScorer.HasGenderOptionVocabulary(candidates.Where(peer =>
                        peer.Descriptor.GroupKey == candidate.Descriptor.GroupKey &&
                        peer.Descriptor.RootKey == candidate.Descriptor.RootKey &&
                        peer.FramePath.SequenceEqual(candidate.FramePath)));
                var item = CandidateScorer.Score(candidate, field, genderVocabulary: genderVocabulary);
                scored.Add(item);
                if (item.Rejected && log is not null && (field.Kind == FieldKind.Radio || field.Name == Fields.Gender.Name))
                    log($"  {field.Name} rejected <{candidate.Descriptor.Tag}> label='{Short(candidate.Descriptor.Label)}': {item.RejectReason}; visible={candidate.Descriptor.SelfVisible}; labelVisible={candidate.Descriptor.LabelVisible}; opacityZero={candidate.Descriptor.OpacityZero}; tiny={candidate.Descriptor.TinyBox}; offscreen={candidate.Descriptor.Offscreen}.");
            }
            return scored.Where(item => !item.Rejected && item.Score >= field.MinimumScore).ToList();
        }, StringComparer.Ordinal);

        var scopes = candidates.GroupBy(ScopeId).Select(group =>
        {
            var bestByField = fields.ToDictionary(field => field.Name, field => perField[field.Name]
                .Where(item => ScopeId(item.Candidate) == group.Key)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Candidate.Descriptor.InModal)
                .ThenBy(item => item.Candidate.Descriptor.Order)
                .ToList(), StringComparer.Ordinal);
            var available = bestByField.Count(pair => pair.Value.Count > 0);
            var scoreSum = bestByField.Values.Where(items => items.Count > 0).Sum(items => items[0].Score);
            var descriptor = group.First().Descriptor;
            var mainTop = group.First().FramePath.Count == 0 && descriptor.RootKey == "main";
            var scopeScore = scoreSum + ScoringConfig.ScopeCoverageBonus * available +
                5 * Math.Log2(1 + Math.Max(0, descriptor.ScopeControlCount)) +
                (mainTop ? ScoringConfig.ScopeMainBonus : 0);
            var label = $"frame={(group.First().FramePath.Count == 0 ? "top" : string.Join("/", group.First().FramePath))} root={descriptor.RootKey} scope={descriptor.ScopeKey} controls={descriptor.ScopeControlCount}";
            return new ScopeRank(group.Key, label, scopeScore, bestByField, available);
        }).OrderByDescending(scope => scope.Score).ToList();

        if (scopes.Count == 0)
            return new ResolutionSet(new Dictionary<string, ScoredCandidate>(StringComparer.Ordinal), "none", [],
                fields.ToDictionary(field => field.Name, _ => "No form controls were discovered in the page, frames, or open shadow roots.", StringComparer.Ordinal));
        var chosen = scopes[0];
        var results = new Dictionary<string, ScoredCandidate>(StringComparer.Ordinal);
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var inside = chosen.BestByField[field.Name];
            ScoredCandidate? selected = inside.FirstOrDefault();
            if (selected is null)
            {
                selected = perField[field.Name].OrderByDescending(item => item.Score)
                    .ThenBy(item => item.Candidate.Descriptor.InModal).ThenBy(item => item.Candidate.Descriptor.Order).FirstOrDefault();
                if (selected is not null) log?.Invoke($"CrossScope: {field.Name} used a candidate outside the selected form scope.");
            }
            if (selected is null)
            {
                errors[field.Name] = ResolutionFailure(field, perField[field.Name]).Message;
                continue;
            }
            if (inside.Count > 1 && inside[0].Score - inside[1].Score <= ScoringConfig.AmbiguityMargin)
                log?.Invoke($"AMBIGUOUS '{field.Name}' in {chosen.Label}: scores {inside[0].Score:0.0}/{inside[1].Score:0.0}; preferring non-modal then earlier DOM order.");
            results[field.Name] = selected;
            if (log is not null)
                foreach (var item in perField[field.Name].Take(5))
                    log($"  {field.Name} candidate {item.Score:0.0}: <{item.Candidate.Descriptor.Tag}> label='{Short(item.Candidate.Descriptor.Label)}' scope={item.Candidate.Descriptor.ScopeKey} signals=[{string.Join("; ", item.Signals)}]");
        }
        log?.Invoke($"Scope: {chosen.Label}; score={chosen.Score:0.0}; coverage={chosen.Coverage}/{fields.Count}; alternatives={Math.Max(0, scopes.Count - 1)}.");
        return new ResolutionSet(results, chosen.Label, scopes.Skip(1).Select(item => new ScopeAlternative(item.Label, item.Score)).ToArray(), errors);
    }

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

    private static string ScopeId(DomCandidate candidate) =>
        $"{string.Join('/', candidate.FramePath)}|{candidate.Descriptor.RootKey}|{candidate.Descriptor.ScopeKey}";

    internal static IReadOnlyList<object> AsList(object? value) => value switch
    {
        IReadOnlyList<object> list => list,
        IEnumerable<object> enumerable => enumerable.ToArray(),
        IList list => list.Cast<object>().ToArray(),
        _ => []
    };

    internal static string Str(object? value) => value?.ToString() ?? "";
}

/// <summary>Selected candidates and scope details for one complete form run.</summary>
public sealed record ResolutionSet(IReadOnlyDictionary<string, ScoredCandidate> Candidates, string Scope,
    IReadOnlyList<ScopeAlternative> Alternatives, IReadOnlyDictionary<string, string> Errors);

/// <summary>A lower-ranked candidate form scope.</summary>
public sealed record ScopeAlternative(string Scope, double Score);

internal sealed record ScopeRank(string Id, string Label, double Score,
    IReadOnlyDictionary<string, List<ScoredCandidate>> BestByField, int Coverage);
