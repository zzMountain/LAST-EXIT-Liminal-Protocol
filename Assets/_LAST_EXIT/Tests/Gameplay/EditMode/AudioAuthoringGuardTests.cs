using System;
using System.IO;
using LastExit.Editor.Gameplay;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace LastExit.Tests.Gameplay
{
    public class AudioAuthoringGuardTests
    {
        [Test]
        public void DirtySceneRejectsAudioAuthoringBeforeExternalSourcesOrAssetWrites()
        {
            Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.MarkSceneDirty(temporary);
            string clip = "Assets/_LAST_EXIT/Audio/Clips/station_A_loop.wav";
            DateTime previousWrite = File.GetLastWriteTimeUtc(clip);

            try
            {
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(AudioAuthoring.Apply);
                Assert.That(error.Message, Is.EqualTo("Save modified scenes before authoring audio."));
                Assert.That(temporary.isLoaded, Is.True);
                Assert.That(temporary.isDirty, Is.True);
                Assert.That(File.GetLastWriteTimeUtc(clip), Is.EqualTo(previousWrite));
            }
            finally
            {
                EditorSceneManager.CloseScene(temporary, true);
            }
        }
    }
}
