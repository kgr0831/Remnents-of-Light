#if UNITY_EDITOR
using System.IO;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

// Source: gist.github.com/JINZO631/73ba7538b389df6fc912162d7b969ebd (Recorder scripting API
// shape verified against docs.unity3d.com/Packages/com.unity.recorder manual, checked 2026-07-21)
public static class TestRecorder
{
    private static RecorderController _controller;
    private static string _outputPath;

    public static void StartRecording(string fileName)
    {
        var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movieSettings.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
        movieSettings.AudioInputSettings.PreserveAudio = false;
        var encoderSettings = new CoreEncoderSettings();
        encoderSettings.Codec = CoreEncoderSettings.OutputCodec.MP4;
        movieSettings.EncoderSettings = encoderSettings;
        movieSettings.Enabled = true;

        string dir = Path.Combine(Application.dataPath, "..", "Recordings");
        Directory.CreateDirectory(dir);
        movieSettings.OutputFile = Path.Combine(dir, fileName);
        _outputPath = Path.GetFullPath(movieSettings.OutputFile + ".mp4");

        var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        controllerSettings.FrameRate = 30;
        controllerSettings.AddRecorderSettings(movieSettings);

        _controller = new RecorderController(controllerSettings);
        _controller.PrepareRecording();
        _controller.StartRecording();
    }

    public static string StopRecording()
    {
        _controller?.StopRecording();
        return _outputPath;
    }
}
#endif
