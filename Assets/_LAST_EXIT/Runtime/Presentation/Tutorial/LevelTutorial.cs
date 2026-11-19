using System;
using LastExit.Domain;
using LastExit.Platform;

namespace LastExit.Presentation
{
    public class LevelTutorial : ILevelTutorialReadout
    {
        private readonly string _successorBusId;
        private readonly bool _isFirstLevel;
        private readonly string _objectiveHintKey;

        public LevelTutorial(LevelDefinition level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            _isFirstLevel = level.Id == "LEVEL-01";
            _objectiveHintKey = GetObjectiveHintKey(level.Id);
            RowDefinition firstRow = level.Rows[0];

            if (_isFirstLevel && level.NextBusIds.Count == 0)
            {
                throw new ArgumentException("The first tutorial requires a successor in the level FIFO.", nameof(level));
            }

            FirstRowId = firstRow.Id;
            FirstBusId = firstRow.InitialBusId;
            TargetLeftX = level.Corridors["A"] - 1;
            _successorBusId = level.NextBusIds.Count == 0 ? null : level.NextBusIds[0];
            Reset();
        }

        public TutorialStep Step { get; private set; }
        public string FirstRowId { get; }
        public string FirstBusId { get; }
        public int TargetLeftX { get; }
        public bool IsComplete => Step == TutorialStep.Complete;

        public string Text
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.AlignFirstBus:
                        return GameLocalization.Get("tutorial.align");
                    case TutorialStep.Boarding:
                        return GameLocalization.Get("tutorial.boarding");
                    case TutorialStep.Departing:
                        return GameLocalization.Get("tutorial.departing");
                    case TutorialStep.ArrivalExplanation:
                        return GameLocalization.Get("tutorial.arrival");
                    case TutorialStep.Complete:
                        return GameLocalization.Get(_objectiveHintKey);
                    default:
                        return string.Empty;
                }
            }
        }

        public bool CanSelect(string busId)
        {
            return IsComplete || (Step == TutorialStep.AlignFirstBus && busId == FirstBusId) ||
                (Step == TutorialStep.ArrivalExplanation && busId == _successorBusId);
        }

        public bool CanMove(string busId, int leftX)
        {
            return IsComplete || (CanSelect(busId) && leftX == TargetLeftX);
        }

        public void Observe(BoardStep step)
        {
            if (step == null || step.BusId != FirstBusId || step.RowId != FirstRowId)
            {
                return;
            }

            if (step.Kind == BoardStepKind.Board && Step == TutorialStep.AlignFirstBus)
            {
                Step = TutorialStep.Boarding;
            }
            else if (step.Kind == BoardStepKind.Depart && Step == TutorialStep.Boarding)
            {
                Step = TutorialStep.Departing;
            }
        }

        public void Complete(BoardStep step)
        {
            if (step != null && Step == TutorialStep.Departing && step.Kind == BoardStepKind.Arrive
                && step.RowId == FirstRowId && step.BusId == _successorBusId)
            {
                Step = TutorialStep.ArrivalExplanation;
            }
        }

        public void Continue()
        {
            if (Step == TutorialStep.ArrivalExplanation)
            {
                Step = TutorialStep.Complete;
            }
        }

        public void Reset()
        {
            Step = _isFirstLevel ? TutorialStep.AlignFirstBus : TutorialStep.Complete;
        }

        private static string GetObjectiveHintKey(string levelId)
        {
            switch (levelId)
            {
                case "LEVEL-05":
                    return "tutorial.objective05";
                case "LEVEL-06":
                    return "tutorial.objective06";
                case "LEVEL-07":
                    return "tutorial.objective07";
                case "LEVEL-08":
                    return "tutorial.objective08";
                case "LEVEL-10":
                    return "tutorial.objective10";
                case "LEVEL-13":
                    return "tutorial.objective13";
                default:
                    return string.Empty;
            }
        }
    }
}
