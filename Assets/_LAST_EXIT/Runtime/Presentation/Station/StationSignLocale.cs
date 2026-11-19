using LastExit.Platform;
using UnityEngine;

namespace LastExit.Presentation
{
    [RequireComponent(typeof(TextMesh))]
    public class StationSignLocale : MonoBehaviour
    {
        [SerializeField] private string _suffix;
        private TextMesh _sign;

        private void Awake()
        {
            _sign = GetComponent<TextMesh>();
        }

        private void OnEnable()
        {
            GameLocalization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameLocalization.Changed -= Refresh;
        }

        private void Refresh()
        {
            _sign.text = GameLocalization.Get("board.quarantine") + _suffix;
        }
    }
}
