using System;
using UnityEngine.Serialization;

namespace LastExit.Editor.ArtLab
{
    [Serializable]
    public class ArtLabSceneReport
    {
        [FormerlySerializedAs("unityVersion")]
        public string UnityVersion;
        [FormerlySerializedAs("scenePath")]
        public string ScenePath;
        [FormerlySerializedAs("activeLook")]
        public string ActiveLook;
        [FormerlySerializedAs("activeRenderers")]
        public int ActiveRenderers;
        [FormerlySerializedAs("activeTriangles")]
        public int ActiveTriangles;
        [FormerlySerializedAs("missingScripts")]
        public int MissingScripts;
        [FormerlySerializedAs("invalidMaterials")]
        public int InvalidMaterials;
        [FormerlySerializedAs("passengerMeshSources")]
        public int PassengerMeshSources;
        [FormerlySerializedAs("busMeshSources")]
        public int BusMeshSources;
        [FormerlySerializedAs("passengerClips")]
        public int PassengerClips;
        [FormerlySerializedAs("doorClips")]
        public int DoorClips;
        [FormerlySerializedAs("activeLooks")]
        public int ActiveLooks;
        [FormerlySerializedAs("buildScenes")]
        public string[] BuildScenes;
        [FormerlySerializedAs("passed")]
        public bool Passed;
    }
}
