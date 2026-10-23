using Frontier.Core;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        public GuidedTutorial Tutorial { get; private set; }
        public bool IsTutorial => Tutorial != null;
        private MapDefinition tutorialSetupMap;
        private string tutorialSetupMapId;
        private bool tutorialTouch;

        // A separate, local lesson: no strategist, no recording and no change to
        // the player's skirmish setup or the shared simulation rules.
        public void StartTutorial(bool touch = false, bool countdown = false)
        {
            if (IsReplay) StopReplay();
            if (Running) LeaveMatch();
            DisconnectOnline();
            CloseArchive();
            tutorialSetupMap = Map; tutorialSetupMapId = MapId; tutorialTouch = touch;
            LoadMap("training");
            Match = new Simulation(Map, Catalog, Faction, EnemyFaction);
            Tutorial = new GuidedTutorial(Match, touch);
            bot = null; LastClosedWasReplay = false;
            ArchiveStatus = null; ArchiveFailed = false;
            Match.Notice += (player, message) => { if (player == 0) Announce(message); };
            Selection.Clear(); SelectedResource = 0; foreach (var group in groups) group.Clear();
            BuildDefinition = null; AttackMoveMode = false; AbilityDefinitionId = null;
            LastCommandSequence = 0; sequence = 0; accumulator = 0;
            PrepareCountdown(countdown ? StartCountdownSeconds : 0);
            pauseReasons.Remove("menu"); pauseReasons.Remove("tutorial");
            Running = true; View = Match.View(0);
            SynchronizeTutorial();
            Announce("Follow the highlighted objective. The enemy waits while you learn.");
            NewMatch?.Invoke(); Changed?.Invoke();
        }

        public void RestartTutorial()
        {
            if (!IsTutorial) return;
            StartTutorial(tutorialTouch);
        }

        public void ContinueTutorial()
        {
            if (!IsTutorial || HasSystemPause || IsCountingDown || pauseReasons.Contains("placement")) return;
            Tutorial.Continue();
            SynchronizeTutorial(); Changed?.Invoke();
        }

        public void NotifyTutorialMenu(bool open)
        {
            if (!IsTutorial || HasSystemPause || IsCountingDown) return;
            Tutorial.NotifyMenu(open); SynchronizeTutorial();
        }

        public void NotifyTutorialWheel()
        {
            if (!IsTutorial || !CanIssueOrders) return;
            Tutorial.NotifyWheel(); SynchronizeTutorial(); Changed?.Invoke();
        }

        private void UpdateTutorial()
        {
            if (!IsTutorial || HasSystemPause || IsCountingDown || pauseReasons.Contains("placement") || pauseReasons.Contains("menu")) return;
            var step = Tutorial.Step; bool briefing = Tutorial.Briefing;
            Tutorial.Observe(Selection);
            SynchronizeTutorial();
            if (step != Tutorial.Step || briefing != Tutorial.Briefing)
            {
                BuildDefinition = null; AttackMoveMode = false; AbilityDefinitionId = null;
                Announce(Tutorial.Complete ? "Tutorial complete. You led your first victory." : "Objective complete. Read the next lesson.");
                Changed?.Invoke();
            }
        }

        private void SynchronizeTutorial()
        {
            if (Tutorial != null && (Tutorial.Briefing || Tutorial.Complete) && !IsCountingDown)
                pauseReasons.Add("tutorial");
            else pauseReasons.Remove("tutorial");
            if (Match != null) Match.Paused = IsPaused;
        }

        private void CloseTutorial()
        {
            if (!IsTutorial) return;
            Tutorial = null; pauseReasons.Remove("tutorial");
            Map = tutorialSetupMap; MapId = tutorialSetupMapId;
            tutorialSetupMap = null; tutorialSetupMapId = null;
        }

        private bool TutorialRejectOrder()
        {
            Announce("Follow the current tutorial objective before issuing another order.", true);
            Feedback?.Invoke(false); Changed?.Invoke(); return false;
        }

        public bool TutorialAllowsAction(SelectionAction action)
        {
            if (!IsTutorial) return true;
            if (Tutorial.Briefing || Tutorial.Complete) return false;
            switch (Tutorial.Step)
            {
                // Let the learner inspect the wheel before choosing a blueprint.
                case TutorialStep.OpenWheel: return action.Category == ActionCategory.Build;
                case TutorialStep.BuildSupply: case TutorialStep.BuildBarracks: case TutorialStep.BuildExtractor:
                    return action.Id == "build:" + Tutorial.ExpectedDefinition;
                case TutorialStep.TrainWorker: case TutorialStep.TrainArmy:
                    return action.Id == "train:" + Tutorial.ExpectedDefinition;
                case TutorialStep.AttackMove: case TutorialStep.DestroyEnemy:
                    return action.Id == "attack-move";
                default: return false;
            }
        }

        private bool TutorialCanChooseBuild(string definition) => !IsTutorial || !Tutorial.Briefing
            && (Tutorial.Step == TutorialStep.BuildSupply || Tutorial.Step == TutorialStep.BuildBarracks || Tutorial.Step == TutorialStep.BuildExtractor)
            && definition == Tutorial.ExpectedDefinition;

        private bool TutorialCanChooseAttack => !IsTutorial || !Tutorial.Briefing
            && (Tutorial.Step == TutorialStep.AttackMove || Tutorial.Step == TutorialStep.DestroyEnemy);
    }
}
