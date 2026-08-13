#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assets.EditorTools.AnimatedPng
{
    /// <summary>
    /// Декодирует кадры APNG через штатный PNG-декодер Unity и применяет правила композиции APNG.
    /// </summary>
    internal static class AnimatedPngDecoder
    {
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static readonly uint[] CrcTable = CreateCrcTable();

        /// <summary>
        /// Проверяет наличие APNG control chunk без декодирования изображения.
        /// </summary>
        public static bool IsAnimated(byte[] pngBytes)
        {
            if (!HasPngSignature(pngBytes))
                return false;

            var offset = PngSignature.Length;
            while (offset + 12 <= pngBytes.Length)
            {
                var length = ReadInt32BigEndian(pngBytes, offset);
                if (length < 0 || offset + 12L + length > pngBytes.Length)
                    return false;

                var type = Encoding.ASCII.GetString(pngBytes, offset + 4, 4);
                if (type == "acTL")
                    return true;
                if (type == "IDAT" || type == "IEND")
                    return false;

                offset += 12 + length;
            }

            return false;
        }

        /// <summary>
        /// Возвращает полностью скомпонованные кадры APNG в порядке воспроизведения.
        /// </summary>
        public static DecodedAnimation Decode(byte[] pngBytes)
        {
            var animation = Parse(pngBytes);
            var canvas = new Color32[animation.Width * animation.Height];
            var decodedFrames = new List<Color32[]>(animation.Frames.Count);

            // Декодируем sub-frame и накладываем его на текущее состояние холста.
            foreach (var frame in animation.Frames)
            {
                var previousCanvas = frame.DisposeOperation == DisposeOperation.Previous
                    ? (Color32[])canvas.Clone()
                    : null;
                var framePixels = DecodeFrameTexture(animation, frame);

                CompositeFrame(canvas, animation.Width, animation.Height, frame, framePixels);
                decodedFrames.Add((Color32[])canvas.Clone());

                // Готовим холст к следующему кадру по dispose operation текущего кадра.
                if (frame.DisposeOperation == DisposeOperation.Background)
                    ClearFrameArea(canvas, animation.Width, animation.Height, frame);
                else if (frame.DisposeOperation == DisposeOperation.Previous)
                    canvas = previousCanvas;
            }

            return new DecodedAnimation(animation.Width, animation.Height, decodedFrames);
        }

        private static ParsedAnimation Parse(byte[] pngBytes)
        {
            if (!HasPngSignature(pngBytes))
                throw new InvalidDataException("Файл не является PNG.");

            var animation = new ParsedAnimation();
            var offset = PngSignature.Length;
            var imageDataStarted = false;
            FrameData currentFrame = null;

            // Читаем структуру PNG и собираем IDAT-данные каждого APNG-кадра.
            while (offset + 12 <= pngBytes.Length)
            {
                var length = ReadInt32BigEndian(pngBytes, offset);
                if (length < 0 || offset + 12L + length > pngBytes.Length)
                    throw new InvalidDataException("Повреждённая длина PNG chunk.");

                var type = Encoding.ASCII.GetString(pngBytes, offset + 4, 4);
                var data = CopyBytes(pngBytes, offset + 8, length);
                offset += 12 + length;

                switch (type)
                {
                    case "IHDR":
                        ParseHeader(animation, data);
                        break;
                    case "acTL":
                        animation.DeclaredFrameCount = ReadInt32BigEndian(data, 0);
                        break;
                    case "fcTL":
                        currentFrame = ParseFrameControl(animation, data);
                        animation.Frames.Add(currentFrame);
                        break;
                    case "IDAT":
                        imageDataStarted = true;
                        if (currentFrame != null)
                            currentFrame.ImageDataChunks.Add(data);
                        break;
                    case "fdAT":
                        imageDataStarted = true;
                        if (currentFrame == null || data.Length < 4)
                            throw new InvalidDataException("APNG fdAT найден без fcTL.");
                        currentFrame.ImageDataChunks.Add(CopyBytes(data, 4, data.Length - 4));
                        break;
                    case "IEND":
                        ValidateAnimation(animation);
                        return animation;
                    default:
                        if (!imageDataStarted && type != "acTL")
                            animation.HeaderChunks.Add(new PngChunk(type, data));
                        break;
                }
            }

            throw new InvalidDataException("PNG не содержит IEND.");
        }

        private static void ParseHeader(ParsedAnimation animation, byte[] data)
        {
            if (data.Length != 13)
                throw new InvalidDataException("Некорректный IHDR.");

            animation.HeaderData = data;
            animation.Width = ReadInt32BigEndian(data, 0);
            animation.Height = ReadInt32BigEndian(data, 4);
        }

        private static FrameData ParseFrameControl(ParsedAnimation animation, byte[] data)
        {
            if (data.Length != 26)
                throw new InvalidDataException("Некорректный APNG fcTL.");

            var frame = new FrameData
            {
                Width = ReadInt32BigEndian(data, 4),
                Height = ReadInt32BigEndian(data, 8),
                X = ReadInt32BigEndian(data, 12),
                Y = ReadInt32BigEndian(data, 16),
                DisposeOperation = (DisposeOperation)data[24],
                BlendOperation = (BlendOperation)data[25]
            };

            if (frame.Width <= 0 || frame.Height <= 0 || frame.X < 0 || frame.Y < 0 ||
                frame.X + (long)frame.Width > animation.Width || frame.Y + (long)frame.Height > animation.Height)
            {
                throw new InvalidDataException("APNG-кадр выходит за границы холста.");
            }

            if (data[24] > (byte)DisposeOperation.Previous || data[25] > (byte)BlendOperation.Over)
                throw new InvalidDataException("APNG содержит неизвестную операцию композиции.");

            return frame;
        }

        private static void ValidateAnimation(ParsedAnimation animation)
        {
            if (animation.HeaderData == null || animation.Width <= 0 || animation.Height <= 0)
                throw new InvalidDataException("PNG не содержит корректный IHDR.");
            if (animation.DeclaredFrameCount < 2 || animation.Frames.Count != animation.DeclaredFrameCount)
                throw new InvalidDataException("Файл не содержит корректную APNG-анимацию.");

            foreach (var frame in animation.Frames)
            {
                if (frame.ImageDataChunks.Count == 0)
                    throw new InvalidDataException("APNG-кадр не содержит данных изображения.");
            }
        }

        private static Color32[] DecodeFrameTexture(ParsedAnimation animation, FrameData frame)
        {
            var framePng = BuildFramePng(animation, frame);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                if (!ImageConversion.LoadImage(texture, framePng, false) ||
                    texture.width != frame.Width || texture.height != frame.Height)
                {
                    throw new InvalidDataException("Unity не смогла декодировать APNG-кадр.");
                }

                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static byte[] BuildFramePng(ParsedAnimation animation, FrameData frame)
        {
            using var stream = new MemoryStream();
            stream.Write(PngSignature, 0, PngSignature.Length);

            var header = (byte[])animation.HeaderData.Clone();
            WriteInt32BigEndian(header, 0, frame.Width);
            WriteInt32BigEndian(header, 4, frame.Height);
            WriteChunk(stream, "IHDR", header);

            foreach (var chunk in animation.HeaderChunks)
                WriteChunk(stream, chunk.Type, chunk.Data);
            foreach (var imageData in frame.ImageDataChunks)
                WriteChunk(stream, "IDAT", imageData);

            WriteChunk(stream, "IEND", Array.Empty<byte>());
            return stream.ToArray();
        }

        private static void CompositeFrame(Color32[] canvas, int canvasWidth, int canvasHeight, FrameData frame, Color32[] pixels)
        {
            var destinationBottom = canvasHeight - frame.Y - frame.Height;

            // APNG Y отсчитывается сверху, Unity pixel array — снизу.
            for (var y = 0; y < frame.Height; y++)
            {
                for (var x = 0; x < frame.Width; x++)
                {
                    var source = pixels[y * frame.Width + x];
                    var destinationIndex = (destinationBottom + y) * canvasWidth + frame.X + x;
                    canvas[destinationIndex] = frame.BlendOperation == BlendOperation.Source
                        ? source
                        : AlphaBlend(source, canvas[destinationIndex]);
                }
            }
        }

        private static Color32 AlphaBlend(Color32 source, Color32 destination)
        {
            var sourceAlpha = source.a / 255f;
            var destinationAlpha = destination.a / 255f;
            var outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
            if (outputAlpha <= 0f)
                return new Color32(0, 0, 0, 0);

            return new Color32(
                ToByte((source.r * sourceAlpha + destination.r * destinationAlpha * (1f - sourceAlpha)) / outputAlpha),
                ToByte((source.g * sourceAlpha + destination.g * destinationAlpha * (1f - sourceAlpha)) / outputAlpha),
                ToByte((source.b * sourceAlpha + destination.b * destinationAlpha * (1f - sourceAlpha)) / outputAlpha),
                ToByte(outputAlpha * 255f));
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
        }

        private static void ClearFrameArea(Color32[] canvas, int canvasWidth, int canvasHeight, FrameData frame)
        {
            var destinationBottom = canvasHeight - frame.Y - frame.Height;
            for (var y = 0; y < frame.Height; y++)
                Array.Clear(canvas, (destinationBottom + y) * canvasWidth + frame.X, frame.Width);
        }

        private static bool HasPngSignature(byte[] bytes)
        {
            if (bytes == null || bytes.Length < PngSignature.Length)
                return false;

            for (var i = 0; i < PngSignature.Length; i++)
            {
                if (bytes[i] != PngSignature[i])
                    return false;
            }

            return true;
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length)
                throw new InvalidDataException("PNG chunk обрезан.");

            return (bytes[offset] << 24) |
                   (bytes[offset + 1] << 16) |
                   (bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }

        private static void WriteInt32BigEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value >> 24);
            bytes[offset + 1] = (byte)(value >> 16);
            bytes[offset + 2] = (byte)(value >> 8);
            bytes[offset + 3] = (byte)value;
        }

        private static byte[] CopyBytes(byte[] bytes, int offset, int length)
        {
            var result = new byte[length];
            Buffer.BlockCopy(bytes, offset, result, 0, length);
            return result;
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            WriteUInt32BigEndian(stream, (uint)data.Length);
            stream.Write(typeBytes, 0, typeBytes.Length);
            stream.Write(data, 0, data.Length);

            var crc = uint.MaxValue;
            crc = UpdateCrc(crc, typeBytes);
            crc = UpdateCrc(crc, data);
            WriteUInt32BigEndian(stream, crc ^ uint.MaxValue);
        }

        private static void WriteUInt32BigEndian(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static uint UpdateCrc(uint crc, byte[] bytes)
        {
            foreach (var value in bytes)
                crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
            return crc;
        }

        private static uint[] CreateCrcTable()
        {
            var table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                var value = index;
                for (var bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xedb88320U ^ (value >> 1) : value >> 1;
                table[index] = value;
            }

            return table;
        }

        internal sealed class DecodedAnimation
        {
            public DecodedAnimation(int width, int height, IReadOnlyList<Color32[]> frames)
            {
                Width = width;
                Height = height;
                Frames = frames;
            }

            public int Width { get; }
            public int Height { get; }
            public IReadOnlyList<Color32[]> Frames { get; }
        }

        private sealed class ParsedAnimation
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int DeclaredFrameCount { get; set; }
            public byte[] HeaderData { get; set; }
            public List<PngChunk> HeaderChunks { get; } = new();
            public List<FrameData> Frames { get; } = new();
        }

        private sealed class FrameData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public DisposeOperation DisposeOperation { get; set; }
            public BlendOperation BlendOperation { get; set; }
            public List<byte[]> ImageDataChunks { get; } = new();
        }

        private readonly struct PngChunk
        {
            public PngChunk(string type, byte[] data)
            {
                Type = type;
                Data = data;
            }

            public string Type { get; }
            public byte[] Data { get; }
        }

        private enum DisposeOperation : byte
        {
            None = 0,
            Background = 1,
            Previous = 2
        }

        private enum BlendOperation : byte
        {
            Source = 0,
            Over = 1
        }
    }
}
#endif
