using System.Collections.Generic;
using DeliveryRun;
using DeliveryRun.Managers.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeliveryRun.Managers.Subs
{
    public sealed class TrafficSignalVisualManager : SubManagerBase
    {
        private struct SignalVisual
        {
            public int NodeId;
            public Renderer VerticalRed;
            public Renderer VerticalYellow;
            public Renderer VerticalGreen;
            public Renderer HorizontalRed;
            public Renderer HorizontalYellow;
            public Renderer HorizontalGreen;
        }

        private const float ScenePollInterval = 0.5f;
        private const float SignalCornerOffset = 8.5f;
        private const float SignalBaseYOffset = 0.05f;

        private readonly List<SignalVisual> _visuals = new List<SignalVisual>(64);

        private TrafficRoadNetworkService _network;
        private TrafficSignalService _signals;
        private Transform _visualRoot;

        private Material _offMat;
        private Material _redMat;
        private Material _yellowMat;
        private Material _greenMat;

        private bool _isRunScene;
        private float _scenePollAccum;

        public override string Name => nameof(TrafficSignalVisualManager);
        public override int InitOrder => 68;

        protected override void OnInitialize()
        {
            Services.TryGet(out _network);
            Services.TryGet(out _signals);

            Subs.Add<SceneTransitionStarted>(Events, OnSceneTransitionStarted);
            Subs.Add<SceneTransitionCompleted>(Events, OnSceneTransitionCompleted);
            Subs.Add<TrafficSystemDefined>(Events, OnTrafficSystemDefined);

            HandleSceneChanged(SceneManager.GetActiveScene().name, true);
        }

        protected override void OnTick(float unscaledDeltaTime)
        {
            _scenePollAccum += unscaledDeltaTime;
            if (_scenePollAccum >= ScenePollInterval)
            {
                _scenePollAccum = 0f;
                HandleSceneChanged(SceneManager.GetActiveScene().name, false);
            }

            if (!_isRunScene || _visuals.Count == 0)
            {
                return;
            }

            if (_signals == null)
            {
                Services.TryGet(out _signals);
                if (_signals == null)
                {
                    return;
                }
            }

            for (int i = 0; i < _visuals.Count; i++)
            {
                SignalVisual visual = _visuals[i];
                TrafficSignalState state;
                if (!_signals.TryGetState(visual.NodeId, out state))
                {
                    continue;
                }

                ApplyPhaseVisual(ref visual, state.Phase);
            }
        }

        protected override void OnShutdown()
        {
            DestroyVisuals();
            DestroyMaterials();
            _isRunScene = false;
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted evt)
        {
            if (evt.To == SceneNames.RunScene)
            {
                return;
            }

            _isRunScene = false;
            DestroyVisuals();
        }

        private void OnSceneTransitionCompleted(SceneTransitionCompleted evt)
        {
            HandleSceneChanged(evt.SceneName, true);
        }

        private void OnTrafficSystemDefined(TrafficSystemDefined evt)
        {
            if (!_isRunScene)
            {
                return;
            }

            RebuildVisuals();
        }

        private void HandleSceneChanged(string sceneName, bool forceRebuild)
        {
            bool isRun = sceneName == SceneNames.RunScene;
            if (!isRun)
            {
                _isRunScene = false;
                DestroyVisuals();
                return;
            }

            bool enteredNow = !_isRunScene;
            _isRunScene = true;
            if (enteredNow || forceRebuild)
            {
                RebuildVisuals();
            }
        }

        private void RebuildVisuals()
        {
            DestroyVisuals();

            if (_network == null)
            {
                Services.TryGet(out _network);
            }

            if (_signals == null)
            {
                Services.TryGet(out _signals);
            }

            if (_network == null || _network.NodeCount <= 0)
            {
                return;
            }

            EnsureMaterials();

            GameObject root = new GameObject("TrafficSignalVisualRoot");
            _visualRoot = root.transform;

            for (int nodeId = 0; nodeId < _network.NodeCount; nodeId++)
            {
                TrafficNodeData node;
                if (!_network.TryGetNode(nodeId, out node) || !node.IsIntersection)
                {
                    continue;
                }

                SignalVisual visual = CreateSignalVisual(nodeId, node.Position, _visualRoot);
                _visuals.Add(visual);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[TrafficSignalVisualManager] Created temp traffic lights: " + _visuals.Count);
#endif
        }

        private SignalVisual CreateSignalVisual(int nodeId, Vector3 intersectionPosition, Transform parent)
        {
            GameObject signalRoot = new GameObject("Signal_" + nodeId.ToString());
            signalRoot.transform.SetParent(parent, false);
            signalRoot.transform.position = ResolveSignalRoadsidePosition(nodeId, intersectionPosition);

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(signalRoot.transform, false);
            pole.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            pole.transform.localScale = new Vector3(0.12f, 1.8f, 0.12f);
            DestroyColliderIfExists(pole);
            SetRendererMaterial(pole, _offMat);

            // Two heads: one for N/S traffic, one for E/W traffic.
            GameObject verticalHead = new GameObject("HeadVertical");
            verticalHead.transform.SetParent(signalRoot.transform, false);
            verticalHead.transform.localPosition = new Vector3(-0.28f, 3.0f, 0f);

            GameObject horizontalHead = new GameObject("HeadHorizontal");
            horizontalHead.transform.SetParent(signalRoot.transform, false);
            horizontalHead.transform.localPosition = new Vector3(0.28f, 3.0f, 0f);

            SignalVisual visual = new SignalVisual
            {
                NodeId = nodeId,
                VerticalRed = CreateLamp(verticalHead.transform, "Vertical_Red", new Vector3(0f, 0.22f, 0f)),
                VerticalYellow = CreateLamp(verticalHead.transform, "Vertical_Yellow", new Vector3(0f, 0f, 0f)),
                VerticalGreen = CreateLamp(verticalHead.transform, "Vertical_Green", new Vector3(0f, -0.22f, 0f)),
                HorizontalRed = CreateLamp(horizontalHead.transform, "Horizontal_Red", new Vector3(0f, 0.22f, 0f)),
                HorizontalYellow = CreateLamp(horizontalHead.transform, "Horizontal_Yellow", new Vector3(0f, 0f, 0f)),
                HorizontalGreen = CreateLamp(horizontalHead.transform, "Horizontal_Green", new Vector3(0f, -0.22f, 0f))
            };

            ApplyPhaseVisual(ref visual, TrafficSignalPhase.VerticalGreen);
            return visual;
        }

        private static Vector3 ResolveSignalRoadsidePosition(int nodeId, Vector3 intersectionPosition)
        {
            int corner = nodeId & 3;
            float signX = 1f;
            float signZ = 1f;
            if (corner == 1)
            {
                signX = -1f;
            }
            else if (corner == 2)
            {
                signX = -1f;
                signZ = -1f;
            }
            else if (corner == 3)
            {
                signZ = -1f;
            }

            return new Vector3(
                intersectionPosition.x + (signX * SignalCornerOffset),
                intersectionPosition.y + SignalBaseYOffset,
                intersectionPosition.z + (signZ * SignalCornerOffset));
        }

        private Renderer CreateLamp(Transform parent, string name, Vector3 localPos)
        {
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = name;
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = localPos;
            lamp.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
            DestroyColliderIfExists(lamp);

            Renderer renderer = lamp.GetComponent<Renderer>();
            renderer.sharedMaterial = _offMat;
            return renderer;
        }

        private void ApplyPhaseVisual(ref SignalVisual visual, TrafficSignalPhase phase)
        {
            bool verticalGreen = phase == TrafficSignalPhase.VerticalGreen;
            bool verticalYellow = phase == TrafficSignalPhase.VerticalYellow;
            bool horizontalGreen = phase == TrafficSignalPhase.HorizontalGreen;
            bool horizontalYellow = phase == TrafficSignalPhase.HorizontalYellow;

            bool verticalRed = !verticalGreen && !verticalYellow;
            bool horizontalRed = !horizontalGreen && !horizontalYellow;

            SetLampActive(visual.VerticalRed, verticalRed, _redMat);
            SetLampActive(visual.VerticalYellow, verticalYellow, _yellowMat);
            SetLampActive(visual.VerticalGreen, verticalGreen, _greenMat);

            SetLampActive(visual.HorizontalRed, horizontalRed, _redMat);
            SetLampActive(visual.HorizontalYellow, horizontalYellow, _yellowMat);
            SetLampActive(visual.HorizontalGreen, horizontalGreen, _greenMat);
        }

        private void SetLampActive(Renderer renderer, bool active, Material activeMat)
        {
            if (renderer == null)
            {
                return;
            }

            Material target = active ? activeMat : _offMat;
            if (renderer.sharedMaterial != target)
            {
                renderer.sharedMaterial = target;
            }
        }

        private void EnsureMaterials()
        {
            if (_offMat != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _offMat = CreateMaterial(shader, new Color(0.12f, 0.12f, 0.12f, 1f), 0f);
            _redMat = CreateMaterial(shader, new Color(0.90f, 0.10f, 0.10f, 1f), 0f);
            _yellowMat = CreateMaterial(shader, new Color(1.00f, 0.85f, 0.10f, 1f), 0f);
            _greenMat = CreateMaterial(shader, new Color(0.10f, 0.85f, 0.20f, 1f), 0f);
        }

        private Material CreateMaterial(Shader shader, Color color, float metallic)
        {
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.2f);
            }

            return mat;
        }

        private void DestroyVisuals()
        {
            _visuals.Clear();

            if (_visualRoot != null)
            {
                Object.Destroy(_visualRoot.gameObject);
                _visualRoot = null;
            }
        }

        private void DestroyMaterials()
        {
            if (_offMat != null)
            {
                Object.Destroy(_offMat);
                _offMat = null;
            }

            if (_redMat != null)
            {
                Object.Destroy(_redMat);
                _redMat = null;
            }

            if (_yellowMat != null)
            {
                Object.Destroy(_yellowMat);
                _yellowMat = null;
            }

            if (_greenMat != null)
            {
                Object.Destroy(_greenMat);
                _greenMat = null;
            }
        }

        private void DestroyColliderIfExists(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Object.Destroy(col);
            }
        }

        private void SetRendererMaterial(GameObject go, Material mat)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }
        }
    }
}
