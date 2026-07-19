using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class SetupAnimationsEditor {
    [MenuItem("Tools/Setup Player Animations")]
    public static void Run() {
        string folder = "Assets/Sprites/Player";
        string animFolder = "Assets/Animations";
        if (!AssetDatabase.IsValidFolder(animFolder)) AssetDatabase.CreateFolder("Assets", "Animations");

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        List<AnimationClip> clips = new List<AnimationClip>();

        foreach(string guid in guids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int frameWidth = 140;
            int frameHeight = 46;
            
            if (tex.width < frameWidth) continue;

            // 픽셀 아트 최적화 설정
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.filterMode = FilterMode.Point; // 도트 튀는 현상 방지
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None; // 중요: 2의 제곱수가 아닌 이미지 크기가 변형되어 깨지는 현상 방지
            ti.spritePixelsPerUnit = 32;

            int cols = tex.width / frameWidth;
            SpriteMetaData[] meta = new SpriteMetaData[cols];
            for(int i=0; i<cols; i++) {
                meta[i] = new SpriteMetaData {
                    name = tex.name + "_" + i,
                    rect = new Rect(i * frameWidth, 0, frameWidth, frameHeight),
                    alignment = 7, // 7 = BottomCenter
                    pivot = new Vector2(0.5f, 0f) // 피봇을 발 밑으로 설정
                };
            }
            ti.spritesheet = meta;
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            List<Sprite> sprites = new List<Sprite>();
            foreach(Object obj in assets) {
                if (obj is Sprite s) sprites.Add(s);
            }
            sprites.Sort((a, b) => {
                string[] pA = a.name.Split('_');
                string[] pB = b.name.Split('_');
                if (pA.Length > 1 && pB.Length > 1) {
                    return int.Parse(pA.Last()).CompareTo(int.Parse(pB.Last()));
                }
                return a.name.CompareTo(b.name);
            });

            AnimationClip clip = new AnimationClip();
            clip.frameRate = 12;
            EditorCurveBinding spriteBinding = new EditorCurveBinding {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[sprites.Count];
            for(int i=0; i<sprites.Count; i++) {
                keyFrames[i] = new ObjectReferenceKeyframe {
                    time = i / clip.frameRate,
                    value = sprites[i]
                };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyFrames);
            
            if (tex.name.Contains("Idle") || tex.name.Contains("Run") || tex.name.Contains("Wall Slide")) {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
            
            string clipPath = animFolder + "/" + tex.name + ".anim";
            AssetDatabase.CreateAsset(clip, clipPath);
            clips.Add(clip);
        }

        Debug.Log("스프라이트 슬라이싱 및 픽셀/피봇 설정 완료!");
    }
}
