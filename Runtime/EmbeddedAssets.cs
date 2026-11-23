using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FortnitePirateToken.Runtime;

/// <summary>Reads files compiled into the plugin DLL (everything under the project's Assets folder).</summary>
internal static class EmbeddedAssets
{
    private static readonly Assembly Assembly = typeof(EmbeddedAssets).Assembly;
    private static readonly string[] Names = Assembly.GetManifestResourceNames();

    public static byte[] ReadBytes(string path)
    {
        using var stream = Open(path);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static string ReadText(string path)
    {
        using var stream = Open(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static Texture2D LoadTexture(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = Path.GetFileNameWithoutExtension(path) };
        if (!ImageConversion.LoadImage(tex, ReadBytes(path)))
        {
            throw new InvalidDataException($"Could not decode texture '{path}'.");
        }

        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    /// <summary>Embedded asset paths (relative to the Assets folder's parent, e.g. "Assets/token/anim/f0001.png") in a folder.</summary>
    public static string[] List(string folder)
    {
        var prefix = folder.Replace('\\', '/').TrimEnd('/') + "/";
        return Names.Select(n => n.Replace('\\', '/'))
            .Where(n => n.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Decodes a 16-bit PCM WAV file.</summary>
    public static AudioClip LoadWavPcm16(string path)
    {
        var bytes = ReadBytes(path);
        int channels = 0, sampleRate = 0, bitsPerSample = 0, audioFormat = 0, dataOffset = -1, dataLength = 0;

        using (var stream = new MemoryStream(bytes))
        using (var reader = new BinaryReader(stream))
        {
            if (reader.ReadUInt32() != 0x46464952) throw new InvalidDataException("Invalid WAV file."); // RIFF
            reader.ReadUInt32();
            if (reader.ReadUInt32() != 0x45564157) throw new InvalidDataException("Invalid WAV file."); // WAVE

            while (stream.Position + 8 <= stream.Length)
            {
                var chunk = new string(reader.ReadChars(4));
                var length = reader.ReadInt32();
                var next = stream.Position + length;

                if (chunk == "fmt ")
                {
                    audioFormat = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadUInt16();
                    bitsPerSample = reader.ReadUInt16();
                }
                else if (chunk == "data")
                {
                    dataOffset = (int)stream.Position;
                    dataLength = length;
                }

                stream.Position = next + (length & 1);
                if (stream.Position > stream.Length) break;
            }
        }

        if (audioFormat != 1 || bitsPerSample != 16)
            throw new InvalidDataException($"Expected 16-bit PCM WAV. Format: {audioFormat}, bit depth: {bitsPerSample}");
        if (dataOffset < 0 || channels <= 0 || sampleRate <= 0)
            throw new InvalidDataException("WAV file is missing audio data.");

        dataLength = Math.Min(dataLength, bytes.Length - dataOffset);
        var sampleCount = dataLength / 2;
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToInt16(bytes, dataOffset + i * 2) / 32768f;
        }

        var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), sampleCount / channels, channels, sampleRate, false);
        clip.SetData(samples, 0);
        clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return clip;
    }

    private static Stream Open(string path)
    {
        var wanted = path.Replace('\\', '/');
        var name = Names.FirstOrDefault(n => n.Replace('\\', '/').EndsWith(wanted, StringComparison.OrdinalIgnoreCase));
        if (name == null)
        {
            throw new FileNotFoundException($"Embedded asset '{path}' not found. Available: {string.Join(", ", Names)}");
        }

        return Assembly.GetManifestResourceStream(name)!;
    }
}
