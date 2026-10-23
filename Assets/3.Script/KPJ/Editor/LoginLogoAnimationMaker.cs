using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Lexio/Make Login Logo Animation: 로그인 화면 로고 애니메이션(KPJ용 복사본)을 만듦
// LSY 원본(LogoAnimation)에서 조각(카드·칩)마다 마지막에 놓이는 자리와 등장 순서만 읽고, 원본 파일은 바꾸지 않음
//  - 나올 때: 로고 뒤(가운데)에서 차례로 튀어나와 크게 부풀었다가 제자리에 안착
//  - 머무는 동안: 조각마다 살짝 흔들림
//  - 들어갈 때: 잠깐 부풀었다가 로고 뒤로 빨려 들어감 (다시 처음부터 반복)
//  - 완성 로고(가운데 큰 로고)는 움직이지 않음
// 만든 뒤에는 Animation 창에서 키를 직접 고쳐도 됨. 이 메뉴를 다시 실행하면 처음 값으로 다시 만듦
public class LoginLogoAnimationMaker
{
    private const string SourceClipPath = "Assets/5.Animation/LSY/LogoAnimation.anim";
    private const string OutputFolder = "Assets/5.Animation/KPJ";
    public const string ClipPath = OutputFolder + "/LoginLogo.anim";
    public const string ControllerPath = OutputFolder + "/LoginLogo.controller";

    // 시간(초)
    private const float StartDelay = 0.15f;
    private const float Stagger = 0.12f;
    private const float PopSeconds = 0.45f;
    private const float HoldUntil = 3.4f;
    private const float OutStagger = 0.05f;
    private const float OutSeconds = 0.3f;
    private const float LoopLength = 4.4f;
    // 나올 때 가장 크게 부푸는 배율, 제자리보다 더 멀리 튀어나가는 비율, 숨어 있을 때 가운데 쪽으로 들어가 있는 비율
    private const float Overshoot = 1.25f;
    private const float BurstDistance = 1.12f;
    private const float HiddenInside = 0.25f;
    // 나올 때·들어갈 때 기우는 각도, 머무는 동안 흔들리는 각도
    private const float TiltDegrees = 12f;
    private const float WobbleDegrees = 3f;

    private class Piece
    {
        public string path;
        public Vector3 position;
        public Vector3 euler;
        public Vector3 scale;
        public float firstShown;
    }

    [MenuItem("Lexio/Make Login Logo Animation")]
    public static void Make()
    {
        new LoginLogoAnimationMaker().Run();
    }

