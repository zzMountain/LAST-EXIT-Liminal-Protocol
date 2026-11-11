using System.Collections;
using System.Collections.Generic;
using LastExit.Content;
using LastExit.Domain;
using LastExit.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastExit.Tests.Gameplay.PlayMode
{
    public class CctvPanelPlayModeTests
    {
        private LevelSecurityPanel _panel;
        private BoardSession _board;
        private LevelBoardView _view;
        private Camera _camera;
        private Canvas _canvas;
        private bool _paused;
        private bool _anomalies;
        private string _quarantined;
        private int _quarantineRequests;

        [UnitySetUp]
        public IEnumerator LoadPanel()
        {
            yield return SceneManager.LoadSceneAsync("StationL07");

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                LevelComposition composition = root.GetComponentInChildren<LevelComposition>(true);

                if (composition != null)
                {
                    // The fixture owns a separate model; the scene coordinator must not consume its clicks.
                    composition.enabled = false;
                    root.GetComponentInChildren<LevelHud>(true).Shutdown();
                }

                LevelSecurityPanel candidate = root.GetComponentInChildren<LevelSecurityPanel>(true);

                if (candidate != null)
                {
                    _panel = candidate;
                }

                LevelBoardView view = root.GetComponentInChildren<LevelBoardView>(true);

                if (view != null)
                {
                    _view = view;
                }
            }

            Assert.IsNotNull(_panel);
            _paused = false;
            _anomalies = false;
            _quarantined = null;
            _quarantineRequests = 0;
            _board = new BoardSession(LevelJsonImporter.Parse(Resources.Load<TextAsset>("Levels/level_04").text));
            _view.Clear();
            _view.Synchronize(_board.GetSnapshot());
            _canvas = _panel.GetComponentInParent<Canvas>();
            _camera = _canvas.worldCamera;
            _panel.gameObject.SetActive(true);
            _panel.Initialize(_camera, (RectTransform)_canvas.transform, _view);
            _panel.SendMessage("OnApplicationFocus", true);
            _panel.ScanRequested += OnScan;
            _panel.QuarantineRequested += OnQuarantine;
            Refresh();
            _panel.RefreshLayout(1, true);
            yield return null;
        }

        [TearDown]
        public void Unsubscribe()
        {
            if (_panel != null)
            {
                _panel.ScanRequested -= OnScan;
                _panel.QuarantineRequested -= OnQuarantine;
            }
        }

        [UnityTest]
        public IEnumerator ChoosingAHeadEnablesOneExplicitQuarantineConfirmation()
        {
            Button selection = _panel.GetSelectionControl();
            Assert.IsTrue(selection.interactable);
            selection.onClick.Invoke();
            Assert.IsTrue(_panel.IsInspectionOpen);
            Assert.IsNull(_panel.SelectedQueue);
            Assert.IsFalse(_panel.GetQuarantineControl().interactable,
                "Before choosing a head, the action must show that selection is still required.");
            Assert.AreEqual(0, _quarantineRequests);

            Button target = _panel.GetQueueControl("C");
            Assert.IsTrue(target.interactable);
            target.onClick.Invoke();
            Assert.AreEqual("C", _panel.SelectedQueue);
            Assert.IsTrue(_panel.GetQuarantineControl().interactable);
            Assert.AreEqual(0, _quarantineRequests, "Choosing a head is not confirmation.");

            _panel.GetQuarantineControl().onClick.Invoke();
            Assert.AreEqual("C", _quarantined);
            Assert.AreEqual(1, _quarantineRequests,
                "One confirmation must submit exactly one quarantine request.");
            Assert.IsFalse(_panel.IsInspectionOpen);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverlappingPhoneTargetsSelectNearestHeadThroughActualGraphicRaycast()
        {
            _panel.GetSelectionControl().onClick.Invoke();
            Button[] targets = new Button[4];

            for (int index = 0; index < targets.Length; index++)
            {
                targets[index] = _panel.GetQueueControl("ABCD"[index].ToString());
                RectTransform rect = (RectTransform)targets[index].transform;
                rect.anchoredPosition = new Vector2(index * 21, 0);
                rect.sizeDelta = new Vector2(44, 44);
            }

            yield return null;
            Canvas.ForceUpdateCanvases();
            GraphicRaycaster raycaster = _canvas.GetComponent<GraphicRaycaster>();

            for (int index = 0; index < targets.Length; index++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, targets[index].transform.position);
                PointerEventData pointer = new PointerEventData(EventSystem.current) { position = point };
                List<RaycastResult> hits = new List<RaycastResult>();
                raycaster.Raycast(pointer, hits);
                GameObject headHit = null;

                foreach (RaycastResult hit in hits)
                {
                    Button button = hit.gameObject.GetComponentInParent<Button>();

                    foreach (Button target in targets)
                    {
                        if (button == target)
                        {
                            Assert.AreSame(targets[index], button, "An overlapping sibling stole the physical head tap.");
                            headHit = hit.gameObject;
                        }
                    }
                }

                Assert.IsNotNull(headHit, "The enlarged physical head target must remain hittable. Queue=" + index +
                    "; Point=" + point + "; Canvas=" + _canvas.renderMode + "; Active=" + targets[index].IsActive() +
                    "; Interactive=" + targets[index].IsInteractable() + "; Depth=" + targets[index].GetComponent<Image>().depth);
                ExecuteEvents.ExecuteHierarchy(headHit, pointer, ExecuteEvents.pointerClickHandler);
                Assert.AreEqual("ABCD"[index].ToString(), _panel.SelectedQueue);
                Assert.AreEqual(0, _quarantineRequests, "Selection must still require explicit confirmation.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ScanDistortsOnlyEchoOnBoardAndRestoresOnTimeoutAndDisable()
        {
            Transform echo = _view.GetPassenger("P04-A-01").GetComponentInChildren<Animator>().transform;
            Transform human = _view.GetPassenger("P04-B-01").GetComponentInChildren<Animator>().transform;
            Vector3 echoScale = echo.localScale;
            Quaternion echoRotation = echo.localRotation;
            Vector3 humanScale = human.localScale;
            Vector3 rootPosition = _view.GetPassenger("P04-A-01").position;
            float cameraSize = _camera.orthographicSize;
            _panel.GetScanControl().onClick.Invoke();
            yield return null;
            Assert.IsTrue(_panel.IsVideo);
            Assert.IsFalse(_panel.PointerRegion.gameObject.activeSelf);
            Assert.AreNotEqual(echoScale, echo.localScale, "Actual shared model must distort during scanning.");
            Assert.AreEqual(humanScale, human.localScale, "Human model must remain neutral.");
            Assert.AreEqual(rootPosition, _view.GetPassenger("P04-A-01").position);
            Assert.AreEqual(cameraSize, _camera.orthographicSize);
            yield return new WaitForSecondsRealtime(4.1f);
            Assert.IsFalse(_panel.IsVideo);
            Assert.IsFalse(_panel.IsInspectionOpen);
            Assert.AreEqual(echoScale, echo.localScale);
            Assert.That(Quaternion.Angle(echoRotation, echo.localRotation), Is.LessThan(.001f));
            _board.Restart();
            Refresh();
            _panel.GetScanControl().onClick.Invoke();
            yield return null;
            Assert.AreNotEqual(echoScale, echo.localScale);
            _panel.enabled = false;
            Assert.AreEqual(echoScale, echo.localScale);
            Assert.That(Quaternion.Angle(echoRotation, echo.localRotation), Is.LessThan(.001f));
            _panel.enabled = true;
        }

        [UnityTest]
        public IEnumerator ReinitializeWithSameGenerationStartsFirstScanAndTargetsFollowHeads()
        {
            _panel.GetScanControl().onClick.Invoke();
            yield return null;
            _board = new BoardSession(LevelJsonImporter.Parse(Resources.Load<TextAsset>("Levels/level_04").text));
            _panel.Initialize(_camera, (RectTransform)_canvas.transform, _view);
            Refresh();
            _panel.GetScanControl().onClick.Invoke();
            Assert.IsTrue(_panel.IsVideo, "The first scan in a different attempt must never be skipped.");
            yield return new WaitForSecondsRealtime(4.1f);
            _panel.GetSelectionControl().onClick.Invoke();

            foreach (QueueSnapshot queue in _board.GetSnapshot().Queues)
            {
                Button target = _panel.GetQueueControl(queue.Id);
                Assert.IsTrue(target.gameObject.activeInHierarchy);
                _panel.ProjectTargets(1);
                Vector3 world = _view.GetPassengerBounds(queue.RemainingPassengers[0].Id).center;
                Vector2 expected = LastExit.ArtLab.Preview.PreviewScreenProjection.Project(_camera,
                    (RectTransform)_canvas.transform, world);
                Assert.That(Vector2.Distance(expected, ((RectTransform)target.transform).anchoredPosition),
                    Is.LessThan(.1f));
            }

            Assert.IsFalse(_panel.PointerRegion.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator FourSecondVideoPausesOnFocusAndRotationKeepsChoice()
        {
            _panel.GetSelectionControl().onClick.Invoke();
            _panel.GetQueueControl("B").onClick.Invoke();
            Assert.AreEqual("B", _panel.SelectedQueue);
            _panel.GetScanControl().onClick.Invoke();
            Assert.IsTrue(_panel.IsVideo);
            Assert.IsFalse(_panel.GetQuarantineControl().interactable);
            float start = _panel.VideoRemaining;
            _panel.SendMessage("OnApplicationFocus", false);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(start, _panel.VideoRemaining, .001f);
            _panel.RefreshLayout(1, false);
            Assert.AreEqual("B", _panel.SelectedQueue);
            Assert.IsTrue(_panel.IsVideo);
            _panel.SendMessage("OnApplicationFocus", true);
            float deadline = Time.realtimeSinceStartup + 5;

            while (_panel.IsVideo && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsFalse(_panel.IsVideo);
            Assert.AreEqual(0, _panel.VideoRemaining, .001f);
            Assert.IsFalse(_panel.IsInspectionOpen, "Scan must end without an automatic selection screen.");
            Assert.IsNull(_panel.SelectedQueue);
            Assert.IsFalse(_panel.PointerRegion.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator NoChargesStillAllowsEveryHeadAndDoesNotRevealTruth()
        {
            while (_board.GetSnapshot().CctvCharges > 0)
            {
                ConsumeScan();
            }

            // Render a fresh generation baseline so this is a blind/manual choice, without starting a video.
            _panel.gameObject.SetActive(false);
            _panel.gameObject.SetActive(true);
            _board.Restart();
            Refresh();

            while (_board.GetSnapshot().CctvCharges > 0)
            {
                ConsumeScan();
            }

            Refresh();
            yield return new WaitForSecondsRealtime(4.1f);
            Assert.IsFalse(_panel.IsVideo);
            Assert.IsFalse(_panel.GetScanControl().interactable);
            Assert.IsFalse(_panel.IsInspectionOpen);
            _panel.GetSelectionControl().onClick.Invoke();

            foreach (QueueSnapshot queue in _board.GetSnapshot().Queues)
            {
                if (queue.RemainingPassengers.Count == 0)
                {
                    continue;
                }

                Button target = _panel.GetQueueControl(queue.Id);
                Assert.IsTrue(target.interactable, queue.Id);
                target.onClick.Invoke();
                Assert.AreEqual(queue.Id, _panel.SelectedQueue);
                Assert.IsTrue(_panel.GetQuarantineControl().interactable);
            }

            foreach (Text text in _panel.PointerRegion.GetComponentsInChildren<Text>(true))
            {
                StringAssert.DoesNotContain("ECHO", text.text);
            }

            _panel.GetQuarantineControl().onClick.Invoke();
            Assert.IsNotNull(_quarantined);
            Assert.IsFalse(_panel.IsInspectionOpen);
        }

        [UnityTest]
        public IEnumerator DisableAndDeactivateResetVideoAndFreshScanRunsOnceAfterReenable()
        {
            foreach (bool deactivateObject in new[] { false, true })
            {
                _board.Restart();
                Refresh();
                _panel.GetScanControl().onClick.Invoke();
                Assert.IsTrue(_panel.IsVideo);
                yield return new WaitForSecondsRealtime(.1f);

                if (deactivateObject)
                {
                    _panel.gameObject.SetActive(false);
                }
                else
                {
                    _panel.enabled = false;
                }

                Assert.IsFalse(_panel.IsVideo);
                Assert.IsFalse(_panel.IsInspectionOpen);
                Assert.IsNull(_panel.SelectedQueue);
                yield return new WaitForSecondsRealtime(.1f);
                _board.Restart();

                if (deactivateObject)
                {
                    _panel.gameObject.SetActive(true);
                }
                else
                {
                    _panel.enabled = true;
                }

                Refresh();
                Assert.IsFalse(_panel.IsVideo);
                int charges = _board.GetSnapshot().CctvCharges;
                _panel.GetScanControl().onClick.Invoke();
                Assert.IsTrue(_panel.IsVideo);
                Assert.AreEqual(charges - 1, _board.GetSnapshot().CctvCharges,
                    "Re-enabling must attach one scan handler and start one new viewing operation.");
                float remaining = _panel.VideoRemaining;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(_panel.VideoRemaining, Is.LessThan(remaining));
            }
        }

        [UnityTest]
        public IEnumerator MultipleAnomaliesPauseAndRetryDoNotAutoSelect()
        {
            _anomalies = true;
            _panel.GetScanControl().onClick.Invoke();
            Assert.IsTrue(_panel.IsVideo);
            Assert.IsNull(_panel.SelectedQueue);
            _paused = true;
            Refresh();
            float remaining = _panel.VideoRemaining;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(remaining, _panel.VideoRemaining, .001f);
            _board.Restart();
            Refresh();
            Assert.IsFalse(_panel.IsInspectionOpen);
            Assert.IsNull(_panel.SelectedQueue);
            Assert.IsFalse(_panel.IsVideo);
        }

        private void Refresh()
        {
            _panel.Render(_board.GetSnapshot(), _paused == false, _paused,
                queue => _anomalies || _board.IsCctvAnomaly(queue));
        }

        private void ConsumeScan()
        {
            Assert.IsTrue(_board.TryScan());
            BoardStep step = _board.PendingStep;
            Assert.IsTrue(_board.TryAcknowledge(step.Generation, step.Token));
        }

        private void OnScan()
        {
            ConsumeScan();
            Refresh();
        }

        private void OnQuarantine(string queue)
        {
            _quarantined = queue;
            _quarantineRequests++;
        }
    }
}
