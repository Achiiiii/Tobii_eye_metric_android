using System;
using System.IO;
using System.Threading;
using UnityEngine;

// Diagnostic: saves a few of the exact grey frames handed to the Tobii processor, to check focus,
// exposure and face size. Frames go to <persistentDataPath>/frames as PGM (pull with adb). The main
// thread requests a frame with a tag; GazeFrameWorker writes the next frame it processes.
// These are pictures of the user's face: diagnostic builds only, never uploaded.
public static class FrameCapture
{
    private const int MaxFrames = 20;

    private static string s_folder;
    private static string s_pendingTag;
    private static int s_saved;

    // Main thread, once: Application.persistentDataPath may not be read from other threads.
    public static void Init()
    {
        s_folder = Path.Combine(Application.persistentDataPath, "frames");
        try
        {
            if (Directory.Exists(s_folder))
                foreach (var old in Directory.GetFiles(s_folder, "*.pgm"))
                    File.Delete(old);
            Directory.CreateDirectory(s_folder);
            Debug.Log("[FRAME] saving frames to " + s_folder);
        }
        catch (Exception e)
        {
            Debug.LogError("[FRAME] " + e.Message);
            s_folder = null;
        }
    }

    public static void Request(string tag)
    {
        if (s_folder != null && s_saved < MaxFrames)
            Interlocked.Exchange(ref s_pendingTag, tag);
    }

    // Frame worker thread.
    public static void TryWrite(sbyte[] gray8, int width, int height)
    {
        string tag = Interlocked.Exchange(ref s_pendingTag, null);
        if (tag == null || s_folder == null || s_saved >= MaxFrames)
            return;
        try
        {
            s_saved++;
            string name = $"frame_{s_saved:00}_{tag}_{width}x{height}.pgm";
            var pixels = new byte[width * height];
            Buffer.BlockCopy(gray8, 0, pixels, 0, pixels.Length);
            using (var file = new FileStream(Path.Combine(s_folder, name), FileMode.Create))
            {
                var header = System.Text.Encoding.ASCII.GetBytes($"P5\n{width} {height}\n255\n");
                file.Write(header, 0, header.Length);
                file.Write(pixels, 0, pixels.Length);
            }
            long sum = 0;
            for (int i = 0; i < pixels.Length; i += 16)
                sum += pixels[i];
            Debug.Log($"[FRAME] saved {name}, mean brightness {sum * 16.0 / pixels.Length:0}");
        }
        catch (Exception e)
        {
            Debug.LogError("[FRAME] " + e.Message);
        }
    }
}
