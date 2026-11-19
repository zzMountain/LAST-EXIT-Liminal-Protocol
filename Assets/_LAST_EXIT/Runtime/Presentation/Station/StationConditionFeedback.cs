using System;
using System.Collections;
using LastExit.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastExit.Presentation
{
    // Presents committed station condition only; owns no rules, passengers or input projection.
    public class StationConditionFeedback : MonoBehaviour
    {
        private const int StageCount = 3;
        [SerializeField, Min(.05f)] private float _shakeSeconds = .3f;
        [SerializeField, Min(.01f)] private float _shakeDistance = .14f;
        [SerializeField, Min(.1f)] private float _flickerSeconds = .6f;
        [SerializeField] private Transform _decorationParent;
        [SerializeField] private Light[] _stationLights;
        [SerializeField] private Transform[] _shakeRoots;
        private readonly GameObject[] _stages = new GameObject[StageCount];
        private Camera _camera;
        private Transform[] _worldRoots = Array.Empty<Transform>();
        private Vector3[] _renderPositions = Array.Empty<Vector3>();
        private Light[] _lights = Array.Empty<Light>();
        private float[] _intensities = Array.Empty<float>();
        private GameObject _decorations;
        private Material _crackMaterial;
        private Material _debrisMaterial;
        private Material _lampMaterial;
        private Material _lampHousingMaterial;
        private Mesh _blockMesh;
        private Coroutine _shakeFeedback;
        private Coroutine _flickerFeedback;
        private Vector3 _shakeOffset;
        private float _shakeRemaining;
        private float _flickerRemaining;
        private bool _worldOffsetApplied;
        private bool _subscribed;
        private bool _paused;
        private bool _hasSnapshot;
        private int _generation;
        private int _stability;
        private int _anomalyVersion;
        private int _damageStage;

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Cancel();
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Cancel();
            Unsubscribe();
            ReleaseDecorations();
        }

        public void Initialize(Camera camera, Bounds platformBounds)
        {
            Initialize(camera, _decorationParent, platformBounds, _stationLights, _shakeRoots);
        }

        public void Initialize(Camera camera, Transform decorationParent, Bounds platformBounds,
            Light[] stationLights = null, Transform[] worldRoots = null)
        {
            if (camera == null || decorationParent == null)
            {
                throw new ArgumentException("Station feedback requires a camera and decoration parent.");
            }

            Cancel();
            Unsubscribe();
            ReleaseDecorations();
            _camera = camera;
            _lights = stationLights == null ? Array.Empty<Light>() : (Light[])stationLights.Clone();
            _intensities = new float[_lights.Length];

            for (int index = 0; index < _lights.Length; index++)
            {
                _intensities[index] = _lights[index] != null ? _lights[index].intensity : 0;
            }

            CreateDecorations(decorationParent, platformBounds);
            _worldRoots = worldRoots == null ? new[] { _decorations.transform } : (Transform[])worldRoots.Clone();
            _renderPositions = new Vector3[_worldRoots.Length];

            foreach (Transform worldRoot in _worldRoots)
            {
                if (worldRoot != null && (worldRoot.GetComponentInChildren<Camera>(true) != null ||
                    worldRoot.GetComponentInChildren<Canvas>(true) != null))
                {
                    throw new ArgumentException("Station shake roots must not contain cameras or canvases.");
                }
            }

            _hasSnapshot = false;
            ResetAttempt();

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void Synchronize(BoardSnapshot snapshot)
        {
            if (_camera == null || snapshot == null)
            {
                throw new InvalidOperationException("Initialize station feedback before synchronizing a snapshot.");
            }

            if (_hasSnapshot == false || snapshot.Generation != _generation)
            {
                ResetAttempt();
                _generation = snapshot.Generation;
                _stability = snapshot.Stability;
                _anomalyVersion = 0;
                _hasSnapshot = true;
            }

            if (snapshot.Stability < _stability)
            {
                _damageStage = Mathf.Min(StageCount, _damageStage + _stability - snapshot.Stability);
                _shakeRemaining = _shakeSeconds;
                ApplyDamageStages();
            }

            if (snapshot.AnomalyVersion > _anomalyVersion)
            {
                _flickerRemaining = _flickerSeconds;
            }

            _stability = snapshot.Stability;
            _anomalyVersion = snapshot.AnomalyVersion;

            if (_shakeRemaining > 0 && _shakeFeedback == null && isActiveAndEnabled)
            {
                _shakeFeedback = StartCoroutine(PlayShake());
            }

            if (_flickerRemaining > 0 && _flickerFeedback == null && isActiveAndEnabled)
            {
                _flickerFeedback = StartCoroutine(PlayFlicker());
            }
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;

            if (paused)
            {
                RestoreWorld();
                ApplyLighting(1);
            }
        }

        public void Cancel()
        {
            if (_shakeFeedback != null)
            {
                StopCoroutine(_shakeFeedback);
                _shakeFeedback = null;
            }

            if (_flickerFeedback != null)
            {
                StopCoroutine(_flickerFeedback);
                _flickerFeedback = null;
            }

            _shakeRemaining = 0;
            _flickerRemaining = 0;
            _shakeOffset = Vector3.zero;
            RestoreWorld();
            ApplyLighting(1);
        }

        private IEnumerator PlayShake()
        {
            while (_shakeRemaining > 0)
            {
                if (_paused == false)
                {
                    _shakeRemaining = Mathf.Max(0, _shakeRemaining - Time.deltaTime);
                    float envelope = _shakeRemaining / _shakeSeconds;
                    float elapsed = _shakeSeconds - _shakeRemaining;
                    _shakeOffset = new Vector3(Mathf.Sin(elapsed * 105), Mathf.Cos(elapsed * 137), 0) *
                        (_shakeDistance * envelope * envelope);
                }

                yield return null;
            }

            RestoreWorld();
            _shakeOffset = Vector3.zero;
            _shakeFeedback = null;
        }

        private IEnumerator PlayFlicker()
        {
            while (_flickerRemaining > 0)
            {
                if (_paused == false)
                {
                    _flickerRemaining = Mathf.Max(0, _flickerRemaining - Time.deltaTime);
                    float elapsed = _flickerSeconds - _flickerRemaining;
                    float brightness = _flickerRemaining > 0 && Mathf.Sin(elapsed * 53) > -.2f ? .15f : 1;
                    ApplyLighting(brightness);
                }

                yield return null;
            }

            ApplyLighting(1);
            _flickerFeedback = null;
        }

        private void Subscribe()
        {
            if (_subscribed || _camera == null)
            {
                return;
            }

            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed == false)
            {
                return;
            }

            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            _subscribed = false;
        }

        private void ResetAttempt()
        {
            Cancel();
            _damageStage = 0;
            ApplyDamageStages();
        }

        private void ApplyDamageStages()
        {
            for (int index = 0; index < _stages.Length; index++)
            {
                if (_stages[index] != null)
                {
                    _stages[index].SetActive(index < _damageStage);
                }
            }

            ApplyLighting(1);
        }

        private void ApplyLighting(float brightness)
        {
            for (int index = 0; index < _lights.Length; index++)
            {
                if (_lights[index] != null)
                {
                    _lights[index].intensity = _intensities[index] * brightness;
                }
            }

            if (_lampMaterial != null)
            {
                Color color = _damageStage >= 2 ? new Color(.08f, .07f, .055f) :
                    new Color(.92f, .79f, .46f) * brightness;
                _lampMaterial.SetColor("_BaseColor", color);
                _lampMaterial.color = color;
            }
        }

        private void RestoreWorld()
        {
            if (_worldOffsetApplied == false)
            {
                return;
            }

            for (int index = 0; index < _worldRoots.Length; index++)
            {
                if (_worldRoots[index] != null)
                {
                    _worldRoots[index].position = _renderPositions[index];
                }
            }

            _worldOffsetApplied = false;
        }

        private void ReleaseDecorations()
        {
            if (_decorations != null)
            {
                _decorations.SetActive(false);
                Destroy(_decorations);
            }

            Destroy(_crackMaterial);
            Destroy(_debrisMaterial);
            Destroy(_lampMaterial);
            Destroy(_lampHousingMaterial);
            Destroy(_blockMesh);
        }

        private void CreateDecorations(Transform parent, Bounds bounds)
        {
            _decorations = new GameObject("StationConditionDecorations");
            _decorations.transform.SetParent(parent, false);
            _crackMaterial = CreateMaterial("Station cracks", new Color(.045f, .038f, .031f));
            _debrisMaterial = CreateMaterial("Station rubble", new Color(.42f, .36f, .29f));
            _lampMaterial = CreateMaterial("Damaged station lamp", new Color(.92f, .79f, .46f));
            _lampHousingMaterial = CreateMaterial("Station lamp housing", new Color(.08f, .12f, .14f));
            _blockMesh = CreateBlockMesh();

            for (int index = 0; index < _stages.Length; index++)
            {
                _stages[index] = new GameObject("DamageStage" + (index + 1));
                _stages[index].transform.SetParent(_decorations.transform, false);
                _stages[index].SetActive(false);
            }

            float left = bounds.min.x + .18f;
            float right = bounds.max.x - .18f;
            float rear = bounds.max.z;

            for (int index = 0; index < 6; index++)
            {
                float z = Mathf.Lerp(bounds.min.z, bounds.max.z, (index + .5f) / 6);
                float x = index % 2 == 0 ? left : right;
                float direction = index % 2 == 0 ? 1 : -1;
                CreateCrack(_stages[0].transform, new Vector3(x, .035f, z), direction, 1);
                CreateBlock(_stages[0].transform, new Vector3(x, .055f, z + .4f),
                    new Vector3(.24f, .1f, .35f), index * 37, _debrisMaterial);
                CreateCrack(_stages[2].transform, new Vector3(x, .045f, z + .7f), direction, 1.6f);
            }

            for (int index = 0; index < 10; index++)
            {
                float x = index % 2 == 0 ? left : right;
                float z = Mathf.Lerp(bounds.min.z, bounds.max.z, (index + .5f) / 10);
                CreateBlock(_stages[1].transform, new Vector3(x, .12f, z),
                    new Vector3(.5f, .24f, .65f), index * 57, _debrisMaterial);
                CreateBlock(_stages[2].transform, new Vector3(x, .22f, z - .4f),
                    new Vector3(.7f, .44f, .8f), index * 29, _debrisMaterial);
            }

            CreateBlock(_decorations.transform, new Vector3(right - .4f, .2f, rear + .35f),
                new Vector3(1.75f, .16f, .42f), 0, _lampHousingMaterial, "Station lamp housing");
            CreateBlock(_decorations.transform, new Vector3(right - .4f, .31f, rear + .35f),
                new Vector3(1.25f, .045f, .12f), 0, _lampMaterial, "Station lamp tube");
            CreateBlock(_decorations.transform, new Vector3(right - 1.16f, .3f, rear + .35f),
                new Vector3(.12f, .15f, .42f), 0, _lampHousingMaterial, "Station lamp left mount");
            CreateBlock(_decorations.transform, new Vector3(right + .36f, .3f, rear + .35f),
                new Vector3(.12f, .15f, .42f), 0, _lampHousingMaterial, "Station lamp right mount");
            CreateBlock(_stages[1].transform, new Vector3(right - .3f, .21f, rear - .2f),
                new Vector3(1.2f, .15f, .17f), 24, _debrisMaterial);
            CreateBlock(_stages[2].transform, new Vector3(left + .3f, .35f, rear - .5f),
                new Vector3(1.4f, .7f, .9f), -17, _debrisMaterial);
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            // Lit is already retained by the authored station materials in the WebGL player.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material material = new Material(shader) { name = materialName };
            material.SetColor("_BaseColor", color);
            material.color = color;
            return material;
        }

        private void CreateCrack(Transform parent, Vector3 position, float direction, float scale)
        {
            GameObject crack = new GameObject("Floor fracture");
            crack.transform.SetParent(parent, false);
            LineRenderer line = crack.AddComponent<LineRenderer>();
            line.sharedMaterial = _crackMaterial;
            line.useWorldSpace = false;
            line.widthMultiplier = .065f * scale;
            line.positionCount = 5;
            Vector3[] points = new[]
            {
                position,
                position + new Vector3(.35f * direction, 0, .25f) * scale,
                position + new Vector3(.21f * direction, 0, .57f) * scale,
                position + new Vector3(.64f * direction, 0, .85f) * scale,
                position + new Vector3(.78f * direction, 0, 1.12f) * scale
            };

            for (int index = 0; index < points.Length; index++)
            {
                points[index] = crack.transform.InverseTransformPoint(points[index]);
            }

            line.SetPositions(points);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private void CreateBlock(Transform parent, Vector3 position, Vector3 scale, float angle, Material material,
            string objectName = "Station fragment")
        {
            GameObject block = new GameObject(objectName);
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.rotation = Quaternion.Euler(0, angle, 0);
            block.transform.localScale = scale;
            block.AddComponent<MeshFilter>().sharedMesh = _blockMesh;
            MeshRenderer renderer = block.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh CreateBlockMesh()
        {
            Mesh mesh = new Mesh { name = "Station fragment mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, -.5f, -.5f),
                new Vector3(.5f, .5f, -.5f), new Vector3(-.5f, .5f, -.5f),
                new Vector3(-.5f, -.5f, .5f), new Vector3(.5f, -.5f, .5f),
                new Vector3(.5f, .5f, .5f), new Vector3(-.5f, .5f, .5f)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2, 0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnCameraPreCull(Camera camera)
        {
            if (camera != _camera || _worldOffsetApplied || _paused || _shakeRemaining <= 0)
            {
                return;
            }

            Vector3 offset = -camera.transform.right * _shakeOffset.x - camera.transform.up * _shakeOffset.y;

            for (int index = 0; index < _worldRoots.Length; index++)
            {
                Transform worldRoot = _worldRoots[index];

                if (worldRoot != null)
                {
                    _renderPositions[index] = worldRoot.position;
                    worldRoot.position = _renderPositions[index] + offset;
                }
            }

            // Keep the actual camera and camera-space Canvas completely unchanged.
            // Root transforms are restored before input, layout or the next simulation step.
            _worldOffsetApplied = true;
        }

        private void OnCameraPostRender(Camera camera)
        {
            if (camera == _camera)
            {
                RestoreWorld();
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            OnCameraPreCull(camera);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            OnCameraPostRender(camera);
        }
    }
}
