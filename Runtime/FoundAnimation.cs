using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace FortnitePirateToken.Runtime;

/// <summary>
/// The "You found Pirate Shop Access" banner: a chroma-keyed image sequence (Assets/token/anim) drawn as a screen
/// overlay along the top of the screen, with its sound. Frames are decoded one at a time into a single texture.
/// </summary>
internal sealed class FoundAnimation : MonoBehaviour
{
    private const string FrameFolder = "Assets/token/anim";
    private const string SoundPath = "token/found.wav";

    // The frames are on the source video's 30 fps grid; some are missing because the animation holds still (or was
    // thinned to 24 fps). Each file is named f<frame>_<x>_<y>_<w>_<h>: where the frame's content sits in the original
    // 1920x1080 picture. Only that box is stored, at whatever resolution fits, so no frame is ever stretched.
    private const float FrameRate = 30f;
    private const float SourceWidth = 1920f;
    private const float SourceHeight = 1080f;
    private const float Duration = 10f;
    private const float FadeOut = 0.5f;
    private const float Volume = 0.9f;

    private readonly struct Frame
    {
        public readonly int Index;
        public readonly string Path;
        public readonly Vector2 AnchorMin;
        public readonly Vector2 AnchorMax;

        public Frame(int index, string path, float x, float y, float w, float h)
        {
            Index = index;
            Path = path;
            // Anchors are fractions of the whole picture, measured from the bottom left.
            AnchorMin = new Vector2(x / SourceWidth, 1f - (y + h) / SourceHeight);
            AnchorMax = new Vector2((x + w) / SourceWidth, 1f - y / SourceHeight);
        }
    }

    private static FoundAnimation? _instance;

    private RawImage _image = null!;
    private CanvasGroup _group = null!;
    private AudioSource _audio = null!;
    private Texture2D _texture = null!;
    private AudioClip? _clip;
    private RectTransform _imageRect = null!;
    private List<Frame>? _frames;
    private Coroutine? _playing;

    public static void Play()
    {
        try
        {
            if (_instance == null) Create();
            _instance!.Restart();
        }
        catch (System.Exception e)
        {
            FortnitePirateTokenPlugin.Logger.LogError($"Could not play the found animation: {e}");
        }
    }

    private static void Create()
    {
        var root = new GameObject("FortnitePirateToken Found Animation");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        _instance = root.AddComponent<FoundAnimation>();
        _instance._group = root.AddComponent<CanvasGroup>();
        _instance._group.blocksRaycasts = false;
        _instance._group.interactable = false;

        // A 16:9 box fitted into the screen and pinned to its top; frames are placed inside it by anchors.
        var boxObject = new GameObject("Picture");
        boxObject.transform.SetParent(root.transform, false);
        var box = boxObject.AddComponent<RectTransform>();
        box.anchorMin = Vector2.zero;
        box.anchorMax = Vector2.one;
        box.pivot = new Vector2(0.5f, 1f);
        box.anchoredPosition = Vector2.zero;
        var fitter = boxObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = SourceWidth / SourceHeight;

        var imageObject = new GameObject("Frame");
        imageObject.transform.SetParent(boxObject.transform, false);
        _instance._imageRect = imageObject.AddComponent<RectTransform>();
        _instance._imageRect.offsetMin = Vector2.zero;
        _instance._imageRect.offsetMax = Vector2.zero;

        _instance._texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "found animation frame",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        _instance._image = imageObject.AddComponent<RawImage>();
        _instance._image.texture = _instance._texture;
        _instance._image.raycastTarget = false;

        _instance._audio = root.AddComponent<AudioSource>();
        _instance._audio.playOnAwake = false;
        _instance._audio.spatialBlend = 0f; // 2D: heard by the local player only, at full volume
        _instance._audio.volume = Volume;
        _instance._audio.ignoreListenerPause = true;

        _instance._frames = ListFrames();
        _instance._group.alpha = 0f;
    }

    private static List<Frame> ListFrames()
    {
        var frames = new List<Frame>();
        foreach (var path in EmbeddedAssets.List(FrameFolder))
        {
            var parts = Path.GetFileNameWithoutExtension(path).Split('_'); // f0042_x_y_w_h
            if (parts.Length != 5) continue;

            var numbers = new int[5];
            var ok = true;
            for (var i = 0; i < 5; i++)
            {
                ok &= int.TryParse(i == 0 ? parts[i].TrimStart('f') : parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]);
            }

            if (ok) frames.Add(new Frame(numbers[0], path, numbers[1], numbers[2], numbers[3], numbers[4]));
        }

        frames.Sort((a, b) => a.Index.CompareTo(b.Index));
        return frames;
    }

    private void Restart()
    {
        if (_playing != null) StopCoroutine(_playing);
        _playing = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        _clip ??= EmbeddedAssets.LoadWavPcm16(SoundPath);
        _audio.clip = _clip;
        _audio.Play();

        var frames = _frames!;
        var shown = -1;
        var start = Time.unscaledTime;
        _group.alpha = 1f;

        while (true)
        {
            var t = Time.unscaledTime - start;
            if (t >= Duration + FadeOut) break;

            // The latest frame whose time has come.
            var wanted = -1;
            for (var i = 0; i < frames.Count && frames[i].Index / FrameRate <= t; i++) wanted = i;

            if (wanted >= 0 && wanted != shown)
            {
                shown = wanted;
                var frame = frames[wanted];
                ImageConversion.LoadImage(_texture, EmbeddedAssets.ReadBytes(frame.Path));
                _imageRect.anchorMin = frame.AnchorMin;
                _imageRect.anchorMax = frame.AnchorMax;
                _imageRect.offsetMin = Vector2.zero;
                _imageRect.offsetMax = Vector2.zero;
            }

            _group.alpha = t <= Duration ? 1f : 1f - (t - Duration) / FadeOut;
            yield return null;
        }

        _group.alpha = 0f;
        _audio.Stop();
        _playing = null;
    }
}
