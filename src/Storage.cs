using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace WordAss
{
    public sealed class Sense
    {
        public string PartOfSpeech { get; set; }
        public string Definition { get; set; }
        public string Example { get; set; }
        public string ExampleTranslation { get; set; }
    }

    public sealed class Entry
    {
        public string Word { get; set; }
        public string Summary { get; set; }
        public Sense[] Senses { get; set; }
        public DateTimeOffset AddedAt { get; set; }

        private static readonly Regex WordPattern = new Regex(@"\A[A-Za-z]+(?:[-'’][A-Za-z]+)*\z");

        public static string SelectedWord(string text)
        {
            var candidate = text.Trim().Trim('"', '\'', '“', '”', '‘', '’', '(', ')', '[', ']',
                '{', '}', '<', '>', '.', ',', '!', '?', ':', ';', '。', '，', '！', '？', '：', '；').Trim();
            return candidate.Length <= 100 && WordPattern.IsMatch(candidate) ? candidate : null;
        }

        public string Key { get { return Word.Trim().Replace('’', '\'').ToLowerInvariant(); } }
    }

    internal sealed class LocalStore
    {
        private readonly string wordbookPath;
        private readonly string apiPath;
        private readonly string legacyKeyPath;

        public LocalStore(string directory)
        {
            Directory.CreateDirectory(directory);
            wordbookPath = Path.Combine(directory, "wordbook.xml");
            apiPath = Path.Combine(directory, "api-settings.dat");
            legacyKeyPath = Path.Combine(directory, "deepseek.key");
        }

        public ApiSettings ReadApiSettings()
        {
            string json = ReadProtectedText(apiPath);
            try
            {
                if (json == null)
                {
                    string legacyKey = ReadProtectedText(legacyKeyPath);
                    return legacyKey == null ? null : ApiSettings.Create(true, null, ApiSettings.DeepSeekModel, legacyKey);
                }

                var settings = new JavaScriptSerializer().Deserialize<ApiSettings>(json);
                if (settings == null) throw new InvalidDataException("无法读取 API 配置。");
                return ApiSettings.Create(settings.IsDeepSeek, settings.BaseUrl, settings.Model, settings.Key);
            }
            catch (ArgumentException ex) { throw new InvalidDataException("无法读取 API 配置。", ex); }
            catch (InvalidOperationException ex) { throw new InvalidDataException("无法读取 API 配置。", ex); }
        }

        private static string ReadProtectedText(string path)
        {
            byte[] encrypted;
            try { encrypted = File.ReadAllBytes(path); }
            catch (FileNotFoundException) { return null; }
            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(decrypted); }
            finally { Array.Clear(decrypted, 0, decrypted.Length); }
        }

        public void SaveApiSettings(ApiSettings settings)
        {
            byte[] plaintext = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(settings));
            try
            {
                byte[] encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
                ReplaceFile(apiPath, stream => stream.Write(encrypted, 0, encrypted.Length));
            }
            finally { Array.Clear(plaintext, 0, plaintext.Length); }
        }

        public List<Entry> ReadEntries()
        {
            FileStream stream;
            try { stream = File.OpenRead(wordbookPath); }
            catch (FileNotFoundException) { return new List<Entry>(); }
            using (stream)
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
            {
                var root = XDocument.Load(reader).Root;
                if (root == null || root.Name != "wordbook" || (string)root.Attribute("version") != "1")
                    throw new InvalidDataException("无法识别单词本的 XML 格式。");

                var entries = new List<Entry>();
                foreach (var node in root.Elements("entry"))
                {
                    string word = (string)node.Element("word");
                    string summary = (string)node.Element("summary");
                    DateTimeOffset added;
                    if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(summary) ||
                        !DateTimeOffset.TryParseExact((string)node.Attribute("addedAt"), "o", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out added))
                        throw new InvalidDataException("单词本中有格式不完整的词条。");

                    entries.Add(new Entry
                    {
                        Word = word,
                        Summary = summary,
                        AddedAt = added,
                        Senses = node.Elements("sense").Select(s => new Sense
                        {
                            PartOfSpeech = (string)s.Attribute("partOfSpeech"),
                            Definition = (string)s.Element("definition"),
                            Example = (string)s.Element("example"),
                            ExampleTranslation = (string)s.Element("exampleTranslation")
                        }).ToArray()
                    });
                }
                return entries;
            }
        }

        public bool Add(Entry entry)
        {
            var entries = ReadEntries();
            if (entries.Any(existing => existing.Key == entry.Key)) return false;
            entry.AddedAt = DateTimeOffset.UtcNow;
            entries.Insert(0, entry);
            WriteEntries(entries);
            return true;
        }

        public bool Remove(Entry entry)
        {
            var entries = ReadEntries();
            if (entries.RemoveAll(existing => existing.Key == entry.Key) == 0) return false;
            WriteEntries(entries);
            return true;
        }

        private void WriteEntries(List<Entry> entries)
        {
            var document = new XDocument(new XElement("wordbook", new XAttribute("version", 1),
                entries.Select(item => new XElement("entry",
                    new XAttribute("addedAt", item.AddedAt.ToString("o", CultureInfo.InvariantCulture)),
                    new XElement("word", item.Word), new XElement("summary", item.Summary),
                    item.Senses.Select(sense => new XElement("sense",
                        new XAttribute("partOfSpeech", sense.PartOfSpeech ?? ""),
                        new XElement("definition", sense.Definition), new XElement("example", sense.Example),
                        new XElement("exampleTranslation", sense.ExampleTranslation)))))));
            ReplaceFile(wordbookPath, stream => document.Save(stream));
        }

        private static void ReplaceFile(string path, Action<FileStream> write)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    write(stream);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
