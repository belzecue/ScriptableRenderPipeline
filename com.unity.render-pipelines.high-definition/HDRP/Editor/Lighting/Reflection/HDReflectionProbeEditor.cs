using System.Collections.Generic;
using UnityEditor.Experimental.Rendering.HDPipeline;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.HDPipeline;
using UnityEngine.Rendering;

namespace UnityEditor.Experimental.Rendering
{
    [CustomEditorForRenderPipeline(typeof(ReflectionProbe), typeof(HDRenderPipelineAsset))]
    [CanEditMultipleObjects]
    partial class HDReflectionProbeEditor : Editor
    {
        [MenuItem("CONTEXT/ReflectionProbe/Remove HD Reflection Probe", false, 0)]
        static void RemoveLight(MenuCommand menuCommand)
        {
            GameObject go = ((ReflectionProbe)menuCommand.context).gameObject;

            Assert.IsNotNull(go);

            Undo.SetCurrentGroupName("Remove HD Reflection Probe");
            Undo.DestroyObjectImmediate(go.GetComponent<ReflectionProbe>());
            Undo.DestroyObjectImmediate(go.GetComponent<HDAdditionalReflectionData>());
        }

        static Dictionary<ReflectionProbe, HDReflectionProbeEditor> s_ReflectionProbeEditors = new Dictionary<ReflectionProbe, HDReflectionProbeEditor>();

        static HDReflectionProbeEditor GetEditorFor(ReflectionProbe p)
        {
            HDReflectionProbeEditor e;
            if (s_ReflectionProbeEditors.TryGetValue(p, out e)
                && e != null
                && !e.Equals(null)
                && ArrayUtility.IndexOf(e.targets, p) != -1)
                return e;

            return null;
        }

        SerializedHDReflectionProbe m_SerializedHdReflectionProbe;
        SerializedObject m_AdditionalDataSerializedObject;
        HDReflectionProbeUI m_UIState = new HDReflectionProbeUI();
        InfluenceVolumeUI m_InfluenceVolumeUI = new InfluenceVolumeUI();

        int m_PositionHash = 0;

        public bool sceneViewEditing
        {
            get { return IsReflectionProbeEditMode(EditMode.editMode) && EditMode.IsOwner(this); }
        }

        void OnEnable()
        {
            var additionalData = CoreEditorUtils.GetAdditionalData<HDAdditionalReflectionData>(targets);
            m_AdditionalDataSerializedObject = new SerializedObject(additionalData);
            m_SerializedHdReflectionProbe = new SerializedHDReflectionProbe(serializedObject, m_AdditionalDataSerializedObject);
            m_UIState.owner = this;
            m_UIState.Reset(
                m_SerializedHdReflectionProbe,
                Repaint);

            foreach (var t in targets)
            {
                var p = (ReflectionProbe)t;
                s_ReflectionProbeEditors[p] = this;
            }

            InitializeAllTargetProbes();
        }


        public override void OnInspectorGUI()
        {
            //InspectColorsGUI();

            var s = m_UIState;
            var p = m_SerializedHdReflectionProbe;

            s.Update();
            p.Update();

            HDReflectionProbeUI.Inspector.Draw(s, p, this);

            PerformOperations(s, p, this);

            p.Apply();

            HideAdditionalComponents(false);

            HDReflectionProbeUI.DoShortcutKey(p, this);
        }

        public static bool IsReflectionProbeEditMode(EditMode.SceneViewEditMode editMode)
        {
            return editMode == EditMode.SceneViewEditMode.ReflectionProbeBox || editMode == EditMode.SceneViewEditMode.Collider || editMode == EditMode.SceneViewEditMode.GridBox ||
                editMode == EditMode.SceneViewEditMode.ReflectionProbeOrigin;
        }

        static void PerformOperations(HDReflectionProbeUI s, SerializedHDReflectionProbe p, HDReflectionProbeEditor o)
        {
        }

        void HideAdditionalComponents(bool visible)
        {
            var adds = CoreEditorUtils.GetAdditionalData<HDAdditionalReflectionData>(targets);
            var flags = visible ? HideFlags.None : HideFlags.HideInInspector;
            for (var i = 0; i < targets.Length; ++i)
            {
                var addData = adds[i];
                addData.hideFlags = flags;
            }
        }

        void BakeRealtimeProbeIfPositionChanged(HDReflectionProbeUI s, SerializedHDReflectionProbe sp, Editor o)
        {
            if (Application.isPlaying
                || ((ReflectionProbeMode)sp.mode.intValue) != ReflectionProbeMode.Realtime)
            {
                m_PositionHash = 0;
                return;
            }

            var hash = 0;
            for (var i = 0; i < sp.so.targetObjects.Length; i++)
            {
                var p = (ReflectionProbe)sp.so.targetObjects[i];
                var tr = p.GetComponent<Transform>();
                hash ^= tr.position.GetHashCode();
            }

            if (hash != m_PositionHash)
            {
                m_PositionHash = hash;
                for (var i = 0; i < sp.so.targetObjects.Length; i++)
                {
                    var p = (ReflectionProbe)sp.so.targetObjects[i];
                    p.RenderProbe();
                }
            }
        }

        static void ApplyConstraintsOnTargets(HDReflectionProbeUI s, SerializedHDReflectionProbe sp, Editor o)
        {
            switch ((Shape)sp.influenceVolume.shape.enumValueIndex)
            {
                case Shape.Box:
                {
                    var maxBlendDistance = sp.influenceVolume.boxSize.vector3Value;
                    sp.targetData.influenceVolume.boxBlendDistancePositive = Vector3.Min(sp.targetData.influenceVolume.boxBlendDistancePositive, maxBlendDistance);
                    sp.targetData.influenceVolume.boxBlendDistanceNegative = Vector3.Min(sp.targetData.influenceVolume.boxBlendDistanceNegative, maxBlendDistance);
                    sp.targetData.influenceVolume.boxBlendNormalDistancePositive = Vector3.Min(sp.targetData.influenceVolume.boxBlendNormalDistancePositive, maxBlendDistance);
                    sp.targetData.influenceVolume.boxBlendNormalDistanceNegative = Vector3.Min(sp.targetData.influenceVolume.boxBlendNormalDistanceNegative, maxBlendDistance);
                    break;
                }
                case Shape.Sphere:
                {
                    var maxBlendDistance = Vector3.one * sp.influenceVolume.sphereRadius.floatValue;
                    sp.targetData.influenceVolume.boxBlendDistancePositive = Vector3.Min(sp.targetData.influenceVolume.boxBlendDistancePositive, maxBlendDistance);
                    sp.targetData.influenceVolume.boxBlendDistanceNegative = Vector3.Min(sp.targetData.influenceVolume.boxBlendDistanceNegative, maxBlendDistance);
                    sp.targetData.influenceVolume.boxBlendNormalDistancePositive = Vector3.Min(sp.targetData.influenceVolume.boxBlendNormalDistancePositive, maxBlendDistance);
                    sp.targetData.influenceVolume.boxBlendNormalDistanceNegative = Vector3.Min(sp.targetData.influenceVolume.boxBlendNormalDistanceNegative, maxBlendDistance);
                    break;
                }
            }
        }
    }
}
