using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FormAutomation;

/// <summary>Normalizes English labels for meaning-based matching.</summary>
public static class TextNormalizer
{
    private static readonly HashSet<string> StopTokens = new(StringComparer.Ordinal)
    {
        "enter", "your", "please", "select", "choose", "the", "a", "an", "required", "optional", "field"
    };

    /// <summary>Applies Unicode compatibility normalization, token cleanup, and stop-word removal.</summary>
    public static string Normalize(string? value)
    {
        var normalized = (value ?? "").Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }
        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !StopTokens.Contains(token))
            .Aggregate(new StringBuilder(), (result, token) => result.Length == 0 ? result.Append(token) : result.Append(' ').Append(token))
            .ToString();
    }

    /// <summary>Tokenizes weak id/name/class evidence before lowercasing.</summary>
    public static IReadOnlyList<string> TokenizeAttribute(string? value)
    {
        var separated = Regex.Replace(value ?? "", @"(?<=[a-z])(?=[A-Z])", " ");
        separated = Regex.Replace(separated, @"(?<=[A-Za-z])(?=\d)|(?<=\d)(?=[A-Za-z])", " ");
        return Regex.Split(separated, @"[^\p{L}\p{N}]+").Where(token => token.Length > 0)
            .Select(token => token.ToLowerInvariant()).ToArray();
    }
}

/// <summary>Measures phrase, token-set, containment, and fuzzy semantic similarity.</summary>
public static class MatchStrength
{
    /// <summary>Returns a 0..100 match strength; empty strings and substring-only matches score zero.</summary>
    public static double Of(string? text, SemanticField spec)
    {
        var normalized = TextNormalizer.Normalize(text);
        if (normalized.Length == 0) return 0;
        var candidateTokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var best = 0d;
        for (var index = 0; index < spec.Synonyms.Count; index++)
        {
            var phrase = TextNormalizer.Normalize(spec.Synonyms[index]);
            if (phrase.Length == 0) continue;
            var phraseTokens = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var canonical = index == 0;
            if (normalized == phrase) best = Math.Max(best, canonical ? 100 : 90);
            if (candidateTokens.Length == phraseTokens.Length && candidateTokens.ToHashSet(StringComparer.Ordinal).SetEquals(phraseTokens))
                best = Math.Max(best, canonical ? 95 : 85);
            if (ContainsAllTokens(candidateTokens, phraseTokens))
                best = Math.Max(best, Math.Max(50, 70 - 5 * (candidateTokens.Length - phraseTokens.Length)));

            var similarities = phraseTokens.Select(token => candidateTokens.Max(candidate => Similarity(token, candidate))).ToArray();
            if (similarities.Length > 0)
            {
                var mean = similarities.Average();
                if (mean >= 0.85) best = Math.Max(best, 60 * mean);
            }
        }
        return best;
    }

    /// <summary>Tests if a text equals an option after whole-token normalization.</summary>
    public static bool IsWholeOptionMatch(string? option, string? expected) =>
        TextNormalizer.Normalize(option) == TextNormalizer.Normalize(expected);

    private static bool ContainsAllTokens(IReadOnlyList<string> text, IReadOnlyList<string> phrase) =>
        phrase.Count > 0 && phrase.All(token => text.Contains(token, StringComparer.Ordinal));

    private static double Similarity(string left, string right)
    {
        if (left == right) return 1;
        if (left.Length == 0 || right.Length == 0) return 0;
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return 1d - (double)previous[right.Length] / Math.Max(left.Length, right.Length);
    }
}

/// <summary>Scoring engine. Visible labels dominate; id/name/class tokens are intentionally weak.</summary>
public static class CandidateScorer
{
    private static readonly HashSet<string> GenderOptions = new(StringComparer.Ordinal)
    {
        "male", "female", "other", "transgender", "non binary", "nonbinary", "prefer not to say", "man", "woman", "m", "f"
    };

    /// <summary>Compatibility facade for callers that need normalized option comparisons.</summary>
    public static string Normalize(string? value) => TextNormalizer.Normalize(value);

    /// <summary>Compatibility facade for strict whole-option text equality.</summary>
    public static bool IsWholeOptionMatch(string? option, string? expected) => MatchStrength.IsWholeOptionMatch(option, expected);

