using Frontier.Core;

namespace Frontier.Runtime;

// State/effect boundary for production group/selection partials. Membership,
// selection, save/replace/recall and tutorial notifications are never mocked.
// This does not exercise MonoBehaviour, HUD rendering or StartMatch lifecycle.
public sealed partial class GameSession
{
    public const int ControlGroupCount = 4;
    private readonly List<int>[] groups = Enumerable.Range(0, ControlGroupCount).Select(_ => new List<int>()).ToArray();
    public readonly HashSet<int> Selection = new();
    public PlayerView View { get; set; }
    public int LocalPlayer => View?.player ?? 0;
    public Catalog Catalog { get; set; }
    public GuidedTutorial Tutorial { get; set; }
    public bool IsTutorial => Tutorial != null;
    public bool Running { get; set; } = true;
    public bool IsReplay { get; set; }
    public bool IsCountingDown { get; set; }
    public bool SystemPaused { get; set; }
    public bool IsPaused => SystemPaused || IsCountingDown || Tutorial?.Briefing == true;
    public bool CanIssueOrders => Running && !IsReplay && !IsPaused && View != null && View.winner == -1;
    public int SelectedResource { get; set; }
    public string BuildDefinition { get; set; }
    public bool AttackMoveMode { get; set; }
    public string AbilityDefinitionId { get; set; }
    private HashSet<int> rallySelection;
    public bool RallyPointMode
    {
        get => rallySelection != null;
        set => rallySelection = value ? new HashSet<int>(Selection) : null;
    }
    public bool HasTargetingMode => BuildDefinition != null || AttackMoveMode || AbilityDefinitionId != null || RallyPointMode;
    public string Message { get; private set; }
    public bool MessageIsError { get; private set; }
    public int CancelCalls { get; private set; }
    public int TutorialRejects { get; private set; }
    public int ValidationCalls { get; private set; }
    public event Action Changed;
    public event Action<bool> Feedback;

    public void Announce(string message, bool isError = false) { Message = message; MessageIsError = isError; }
    public void CancelMode()
    {
        CancelCalls++;
        BuildDefinition = AbilityDefinitionId = null;
        AttackMoveMode = RallyPointMode = false;
    }
    private bool TutorialRejectOrder() { TutorialRejects++; return false; }
    private void ValidateAbilitySelection() { ValidationCalls++; }
    private void UpdateTutorial() { Tutorial?.Observe(Selection); }

    // Explicit fixture lifecycle reset, not a substitute test for StartMatch.
    public void ResetFixtureGroups()
    {
        foreach (var group in groups) group.Clear();
        Selection.Clear(); SelectedResource = 0;
    }
}
