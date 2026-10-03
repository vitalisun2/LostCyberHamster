using System;
using System.IO;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Декодирует Apple LZ4 с общей историей блоков одного тайла.</summary>
    internal static class ProcreateLz4Decoder
    {
        /// <summary>Читает bv41/bv4- блоки и проверяет размер готового тайла.</summary>
        internal static byte[] Decode(byte[] source, int expectedSize)
        {
            var output = new byte[expectedSize];
            int inputPosition = 0;
            int outputPosition = 0;

            // Все блоки тайла используют общую 64-КиБ историю LZ4.
            while (inputPosition <= source.Length - 4)
            {
                uint magic = ReadUInt32(source, ref inputPosition);
                if (magic == 0x24347662) // bv4$
                {
                    if (inputPosition != source.Length || outputPosition != expectedSize)
                        throw new InvalidDataException("Некорректная длина Apple LZ4 тайла.");
                    return output;
                }
                if (magic != 0x31347662 && magic != 0x2d347662)
                    throw new NotSupportedException("Неподдерживаемый формат тайла Procreate: требуется Apple LZ4.");
                int rawSize = CheckedSize(ReadUInt32(source, ref inputPosition));
                if (rawSize <= 0 || rawSize > 65536 || rawSize > expectedSize - outputPosition)
                    throw new InvalidDataException("Некорректный размер блока Apple LZ4.");

                // Неупакованные блоки сохраняют ту же историю следующих блоков.
                if (magic == 0x2d347662) // bv4-
                {
                    RequireBytes(source, inputPosition, rawSize);
                    Buffer.BlockCopy(source, inputPosition, output, outputPosition, rawSize);
                    inputPosition += rawSize;
                }
                else
                {
                    int compressedSize = CheckedSize(ReadUInt32(source, ref inputPosition));
                    RequireBytes(source, inputPosition, compressedSize);
                    DecodeBlock(source, inputPosition, compressedSize, output, outputPosition, rawSize);
                    inputPosition += compressedSize;
                }
                outputPosition += rawSize;
            }
            throw new InvalidDataException("Отсутствует завершающий маркер Apple LZ4 тайла.");
        }

        /// <summary>Восстанавливает LZ4-блок с проверкой литералов, смещений и повторов.</summary>
        private static void DecodeBlock(byte[] input, int start, int size, byte[] output,
            int outputStart, int outputSize)
        {
            int position = start;
            int end = start + size;
            int write = outputStart;
            int outputEnd = outputStart + outputSize;
            while (position < end)
            {
                // Копируем литералы текущей последовательности.
                byte token = input[position++];
                int literals = ReadLength(input, ref position, end, token >> 4);
                if (literals > end - position || literals > outputEnd - write)
                    throw new InvalidDataException("Литералы LZ4 выходят за границы блока.");
                Buffer.BlockCopy(input, position, output, write, literals);
                position += literals;
                write += literals;
                if (position == end)
                    break;

                // Повтор может ссылаться на предыдущий блок и перекрываться с собой.
                if (position > end - 2)
                    throw new InvalidDataException("Оборвана ссылка LZ4.");
                int offset = input[position] | input[position + 1] << 8;
                position += 2;
                int matchLength = checked(ReadLength(input, ref position, end, token & 15) + 4);
                if (offset == 0 || offset > write || matchLength > outputEnd - write)
                    throw new InvalidDataException("Некорректная ссылка или длина повтора LZ4.");
                for (int i = 0; i < matchLength; i++)
                {
                    output[write] = output[write - offset];
                    write++;
                }
            }
            if (write != outputEnd)
                throw new InvalidDataException("Распакованный размер LZ4 не совпадает с заголовком.");
        }

        /// <summary>Читает длину литералов или повтора с продолжением 255.</summary>
        private static int ReadLength(byte[] input, ref int position, int end, int length)
        {
            if (length != 15)
                return length;

            // Дополнительные байты продолжают длину до первого значения меньше 255.
            int part;
            do
            {
                if (position >= end)
                    throw new InvalidDataException("Оборвана длина последовательности LZ4.");
                part = input[position++];
                length = checked(length + part);
            } while (part == 255);
            return length;
        }

        /// <summary>Читает uint32 little-endian заголовка Apple LZ4.</summary>
        private static uint ReadUInt32(byte[] source, ref int position)
        {
            RequireBytes(source, position, 4);
            uint value = (uint)(source[position] | source[position + 1] << 8 |
                source[position + 2] << 16 | source[position + 3] << 24);
            position += 4;
            return value;
        }

        /// <summary>Проверяет доступность диапазона входных байтов.</summary>
        private static void RequireBytes(byte[] source, int position, int size)
        {
            if (size < 0 || position < 0 || position > source.Length - size)
                throw new InvalidDataException("Данные Apple LZ4 находятся за границами тайла.");
        }

        /// <summary>Проверяет, что размер помещается в индекс управляемого массива.</summary>
        private static int CheckedSize(uint size)
        {
            if (size > int.MaxValue)
                throw new InvalidDataException("Размер блока Apple LZ4 слишком велик.");
            return (int)size;
        }
    }
}