    /// <summary>Scores one candidate for one field, including visibility and type hard rejects.</summary>
    public static ScoredCandidate Score(DomCandidate candidate, SemanticField field, bool forWrite = true)
    {
        var descriptor = candidate.Descriptor;
        var reject = RejectReason(descriptor, field, forWrite);
        if (reject is not null)
            return new ScoredCandidate(candidate, double.NegativeInfinity, ["rejected: " + reject], Rejected: true, RejectReason: reject);

        var labelTexts = descriptor.LabelTexts.Concat(descriptor.LabelledByTexts)
            .Concat([descriptor.Label, descriptor.LegendText, descriptor.GroupAriaLabel]).Where(HasText).Distinct().ToArray();
        var ariaTexts = new[] { descriptor.AriaLabel, descriptor.Placeholder, descriptor.Title, descriptor.Accessible }.Where(HasText).Distinct().ToArray();
        var nearTexts = new[] { descriptor.NearLeft, descriptor.NearAbove, descriptor.PrecedingSiblingText, descriptor.Nearby }.Where(HasText).Distinct().ToArray();
        var label = FamilyStrength(labelTexts, field);
        var aria = FamilyStrength(ariaTexts, field) * ScoringConfig.AriaMultiplier;
        var near = FamilyStrength(nearTexts, field) * ScoringConfig.NearMultiplier;
        var primary = Math.Max(label, Math.Max(aria, near));
        var corroboration = Math.Min(ScoringConfig.CorroborationCap,
            (SumNonZero(label, aria, near) - primary) * ScoringConfig.CorroborationMultiplier);
        var attrTokens = TextNormalizer.TokenizeAttribute(string.Join(' ', descriptor.Id, descriptor.Name, descriptor.Classes));
        var hints = field.AttributeHints ?? [];
        var attributeCount = hints.Select(TextNormalizer.Normalize).Distinct()
            .Count(hint => attrTokens.Contains(hint, StringComparer.Ordinal));
        var attribute = Math.Min(ScoringConfig.AttributeCap, attributeCount * ScoringConfig.AttributeTokenWeight);
        var negativeText = labelTexts.Concat(ariaTexts).Concat(nearTexts)
            .Any(text => HasNegative(text, field.NegativeTokens) && !HasPositiveExact(text, field));
        var negativeAttribute = attrTokens.Any(token => field.NegativeTokens.Select(TextNormalizer.Normalize).Contains(token, StringComparer.Ordinal));
        var penalty = negativeText ? ScoringConfig.NegativeTextPenalty : negativeAttribute ? ScoringConfig.NegativeAttributePenalty : 0;
        var notes = new List<string>();
        if (label > 0) notes.Add($"label={label:0.0}");
        if (aria > 0) notes.Add($"aria={aria:0.0}");
        if (near > 0) notes.Add($"near={near:0.0}");
        if (attribute > 0) notes.Add($"attribute={attribute:0.0} (weak)");
        if (negativeText || negativeAttribute) notes.Add($"penalty={penalty:0.0}");
        if (descriptor.InModal) notes.Add("inside modal");
        if (descriptor.ScopeControlCount > 0) notes.Add($"scope controls={descriptor.ScopeControlCount}");
        var score = primary + corroboration + attribute + penalty;
        return new ScoredCandidate(candidate, score, notes, label, aria, near, attribute, penalty);
    }

    /// <summary>Counts normalized gender-like options to recover a label-less radio group.</summary>
    public static bool HasGenderOptionVocabulary(IEnumerable<DomCandidate> candidates)
    {
        var options = candidates.Select(item => TextNormalizer.Normalize(item.Descriptor.OptionText))
            .Where(value => value.Length > 0).ToHashSet(StringComparer.Ordinal);
        return options.Count(GenderOptions.Contains) >= 2;
    }

    /// <summary>Tests whether a text exactly names a gender option or its documented fallback.</summary>
    public static bool IsGenderOptionMatch(string? option, string? expected)
    {
        var actual = TextNormalizer.Normalize(option);
        var target = TextNormalizer.Normalize(expected);
        if (actual == target) return true;
        var synonyms = target switch
        {
            "male" => new[] { "m", "man" },
            "female" => new[] { "f", "woman" },
            "transgender" => new[] { "trans", "non binary", "nonbinary", "other" },
            _ => Array.Empty<string>()
        };
        return synonyms.Contains(actual, StringComparer.Ordinal);
    }

    private static string? RejectReason(CandidateDescriptor descriptor, SemanticField field, bool forWrite)
    {
        if (!(descriptor.SelfVisible || descriptor.LabelVisible) || descriptor.Offscreen || descriptor.OpacityZero || descriptor.TinyBox)
            return "not effectively visible";
        if (descriptor.AriaHidden && !descriptor.LabelVisible) return "aria-hidden without a visible label";
        if (forWrite && descriptor.Disabled) return "disabled";
        if (forWrite && descriptor.ReadOnly) return "read-only";
        if (!TypeMatches(descriptor, field)) return "wrong control type";
        return null;
    }

    private static bool TypeMatches(CandidateDescriptor descriptor, SemanticField field) => field.Kind switch
    {
        FieldKind.Text => descriptor.Kind == "text" && (field.AllowedInputTypes ?? ["", "text", "search"]).Contains(descriptor.Type, StringComparer.Ordinal),
        FieldKind.Radio => descriptor.Kind == "radio",
        FieldKind.Select => descriptor.Kind == "select",
        _ => false
    };

    private static double FamilyStrength(IEnumerable<string> texts, SemanticField field) =>
        texts.Select(text => MatchStrength.Of(text, field)).DefaultIfEmpty(0).Max();

    private static bool HasNegative(string text, IReadOnlyList<string> phrases)
    {
        var words = $" {TextNormalizer.Normalize(text)} ";
        return phrases.Select(TextNormalizer.Normalize).Where(phrase => phrase.Length > 0)
            .Any(phrase => words.Contains($" {phrase} ", StringComparison.Ordinal));
    }

    private static bool HasPositiveExact(string text, SemanticField field) =>
        field.Synonyms.Select(TextNormalizer.Normalize).Contains(TextNormalizer.Normalize(text), StringComparer.Ordinal);

    private static double SumNonZero(params double[] values) => values.Where(value => value > 0).Sum();
    private static bool HasText(string? text) => !string.IsNullOrWhiteSpace(text);
}
