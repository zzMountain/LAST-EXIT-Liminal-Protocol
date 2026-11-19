using LastExit.Domain;
using LastExit.Platform;

namespace LastExit.Presentation
{
    // Tracks the two non-blocking first-anomaly instructions independently of attempt restarts.
    public class SecurityTutorial
    {
        private const string IntroductionLevel = "LEVEL-04";
        private readonly ISecurityTutorialProgress _progress;
        private readonly bool _isIntroduction;
        private bool _anomalyShown;
        private bool _quarantineShown;
        private bool _showAnomaly;
        private bool _showQuarantine;
        private int _generation = -1;
        private int _isolationCount;

        public SecurityTutorial(string levelId, ISecurityTutorialProgress progress = null)
        {
            _isIntroduction = levelId == IntroductionLevel;
            _progress = progress;
            _anomalyShown = progress != null && progress.HasShownAnomalyHint;
            _quarantineShown = progress != null && progress.HasShownQuarantineHint;
        }

        public bool ShowsAnomalyHint => _showAnomaly;

        public string Text => _showQuarantine
            ? GameLocalization.Get("security.tutorialQuarantine")
            : _showAnomaly ? GameLocalization.Get("security.tutorialAnomaly")
            : string.Empty;

        public void Observe(BoardSnapshot snapshot, bool video, bool choosing)
        {
            if (_generation != snapshot.Generation)
            {
                _generation = snapshot.Generation;
                _showAnomaly = false;
                _showQuarantine = false;
                _isolationCount = snapshot.IsolationCount;
            }

            if (_isIntroduction == false)
            {
                return;
            }

            if (snapshot.AnomalyVersion > 0 && _anomalyShown == false)
            {
                _anomalyShown = true;
                _showAnomaly = true;
                _progress?.MarkAnomalyHintShown();
            }

            if (video)
            {
                _showAnomaly = false;
            }

            if (snapshot.CctvUses > 0 && choosing && _quarantineShown == false)
            {
                _quarantineShown = true;
                _showQuarantine = true;
                _progress?.MarkQuarantineHintShown();
            }

            if (_isolationCount != snapshot.IsolationCount || snapshot.IsDefeat || snapshot.IsVictory)
            {
                _showAnomaly = false;
                _showQuarantine = false;
                _isolationCount = snapshot.IsolationCount;
            }

            if (choosing == false)
            {
                _showQuarantine = false;
            }
        }
    }
}
