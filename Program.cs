using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EncodingFixer
{
    class Program
    {
        public static Encoding GetEncoding(string filename)
        {
            // Read the BOM
            var bom = new byte[4];
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            return Encoding.ASCII;
        }


        static string accentChars = "áéíóúůýěžščřďťň";

        static byte[] GenerateByteSnippet(char letter, Encoding encoding)
        {
            return encoding.GetBytes(new char[] {letter});
        }

        static List<byte[]> GetByteSnippets(string accentChars, Encoding encoding)
        {
            return accentChars.Select(letter => GenerateByteSnippet(letter, encoding)).ToList();
        }


        public static int FirstIndexOf(byte[] source, byte[] pattern)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source.Skip(i).Take(pattern.Length).SequenceEqual(pattern))
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool HasAnyPattern(byte[] data, IList<byte[]> patterns)
        {
            for (int i = 0; i < data.Length; i++)
            {
                foreach (var pat in patterns)
                {
                    if (pat.Length + i >= data.Length) continue;
                    
                    var ismatch = true;
                    for (int j = 0; j < pat.Length; j++)
                    {
                        if (data[i+j] != pat[j])
                        {
                            ismatch = false;
                            break;
                        }
                    }
                    if (ismatch) return true;
                }
            }
            return false;
        }



        static void Main(string[] args)
        {
            var utf8snippets = GetByteSnippets(accentChars + accentChars.ToUpper(), Encoding.UTF8);
            var windows1250snippets =
                GetByteSnippets(accentChars + accentChars.ToUpper(), Encoding.GetEncoding("windows-1250"));

            var todel = new List<byte[]>();

            foreach (var utf in utf8snippets)
            foreach (var win in windows1250snippets)
            {
                if (FirstIndexOf(utf, win) >= 0 || FirstIndexOf(win, utf) >= 0)
                {
                   todel.Add(utf);
                   todel.Add(win);
                }
            }

            foreach (var td in todel)
            {
                utf8snippets.Remove(td);
                windows1250snippets.Remove(td);
            }


            var path = args.FirstOrDefault() ?? Directory.GetCurrentDirectory();
            foreach (var file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file)?.ToLower();
                if (ext == ".cs" || ext == ".txt" || ext == ".cshtml" || ext == ".xaml" || ext == ".xml" ||
                    ext == ".html" || ext==".srt")
                {
                    if (GetEncoding(file).Equals(Encoding.ASCII))
                    {
                        var data = File.ReadAllBytes(file);

                        var sourceEncoding = Encoding.GetEncoding("windows-1250");

                        var hasUtf8 =  HasAnyPattern(data, utf8snippets);
                        var hasWindows1250 =  HasAnyPattern(data, windows1250snippets);

                        if (hasUtf8 && hasWindows1250)
                        {
                            Console.WriteLine("{0} - cannot detect charset", file);
                            continue;
                        }

                        if (hasUtf8) sourceEncoding= Encoding.UTF8;


                        var converted = file + ".converted";

                        using (var sr = new StreamReader(file, sourceEncoding))
                        {
                            using (var stream = File.OpenWrite(converted))
                            using (var sw = new StreamWriter(stream, Encoding.UTF8))
                            {
                                sw.Write(sr.ReadToEnd());
                                sw.Close();
                            }
                        }
                        File.Move(file, file+".bak");
                        File.Move(converted, file);
                        File.Delete(converted);
                        File.Delete(file +".bak");
                    }
                    
                }
            }


        }
    }
}
