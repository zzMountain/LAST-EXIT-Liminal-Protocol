using System;
using System.Reflection;
using LastExit.Editor.ArtLab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabRenderingLifetimeTests
    {
        private const string TestingKey = "LastExit.ArtLab.Rendering.Testing";

        [Test]
        public void FailedTestRunReleasesTheRenderingSuppression()
        {
            bool previous = SessionState.GetBool(TestingKey, false);
            Type callbackType = typeof(ArtLabRendering).GetNestedType("TestRunCallbacks", BindingFlags.NonPublic);
            IErrorCallbacks callbacks = (IErrorCallbacks)Activator.CreateInstance(callbackType, true);

            try
            {
                callbacks.RunStarted(null);
                Assert.That(SessionState.GetBool(TestingKey, false), Is.True);
                callbacks.OnError("Synthetic prebuild error; no runner was started.");
                Assert.That(SessionState.GetBool(TestingKey, true), Is.False);
            }
            finally
            {
                SessionState.SetBool(TestingKey, previous);
            }
        }
    }
}
