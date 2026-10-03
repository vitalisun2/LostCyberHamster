using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Читает binary plist с сохранением ссылок NSKeyedArchiver.</summary>
    internal sealed class ProcreateBinaryPlistReader
    {
        private readonly byte[] data;
        private readonly int[] offsets;
        private readonly object[] objects;
        private readonly byte[] states;
        private readonly int referenceSize;
        private readonly int objectTableEnd;
        private readonly int rootIndex;

        /// <summary>Проверяет заголовок, трейлер и таблицу смещений plist.</summary>
        private ProcreateBinaryPlistReader(byte[] data)
        {
            // Проверяем служебные данные архива.
            if (data == null || data.Length < 40 || Encoding.ASCII.GetString(data, 0, 8) != "bplist00")
                throw new InvalidDataException("Document.archive не является binary plist.");
            this.data = data;
            int trailer = data.Length - 32;
            int offsetSize = data[trailer + 6];
            referenceSize = data[trailer + 7];
            int count = ToInt(ReadUnsigned(trailer + 8, 8));
            rootIndex = ToInt(ReadUnsigned(trailer + 16, 8));
            objectTableEnd = ToInt(ReadUnsigned(trailer + 24, 8));
            if (offsetSize < 1 || offsetSize > 8 || referenceSize < 1 || referenceSize > 8 ||
                count < 1 || count > 1000000 || rootIndex >= count || objectTableEnd < 8 ||
                objectTableEnd > trailer || (long)objectTableEnd + (long)count * offsetSize > trailer)
                throw new InvalidDataException("Повреждена таблица объектов Document.archive.");

            // Загружаем адреса объектов; содержимое читаем по требованию.
            offsets = new int[count];
            objects = new object[count];
            states = new byte[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = ToInt(ReadUnsigned(objectTableEnd + i * offsetSize, offsetSize));
                if (offsets[i] < 8 || offsets[i] >= objectTableEnd)
                    throw new InvalidDataException("Объект plist находится за границами таблицы.");
            }
        }

        /// <summary>Возвращает корневой объект проверенного plist.</summary>
        internal static object Read(byte[] bytes)
        {
            var reader = new ProcreateBinaryPlistReader(bytes);
            return reader.ReadObject(reader.rootIndex, 0);
        }

        /// <summary>Декодирует объект и проверяет циклы ссылок binary plist.</summary>
        private object ReadObject(int index, int depth)
        {
            // Проверяем ссылку и используем уже прочитанный объект.
            if (index < 0 || index >= offsets.Length || depth > 256)
                throw new InvalidDataException("Некорректная ссылка или вложенность plist.");
            if (states[index] == 2)
                return objects[index];
            if (states[index] == 1)
                throw new InvalidDataException("Циклическая ссылка в binary plist.");
            states[index] = 1;

            // Декодируем тип из маркера объекта.
            int position = offsets[index];
            byte marker = data[position++];
            int type = marker >> 4;
            int info = marker & 15;
            object value;
            switch (type)
            {
                case 0:
                    value = info == 0 ? null : info == 8 ? (object)false : info == 9 ? true :
                        throw new InvalidDataException("Неподдерживаемый простой тип plist.");
                    break;
                case 1:
                    if (info > 3)
                        throw new InvalidDataException("Неподдерживаемая длина числа plist.");
                    value = unchecked((long)ReadObjectUnsigned(position, 1 << info));
                    break;
                case 2:
                case 3:
                    int realSize = type == 3 ? 8 : 1 << info;
                    if ((type == 3 && info != 3) || (realSize != 4 && realSize != 8))
                        throw new InvalidDataException("Неподдерживаемый вещественный тип plist.");
                    RequireObjectBytes(position, realSize);
                    var realBytes = new byte[realSize];
                    Buffer.BlockCopy(data, position, realBytes, 0, realSize);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(realBytes);
                    value = realSize == 4 ? (double)BitConverter.ToSingle(realBytes, 0) :
                        BitConverter.ToDouble(realBytes, 0);
                    break;
                case 4:
                case 5:
                case 6:
                case 7:
                    int length = ReadLength(info, ref position);
                    int byteLength = checked(length * (type == 6 ? 2 : 1));
                    RequireObjectBytes(position, byteLength);
                    if (type == 4)
                    {
                        var bytes = new byte[byteLength];
                        Buffer.BlockCopy(data, position, bytes, 0, byteLength);
                        value = bytes;
                    }
                    else
                        value = (type == 6 ? Encoding.BigEndianUnicode : type == 7 ? Encoding.UTF8 :
                            Encoding.ASCII).GetString(data, position, byteLength);
                    break;
                case 8:
                    value = new ProcreateArchiveUid(ToInt(ReadObjectUnsigned(position, info + 1)));
                    break;
                case 10:
                    int arrayLength = ReadLength(info, ref position);
                    RequireObjectBytes(position, checked(arrayLength * referenceSize));
                    var array = new object[arrayLength];
                    for (int i = 0; i < arrayLength; i++)
                        array[i] = ReadObject(ToInt(ReadObjectUnsigned(position + i * referenceSize,
                            referenceSize)), depth + 1);
                    value = array;
                    break;
                case 13:
                    int dictionaryLength = ReadLength(info, ref position);
                    RequireObjectBytes(position, checked(dictionaryLength * referenceSize * 2));
                    var dictionary = new Dictionary<string, object>(dictionaryLength);
                    for (int i = 0; i < dictionaryLength; i++)
                    {
                        int keyIndex = ToInt(ReadObjectUnsigned(position + i * referenceSize, referenceSize));
                        int valueIndex = ToInt(ReadObjectUnsigned(position + (dictionaryLength + i) *
                            referenceSize, referenceSize));
                        if (!(ReadObject(keyIndex, depth + 1) is string key) || dictionary.ContainsKey(key))
                            throw new InvalidDataException("Некорректный ключ словаря plist.");
                        dictionary.Add(key, ReadObject(valueIndex, depth + 1));
                    }
                    value = dictionary;
                    break;
                default:
                    throw new InvalidDataException($"Неподдерживаемый тип plist: {type}.");
            }

            // Кэшируем завершённый объект.
            objects[index] = value;
            states[index] = 2;
            return value;
        }

        /// <summary>Читает длину коллекции или строки из маркера.</summary>
        private int ReadLength(int info, ref int position)
        {
            if (info < 15)
                return info;

            // Расширенная длина хранится в следующем целочисленном объекте.
            RequireObjectBytes(position, 1);
            int marker = data[position++];
            int exponent = marker & 15;
            if ((marker >> 4) != 1 || exponent > 3)
                throw new InvalidDataException("Некорректная длина объекта plist.");
            int size = 1 << exponent;
            int length = ToInt(ReadObjectUnsigned(position, size));
            position += size;
            return length;
        }

        /// <summary>Читает число из области объектов.</summary>
        private ulong ReadObjectUnsigned(int position, int size)
        {
            RequireObjectBytes(position, size);
            return ReadUnsigned(position, size);
        }

        /// <summary>Читает беззнаковое число big-endian с проверкой границ файла.</summary>
        private ulong ReadUnsigned(int position, int size)
        {
            if (size < 1 || size > 8 || position < 0 || position > data.Length - size)
                throw new InvalidDataException("Число plist находится за границами файла.");

            // Размеры и ссылки в plist всегда big-endian.
            ulong value = 0;
            for (int i = 0; i < size; i++)
                value = (value << 8) | data[position + i];
            return value;
        }

        /// <summary>Проверяет, что данные объекта не заходят в служебную таблицу.</summary>
        private void RequireObjectBytes(int position, int length)
        {
            if (position < 8 || length < 0 || position > objectTableEnd - length)
                throw new InvalidDataException("Данные объекта plist находятся за границами таблицы.");
        }

        /// <summary>Проверяет допустимый размер индекса или буфера.</summary>
        private static int ToInt(ulong value)
        {
            if (value > int.MaxValue)
                throw new InvalidDataException("Размер или индекс plist слишком велик.");
            return (int)value;
        }
    }
}
