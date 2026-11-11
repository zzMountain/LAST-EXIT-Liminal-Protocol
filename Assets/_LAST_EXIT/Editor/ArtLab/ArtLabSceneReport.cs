using System;

namespace LastExit.Editor.ArtLab
{
    [Serializable]
    public class ArtLabSceneReport
    {
        public string unityVersion;
        public string scenePath;
        public string activeLook;
        public int activeRenderers;
        public int activeTriangles;
        public int missingScripts;
        public int invalidMaterials;
        public int passengerMeshSources;
        public int busMeshSources;
        public int passengerClips;
        public int doorClips;
        public int activeLooks;
        public string[] buildScenes;
        public bool passed;
    }
}
