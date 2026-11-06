namespace FormAutomation;

/// <summary>Control family expected by a semantic field resolver.</summary>
public enum FieldKind { Text, Radio, Select }

/// <summary>Human meaning and allowed control shape for a target field.</summary>
public sealed record SemanticField(
    string Name,
    FieldKind Kind,
    IReadOnlyList<string> Synonyms,
    IReadOnlyList<string> NegativeTokens,
    double MinimumScore = ScoringConfig.AcceptanceThreshold,
    IReadOnlyList<string>? AttributeHints = null,
    IReadOnlyList<string>? AllowedInputTypes = null);

/// <summary>One candidate element and the semantic evidence collected around it.</summary>
public sealed record CandidateDescriptor(
    string Label,
    string Accessible,
    string Nearby,
    string Attributes,
    string Kind,
    string Type,
    string OptionText,
    string FormKey,
    bool InModal,
    int Order)
{
    public string Tag { get; init; } = "";
    public string Role { get; init; } = "";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Classes { get; init; } = "";
    public string Value { get; init; } = "";
    public bool Checked { get; init; }
    public bool Disabled { get; init; }
    public bool ReadOnly { get; init; }
    public bool Required { get; init; }
    public bool SelfVisible { get; init; }
    public bool LabelVisible { get; init; }
    public bool Offscreen { get; init; }
    public bool OpacityZero { get; init; }
    public bool AriaHidden { get; init; }
    public bool TinyBox { get; init; }
    public IReadOnlyList<string> LabelTexts { get; init; } = [];
    public IReadOnlyList<string> LabelledByTexts { get; init; } = [];
    public string AriaLabel { get; init; } = "";
    public string Placeholder { get; init; } = "";
    public string Title { get; init; } = "";
    public string LegendText { get; init; } = "";
    public string GroupAriaLabel { get; init; } = "";
    public string NearLeft { get; init; } = "";
    public string NearAbove { get; init; } = "";
    public string PrecedingSiblingText { get; init; } = "";
    public string NextText { get; init; } = "";
    public IReadOnlyList<OptionDescriptor> Options { get; init; } = [];
    public string RootKey { get; init; } = "main";
    public string ScopeKey { get; init; } = "";
    public int ScopeControlCount { get; init; }
    public string GroupKey { get; init; } = "";
}

/// <summary>Option metadata for a native select.</summary>
public sealed record OptionDescriptor(string Text, string Value, bool Disabled, bool Selected);

/// <summary>Element handle plus its browsing-context path.</summary>
public sealed record DomCandidate(IWebElement Element, CandidateDescriptor Descriptor, IReadOnlyList<int> FramePath);

/// <summary>Scored semantic candidate with human-readable signals.</summary>
public sealed record ScoredCandidate(DomCandidate Candidate, double Score, IReadOnlyList<string> Signals,
    double LabelStrength = 0, double AriaStrength = 0, double NearStrength = 0, double AttributeStrength = 0,
    double Penalty = 0, bool Rejected = false, string? RejectReason = null);

/// <summary>One place for score thresholds, multipliers, and penalties.</summary>
public static class ScoringConfig
{
    public const double AcceptanceThreshold = 50;
    public const double NearMultiplier = 0.65;
    public const double AriaMultiplier = 0.8;
    public const double CorroborationMultiplier = 0.2;
    public const double CorroborationCap = 20;
    public const double AttributeTokenWeight = 8;
    public const double AttributeCap = 15;
    public const double NegativeTextPenalty = -120;
    public const double NegativeAttributePenalty = -15;
    public const double ScopeCoverageBonus = 50;
    public const double ScopeMainBonus = 3;
    public const double AmbiguityMargin = 10;
}

/// <summary>Small, extendable English synonym catalog for the requested form fields.</summary>
public static class Fields
{
    public static readonly SemanticField FirstName = new("First Name", FieldKind.Text,
        ["first name", "given name", "forename", "first", "fname", "firstname", "given"],
        ["last name", "surname", "family name", "lname", "middle name", "nickname", "user name", "username", "full name", "company", "recipient"],
        AttributeHints: ["first", "fname", "given", "forename"], AllowedInputTypes: ["", "text", "search"]);

    public static readonly SemanticField Gender = new("Gender", FieldKind.Radio,
        ["gender", "sex"], ["marital status", "status", "title", "salutation"],
        AttributeHints: ["gender", "sex"]);

    public static readonly SemanticField State = new("State", FieldKind.Select,
        ["state", "province", "region", "state province", "state or province", "county"],
        ["country", "city", "zip", "postal", "address", "status"],
        AttributeHints: ["state", "province", "region"]);

    public static readonly SemanticField Country = new("Country", FieldKind.Text,
        ["country", "nation"], ["county", "state", "city"],
        AttributeHints: ["country", "nation"], AllowedInputTypes: ["", "text", "search"]);
}
