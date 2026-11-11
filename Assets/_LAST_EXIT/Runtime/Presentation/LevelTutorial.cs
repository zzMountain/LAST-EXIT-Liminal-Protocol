using System;
using LastExit.Domain;

namespace LastExit.Presentation
{
    public class LevelTutorial : ILevelTutorialReadout
    {
        private readonly string _successorBusId;
        private readonly bool _isFirstLevel;
        private readonly string _objectiveHint;

        public LevelTutorial(LevelDefinition level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            _isFirstLevel = level.Id == "LEVEL-01";
            _objectiveHint = GetObjectiveHint(level.Id);
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
                        return "Красный закрывает путь бирюзовым.\nСдвиньте его влево к красной очереди.";
                    case TutorialStep.Boarding:
                        return "Очереди идут общим тактом.\nСледующая группа ждёт прибытия всех.";
                    case TutorialStep.Departing:
                        return "Полный автобус уезжает";
                    case TutorialStep.ArrivalExplanation:
                        return "Следующий красный уже на месте.\nСдвиньте его к красной очереди.";
                    case TutorialStep.Complete:
                        return _objectiveHint;
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

        private static string GetObjectiveHint(string levelId)
        {
            switch (levelId)
            {
                case "LEVEL-05":
                    return "CCTV проверяет только головы очередей.\nНовая голова требует нового наблюдения.";
                case "LEVEL-06":
                    return "Следующие автобусы ждут полного выезда.\nОсвободите ряд для общего потока.";
                case "LEVEL-07":
                    return "Теперь четыре ряда. Первый корпус\nперекрывает путь к автобусам ниже.";
                case "LEVEL-08":
                    return "Один CCTV проверяет все головы сразу.\nВыбирайте карантин по наблюдению на поле.";
                case "LEVEL-10":
                    return "LONG: четыре клетки, шесть мест.\nДлинный корпус закрывает больше путей.";
                case "LEVEL-13":
                    return "Теперь пять рядов. Размер автобуса\nне закреплён за номером ряда.";
                default:
                    return string.Empty;
            }
        }
    }
}