    // 로그인 로고 컨트롤러가 없을 때만 만듦 (GameSceneBuilder가 씀. 이미 있으면 Animation 창에서 고친 값을 그대로 둠)
    public static RuntimeAnimatorController GetOrMake()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null)
        {
            new LoginLogoAnimationMaker().Run();
            controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        }
        return controller;
    }

    private void Run()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);
        if (source != null)
        {
            List<Piece> pieces = ReadPieces(source);
            AnimationClip clip = GetOrCreateClip();
            clip.ClearCurves();
            for (int i = 0; i < pieces.Count; i++)
            {
                WritePiece(clip, pieces[i], i);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
            {
                AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[LoginLogoAnimationMaker] " + ClipPath + " 조각 " + pieces.Count + "개");
        }
        else
        {
            Debug.LogWarning("[LoginLogoAnimationMaker] " + SourceClipPath + " 없음");
        }
    }

    private AnimationClip GetOrCreateClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            if (AssetDatabase.IsValidFolder(OutputFolder) == false)
            {
                AssetDatabase.CreateFolder("Assets/5.Animation", "KPJ");
            }
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        return clip;
    }

    // 원본에서 조각마다 마지막 자세와 처음 보이기 시작한 시간(크기가 0보다 커진 때)을 읽음
    private List<Piece> ReadPieces(AnimationClip source)
    {
        Dictionary<string, Piece> byPath = new Dictionary<string, Piece>();
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
        float end = source.length;
        for (int i = 0; i < bindings.Length; i++)
        {
            EditorCurveBinding binding = bindings[i];
            if (binding.type == typeof(Transform) && binding.path.Length > 0)
            {
                if (byPath.ContainsKey(binding.path) == false)
                {
                    Piece created = new Piece();
                    created.path = binding.path;
                    created.scale = Vector3.one;
                    created.firstShown = end;
                    byPath[binding.path] = created;
                }
                Piece piece = byPath[binding.path];
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                float value = curve.Evaluate(end);
                SetAxis(piece, binding.propertyName, value);
                if (binding.propertyName == "m_LocalScale.x")
                {
                    piece.firstShown = GetFirstShownTime(curve, end);
                }
            }
        }

        List<Piece> pieces = new List<Piece>(byPath.Values);
        pieces.Sort((a, b) => a.firstShown.CompareTo(b.firstShown));
        return pieces;
    }

    private void SetAxis(Piece piece, string propertyName, float value)
    {
        int axis = propertyName[propertyName.Length - 1] - 'x';
        if (propertyName.StartsWith("m_LocalPosition."))
        {
            piece.position[axis] = value;
        }
        else if (propertyName.StartsWith("m_LocalScale."))
        {
            piece.scale[axis] = value;
        }
        else if (propertyName.StartsWith("localEulerAngles"))
        {
            piece.euler[axis] = value;
        }
    }

    private float GetFirstShownTime(AnimationCurve curve, float end)
    {
        float result = end;
        Keyframe[] keys = curve.keys;
        for (int i = 0; i < keys.Length && result == end; i++)
        {
            if (keys[i].value > 0.001f)
            {
                result = keys[i].time;
            }
        }
        return result;
    }

    private void WritePiece(AnimationClip clip, Piece piece, int index)
    {
        // 흔들리는 방향을 조각마다 번갈아 바꿈
        float side = 1f;
        if (index % 2 == 1)
        {
            side = -1f;
        }
        Vector3 hiddenPosition = new Vector3(piece.position.x * HiddenInside, piece.position.y * HiddenInside, piece.position.z);
        Vector3 burstPosition = new Vector3(piece.position.x * BurstDistance, piece.position.y * BurstDistance, piece.position.z);
        Vector3 settlePosition = new Vector3(piece.position.x * 0.98f, piece.position.y * 0.98f, piece.position.z);
        Vector3 floatPosition = piece.position + new Vector3(0f, 0.15f, 0f);
        Vector3 pullPosition = new Vector3(piece.position.x * 1.05f, piece.position.y * 1.05f, piece.position.z);
        float rest = piece.euler.z;

        float appear = StartDelay + index * Stagger;
        float settled = appear + PopSeconds;
        float leave = HoldUntil + index * OutStagger;
        float wobble = (settled + leave) * 0.5f;

        List<float> times = new List<float>();
        List<Vector3> positions = new List<Vector3>();
        List<float> angles = new List<float>();
        List<float> scales = new List<float>();
        AddKey(times, positions, angles, scales, 0f, hiddenPosition, rest - TiltDegrees * side, 0f);
        AddKey(times, positions, angles, scales, appear, hiddenPosition, rest - TiltDegrees * side, 0f);
        AddKey(times, positions, angles, scales, appear + PopSeconds * 0.55f, burstPosition, rest + TiltDegrees * 0.6f * side, Overshoot);
        AddKey(times, positions, angles, scales, appear + PopSeconds * 0.8f, settlePosition, rest - TiltDegrees * 0.2f * side, 0.92f);
        AddKey(times, positions, angles, scales, settled, piece.position, rest, 1f);
        AddKey(times, positions, angles, scales, wobble, floatPosition, rest + WobbleDegrees * side, 1f);
        AddKey(times, positions, angles, scales, leave, piece.position, rest, 1f);
        AddKey(times, positions, angles, scales, leave + OutSeconds * 0.35f, pullPosition, rest - TiltDegrees * 0.3f * side, 1.12f);
        AddKey(times, positions, angles, scales, leave + OutSeconds, hiddenPosition, rest + TiltDegrees * side, 0f);
        AddKey(times, positions, angles, scales, LoopLength, hiddenPosition, rest + TiltDegrees * side, 0f);

        string[] axes = { "x", "y", "z" };
        for (int axis = 0; axis < 3; axis++)
        {
            AnimationCurve position = new AnimationCurve();
            AnimationCurve scale = new AnimationCurve();
            AnimationCurve euler = new AnimationCurve();
            for (int k = 0; k < times.Count; k++)
            {
                position.AddKey(new Keyframe(times[k], positions[k][axis]));
                scale.AddKey(new Keyframe(times[k], piece.scale[axis] * scales[k]));
                float angle = piece.euler[axis];
                if (axis == 2)
                {
                    angle = angles[k];
                }
                euler.AddKey(new Keyframe(times[k], angle));
            }
            SetCurve(clip, piece.path, "m_LocalPosition." + axes[axis], position);
            SetCurve(clip, piece.path, "m_LocalScale." + axes[axis], scale);
            SetCurve(clip, piece.path, "localEulerAnglesRaw." + axes[axis], euler);
        }
    }

    private void AddKey(List<float> times, List<Vector3> positions, List<float> angles, List<float> scales, float time, Vector3 position, float angle, float scale)
    {
        times.Add(time);
        positions.Add(position);
        angles.Add(angle);
        scales.Add(scale);
    }

    private void SetCurve(AnimationClip clip, string path, string propertyName, AnimationCurve curve)
    {
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }
}
