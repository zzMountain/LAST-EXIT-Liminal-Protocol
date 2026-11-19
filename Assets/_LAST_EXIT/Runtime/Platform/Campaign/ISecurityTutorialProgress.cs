namespace LastExit.Platform
{
    public interface ISecurityTutorialProgress
    {
        public bool HasShownAnomalyHint { get; }
        public bool HasShownQuarantineHint { get; }
        public void MarkAnomalyHintShown();
        public void MarkQuarantineHintShown();
    }
}
