using System;
using System.Reflection;
using LastExit.Editor.ArtLab;
using NUnit.Framework;
using UnityEngine;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabReportJsonTests
    {
        [Test]
        public void PlayReportKeepsExistingJsonKeysAndEscapedStringValues()
        {
            const string Scene = "Label \"Passed\": untouched";
            ArtLabPlayReport report = new ArtLabPlayReport
            {
                Passed = true,
                Animators = 13,
                PlayingAnimators = 12,
                MovingHierarchies = 11,
                PlacementStable = false,
                Clips = new[] { "Idle", "Walk" },
                Scene = Scene
            };
            string json = ArtLabReportJson.Serialize(report);
            const string Expected = "{\"passed\":true,\"animators\":13,\"playingAnimators\":12," +
                "\"movingHierarchies\":11,\"placementStable\":false,\"clips\":[\"Idle\",\"Walk\"]," +
                "\"scene\":\"Label \\\"Passed\\\": untouched\"}";
            Assert.That(json, Is.EqualTo(Expected));
            ArtLabPlayReport restored = ArtLabReportJson.Deserialize<ArtLabPlayReport>(json);
            Assert.That(restored.Passed, Is.True);
            Assert.That(restored.PlayingAnimators, Is.EqualTo(12));
            Assert.That(restored.Scene, Is.EqualTo(Scene));
        }

        [Test]
        public void ReaderAcceptsWhitespaceBeforeColonsWithoutRewritingEscapedText()
        {
            const string Scene = "Quoted \"passed\" : false and \"Passed\" : true";
            const string Legacy = "{ \"passed\" \t : true, \"playingAnimators\"\n : 7," +
                " \"scene\" : \"Quoted \\\"passed\\\" : false and \\\"Passed\\\" : true\" }";
            ArtLabPlayReport report = ArtLabReportJson.Deserialize<ArtLabPlayReport>(Legacy);
            Assert.That(report.Passed, Is.True);
            Assert.That(report.PlayingAnimators, Is.EqualTo(7));
            Assert.That(report.Scene, Is.EqualTo(Scene));
            string json = ArtLabReportJson.Serialize(report, true);
            Assert.That(ArtLabReportJson.Deserialize<ArtLabPlayReport>(json).Scene, Is.EqualTo(Scene));
        }

        [TestCase(null)]
        [TestCase("")]
        public void ReaderPreservesJsonUtilityEmptyInputResult(string json)
        {
            object expected = JsonUtility.FromJson(json, typeof(ArtLabPlayReport));
            Assert.That(ArtLabReportJson.Deserialize(json, typeof(ArtLabPlayReport)), Is.EqualTo(expected));
            Assert.That(ArtLabReportJson.Deserialize<ArtLabPlayReport>(json), Is.EqualTo(expected));
        }

        [TestCase("{")]
        [TestCase("not-json")]
        public void ReaderPreservesJsonUtilityInvalidInputErrors(string json)
        {
            Exception expected = Assert.Catch(() => JsonUtility.FromJson(json, typeof(ArtLabPlayReport)));
            Exception actual = Assert.Catch(() => ArtLabReportJson.Deserialize(json, typeof(ArtLabPlayReport)));
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()));
            Assert.That(actual.Message, Is.EqualTo(expected.Message));
        }

        [Test]
        public void SceneReportKeepsEveryExistingJsonKey()
        {
            ArtLabSceneReport report = new ArtLabSceneReport
            {
                UnityVersion = "2022.3.62f2",
                ScenePath = "Assets/Station.unity",
                ActiveLook = "ColdLiminal",
                ActiveRenderers = 20,
                ActiveTriangles = 1000,
                MissingScripts = 0,
                InvalidMaterials = 0,
                PassengerMeshSources = 1,
                BusMeshSources = 4,
                PassengerClips = 3,
                DoorClips = 1,
                ActiveLooks = 1,
                BuildScenes = new[] { "Assets/Gameplay.unity" },
                Passed = true
            };
            string json = ArtLabReportJson.Serialize(report);
            const string Expected = "{\"unityVersion\":\"2022.3.62f2\",\"scenePath\":\"Assets/Station.unity\"," +
                "\"activeLook\":\"ColdLiminal\",\"activeRenderers\":20,\"activeTriangles\":1000," +
                "\"missingScripts\":0,\"invalidMaterials\":0,\"passengerMeshSources\":1,\"busMeshSources\":4," +
                "\"passengerClips\":3,\"doorClips\":1,\"activeLooks\":1," +
                "\"buildScenes\":[\"Assets/Gameplay.unity\"],\"passed\":true}";
            Assert.That(json, Is.EqualTo(Expected));
            ArtLabSceneReport restored = ArtLabReportJson.Deserialize<ArtLabSceneReport>(json);
            Assert.That(restored.ActiveTriangles, Is.EqualTo(1000));
            CollectionAssert.AreEqual(report.BuildScenes, restored.BuildScenes);
            Assert.That(restored.Passed, Is.True);
        }

        [Test]
        public void AuthoredProbeReadsExistingRecoveryRecordAndWritesTheSameSchema()
        {
            const string Legacy = "{\"scene\":\"Assets/Preview.unity\"," +
                "\"originalPosition\":{\"x\":1.0,\"y\":2.0,\"z\":3.0}," +
                "\"authoredPosition\":{\"x\":1.5,\"y\":2.0,\"z\":3.0}," +
                "\"previousPaused\":true,\"previousRunInBackground\":false," +
                "\"awakeCapturedAuthoredPosition\":true,\"resetReturnedToAuthoredPosition\":true," +
                "\"savedSceneRestored\":false,\"awaitingRestore\":true,\"passed\":false,\"error\":\"pending\"}";
            Type stateType = typeof(ArtLabAuthoredBaselineProbe).GetNestedType("ProbeState", BindingFlags.NonPublic);
            Assert.That(stateType, Is.Not.Null);
            object state = ArtLabReportJson.Deserialize(Legacy, stateType);
            Assert.That(stateType.GetField("Scene").GetValue(state), Is.EqualTo("Assets/Preview.unity"));
            Assert.That(stateType.GetField("OriginalPosition").GetValue(state), Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(stateType.GetField("AuthoredPosition").GetValue(state), Is.EqualTo(new Vector3(1.5f, 2, 3)));
            Assert.That(stateType.GetField("PreviousPaused").GetValue(state), Is.True);
            Assert.That(stateType.GetField("PreviousRunInBackground").GetValue(state), Is.False);
            Assert.That(stateType.GetField("AwakeCapturedAuthoredPosition").GetValue(state), Is.True);
            Assert.That(stateType.GetField("ResetReturnedToAuthoredPosition").GetValue(state), Is.True);
            Assert.That(stateType.GetField("SavedSceneRestored").GetValue(state), Is.False);
            Assert.That(stateType.GetField("AwaitingRestore").GetValue(state), Is.True);
            Assert.That(stateType.GetField("Passed").GetValue(state), Is.False);
            Assert.That(stateType.GetField("Error").GetValue(state), Is.EqualTo("pending"));
            Assert.That(ArtLabReportJson.Serialize(state), Is.EqualTo(Legacy));
            string formatted = ArtLabReportJson.Serialize(state, true);
            Assert.That(ArtLabReportJson.Serialize(ArtLabReportJson.Deserialize(formatted, stateType)), Is.EqualTo(Legacy));
        }
    }
}
