using FormAutomation;

namespace FormAutomation.Tests.Unit;

public sealed class MatchingUnitTests
{
    [TestCase("FIRST NAME *", "first name")]
    [TestCase("Select State", "state")]
    [TestCase("Crème Brûlée", "creme brulee")]
    public void NormalizeRemovesPunctuationDiacriticsAndPromptWords(string input, string expected) =>
        Assert.That(TextNormalizer.Normalize(input), Is.EqualTo(expected));

    [Test]
    public void TokenizeAttributeSplitsCamelSnakeKebabAndDigits() =>
        Assert.That(TextNormalizer.TokenizeAttribute("txtFirstName_01"), Is.EqualTo(new[] { "txt", "first", "name", "01" }));

    [Test]
    public void MaleDoesNotMatchFemaleBySubstring()
    {
        Assert.That(MatchStrength.Of("Female", Fields.Gender), Is.Zero);
        Assert.That(CandidateScorer.IsGenderOptionMatch("Female", "Male"), Is.False);
        Assert.That(CandidateScorer.IsGenderOptionMatch("Male", "Male"), Is.True);
    }

    [Test]
    public void ExactLabelOutweighsChangedAttributes()
    {
        var candidate = Candidate("First Name", "text") with
        {
            Id = "random-4821", Name = "x77", Classes = "c9", Attributes = "random-4821 x77 c9"
        };
        var score = CandidateScorer.Score(new DomCandidate(null!, candidate, []), Fields.FirstName);
        Assert.That(score.Score, Is.GreaterThanOrEqualTo(100));
    }

    [Test]
    public void LastNameIsRejectedByNegativeEvidence()
    {
        var candidate = Candidate("Last Name", "text");
        var score = CandidateScorer.Score(new DomCandidate(null!, candidate, []), Fields.FirstName);
        Assert.That(score.Penalty, Is.EqualTo(ScoringConfig.NegativeTextPenalty));
        Assert.That(score.Score, Is.LessThan(Fields.FirstName.MinimumScore));
    }

    [Test]
    public void SelectOptionTextIsNeverALabelSignal()
    {
        var candidate = Candidate("", "select") with { Options = [new OptionDescriptor("State", "", false, true)] };
        var score = CandidateScorer.Score(new DomCandidate(null!, candidate, []), Fields.State);
        Assert.That(score.Score, Is.Zero);
    }

    [Test]
    public void HiddenHoneypotIsRejectedBeforeScoring()
    {
        var candidate = Candidate("First Name", "text") with { SelfVisible = false, Offscreen = true };
        var score = CandidateScorer.Score(new DomCandidate(null!, candidate, []), Fields.FirstName);
        Assert.That(score.Rejected, Is.True);
        Assert.That(score.RejectReason, Is.EqualTo("not effectively visible"));
    }

    private static CandidateDescriptor Candidate(string label, string kind) => new(
        label,
        "",
        "",
        "",
        kind,
        kind == "text" ? "text" : "",
        "",
        "form:1",
        false,
        0)
    {
        Tag = kind == "text" ? "input" : kind,
        LabelTexts = label.Length == 0 ? [] : [label],
        SelfVisible = true,
        ScopeKey = "form:1",
        ScopeControlCount = 10
    };
}
