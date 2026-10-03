using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Читает дерево групп и слоёв Procreate без изменения исходника.</summary>
    public static class ProcreateDocumentReader
    {
        /// <summary>Открывает ZIP, проверяет формат документа и читает дерево слоёв.</summary>
        public static ProcreateDocument Read(string path)
        {
            // Читаем только метаданные; пиксели загружаются после выбора ролей.
            string fullPath = Path.GetFullPath(path);
            var file = new FileInfo(fullPath);
            long fileLength = file.Length;
            long lastWriteTicks = file.LastWriteTimeUtc.Ticks;
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry = zip.GetEntry("Document.archive") ??
                throw new InvalidDataException("В Procreate отсутствует Document.archive.");
            if (entry.Length < 40 || entry.Length > 16 * 1024 * 1024)
                throw new InvalidDataException("Некорректный размер Document.archive.");
            using var archiveStream = entry.Open();
            using var buffer = new MemoryStream((int)entry.Length);
            archiveStream.CopyTo(buffer);
            var archive = RequireDictionary(ProcreateBinaryPlistReader.Read(buffer.ToArray()));
            if (!archive.TryGetValue("$objects", out object archivedObjects) ||
                !(archivedObjects is object[] objects) || objects.Length == 0)
                throw new InvalidDataException("В Document.archive отсутствует таблица NSKeyedArchiver.");
            var top = RequireDictionary(RequireValue(archive, "$top"));
            var root = RequireDictionary(Resolve(RequireValue(top, "root"), objects));

            // Проверяем холст и цветовое пространство до сборки дерева.
            ValidateDocument(root, objects);
            string size = RequireString(Resolve(RequireValue(root, "size"), objects));
            var dimensions = size.Trim('{', '}', ' ').Split(',');
            if (dimensions.Length != 2 ||
                !int.TryParse(dimensions[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) ||
                !int.TryParse(dimensions[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int height) ||
                width < 1 || height < 1 || width > 16384 || height > 16384 ||
                (long)width * height > 64000000)
                throw new InvalidDataException($"Неподдерживаемый размер холста Procreate: {size}.");
            int tileSize = ReadInt(root, "tileSize", 0);
            if (tileSize < 1 || tileSize > 1024)
                throw new InvalidDataException($"Неподдерживаемый размер тайла Procreate: {tileSize}.");

            // Сохраняем исходную вложенность и состояние файла.
            var roots = ReadChildren(RequireValue(root, "unwrappedLayers"), objects,
                new HashSet<int>(), false, 1, null, width, height);
            file.Refresh();
            if (file.Length != fileLength || file.LastWriteTimeUtc.Ticks != lastWriteTicks)
                throw new IOException("Procreate изменился во время чтения. Откройте файл заново.");
            return new ProcreateDocument(fullPath, width, height, tileSize,
                roots.AsReadOnly(), fileLength, lastWriteTicks);
        }

        /// <summary>Проверяет поддерживаемый формат холста и sRGB-профиль.</summary>
        private static void ValidateDocument(Dictionary<string, object> root, object[] objects)
        {
            // Холст текущих исходников имеет обычную ориентацию и RGBA-тайлы версии 2.
            if (ReadInt(root, "version", 0) != 2 || ReadInt(root, "orientation", 0) != 1 ||
                ReadBool(root, "flippedHorizontally") || ReadBool(root, "flippedVertically"))
                throw new NotSupportedException("Поддерживаются Procreate version 2, orientation 1 и холст без отражения.");

            // Пиксели экспортируются в Unity как sRGB без неявного преобразования ICC.
            var profile = RequireDictionary(Resolve(RequireValue(root, "colorProfile"), objects));
            string profileName = RequireString(Resolve(RequireValue(profile, "SiColorProfileArchiveICCNameKey"), objects));
            if (!string.Equals(profileName, "sRGB IEC61966-2.1", StringComparison.Ordinal))
                throw new NotSupportedException($"Профиль Procreate '{profileName}' требует преобразования в sRGB.");
        }

        /// <summary>Разворачивает NSArray группы в исходном порядке.</summary>
        private static List<ProcreateNode> ReadChildren(object reference, object[] objects,
            HashSet<int> ancestors, bool parentHidden, double parentOpacity, string parentError,
            int width, int height)
        {
            var array = RequireDictionary(Resolve(reference, objects));
            if (!(RequireValue(array, "NS.objects") is object[] children))
                throw new InvalidDataException("Некорректный список слоёв Procreate.");

            // Ссылки дерева отличаются от плоского root.layers: сохраняем группы.
            var result = new List<ProcreateNode>(children.Length);
            foreach (object child in children)
                result.Add(ReadNode(child, objects, ancestors, parentHidden, parentOpacity,
                    parentError, width, height));
            return result;
        }

        /// <summary>Читает узел дерева и условия, влияющие на экспорт его пикселей.</summary>
        private static ProcreateNode ReadNode(object reference, object[] objects,
            HashSet<int> ancestors, bool parentHidden, double parentOpacity, string parentError,
            int width, int height)
        {
            // Проверяем рекурсию дерева, сохраняя UID группы как стабильный идентификатор.
            if (!(reference is ProcreateArchiveUid uid) || ancestors.Count > 128 || !ancestors.Add(uid.Index))
                throw new InvalidDataException("Некорректное или циклическое дерево слоёв Procreate.");
            var layer = RequireDictionary(Resolve(reference, objects));
            string name = RequireString(Resolve(RequireValue(layer, "name"), objects));
            bool isGroup = layer.ContainsKey("children");
            bool hidden = parentHidden || ReadBool(layer, isGroup ? "isHidden" : "hidden");
            double opacity = ReadDouble(layer, "opacity", 1) * parentOpacity;
            if (double.IsNaN(opacity) || opacity < 0 || opacity > 1)
                throw new InvalidDataException($"Некорректная прозрачность слоя '{name}'.");

            // Неподдерживаемые композиционные эффекты показываем в дереве, но блокируем экспорт.
            string error = parentError;
            if (ReadBool(layer, isGroup ? "isClipped" : "clipped"))
                error = "обтравочная маска";
            if (ReadInt(layer, "blend", 0) != 0 || ReadInt(layer, "extendedBlend", 0) != 0 ||
                ReadInt(layer, "extendedBlend2", 0) != 0)
                error = "режим наложения отличается от Normal";
            if (layer.TryGetValue("mask", out object mask) && Resolve(mask, objects) != null)
                error = "маска слоя";

            // Группы служат навигацией; растровые слои ссылаются на отдельные папки тайлов.
            List<ProcreateNode> children;
            string id;
            if (isGroup)
            {
                id = "group-" + uid.Index.ToString(CultureInfo.InvariantCulture);
                children = ReadChildren(layer["children"], objects, ancestors, hidden, opacity,
                    error, width, height);
            }
            else
            {
                id = RequireString(Resolve(RequireValue(layer, "UUID"), objects));
                if (!Guid.TryParse(id, out _))
                    throw new InvalidDataException($"Некорректный UUID слоя '{name}'.");
                children = new List<ProcreateNode>();
                if (ReadInt(layer, "type", -1) != 0)
                    error = "неподдерживаемый тип слоя";
                if (ReadInt(layer, "sizeWidth", 0) != width || ReadInt(layer, "sizeHeight", 0) != height)
                    error = "размер слоя отличается от холста";
                if (layer.TryGetValue("transform", out object transform) &&
                    !IsIdentityTransform(Resolve(transform, objects)))
                    error = "неподдерживаемое преобразование слоя";
            }
            ancestors.Remove(uid.Index);
            return new ProcreateNode(id, name, isGroup, hidden, children.AsReadOnly(), opacity, error);
        }

        /// <summary>Проверяет единичную матрицу слоя, записанную как 16 little-endian double.</summary>
        private static bool IsIdentityTransform(object value)
        {
            if (!(value is byte[] bytes) || bytes.Length != 128)
                return false;

            // Не интерпретируем сохранённые матрицы как готовые растровые координаты.
            for (int i = 0; i < 16; i++)
            {
                byte[] number = new byte[8];
                Buffer.BlockCopy(bytes, i * 8, number, 0, 8);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(number);
                if (BitConverter.ToDouble(number, 0) != (i % 5 == 0 ? 1d : 0d))
                    return false;
            }
            return true;
        }

        /// <summary>Разрешает UID NSKeyedArchiver, включая его объект null.</summary>
        private static object Resolve(object value, object[] objects)
        {
            if (!(value is ProcreateArchiveUid uid))
                return value;

            // UID 0 представляет отсутствие ссылки.
            if (uid.Index < 0 || uid.Index >= objects.Length)
                throw new InvalidDataException("Ссылка NSKeyedArchiver находится за границами таблицы.");
            return uid.Index == 0 ? null : objects[uid.Index];
        }

        /// <summary>Возвращает обязательное поле метаданных.</summary>
        private static object RequireValue(Dictionary<string, object> dictionary, string key)
        {
            if (!dictionary.TryGetValue(key, out object value))
                throw new InvalidDataException($"В Procreate отсутствует поле '{key}'.");
            return value;
        }

        /// <summary>Проверяет тип словаря метаданных.</summary>
        private static Dictionary<string, object> RequireDictionary(object value)
        {
            return value as Dictionary<string, object> ??
                throw new InvalidDataException("Некорректный словарь метаданных Procreate.");
        }

        /// <summary>Проверяет строковое поле метаданных.</summary>
        private static string RequireString(object value)
        {
            return value as string ?? throw new InvalidDataException("Некорректная строка метаданных Procreate.");
        }

        /// <summary>Читает целочисленное поле метаданных.</summary>
        private static int ReadInt(Dictionary<string, object> dictionary, string key, int defaultValue)
        {
            return dictionary.TryGetValue(key, out object value) ?
                Convert.ToInt32(value, CultureInfo.InvariantCulture) : defaultValue;
        }

        /// <summary>Читает прозрачность слоя или группы.</summary>
        private static double ReadDouble(Dictionary<string, object> dictionary, string key, double defaultValue)
        {
            return dictionary.TryGetValue(key, out object value) ?
                Convert.ToDouble(value, CultureInfo.InvariantCulture) : defaultValue;
        }

        /// <summary>Читает булево поле метаданных.</summary>
        private static bool ReadBool(Dictionary<string, object> dictionary, string key)
        {
            return dictionary.TryGetValue(key, out object value) && value is bool flag && flag;
        }
    }
}
