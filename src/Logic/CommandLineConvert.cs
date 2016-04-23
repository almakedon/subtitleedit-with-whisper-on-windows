﻿using Nikse.SubtitleEdit.Core;
using Nikse.SubtitleEdit.Core.BluRaySup;
using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Core.VobSub;
using Nikse.SubtitleEdit.Forms;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Nikse.SubtitleEdit.Logic
{
    public static class CommandLineConvert
    {
        public static void Convert(string title, string[] args) // E.g.: /convert *.txt SubRip
        {
            const int ATTACH_PARENT_PROCESS = -1;
            if (!Configuration.IsRunningOnMac() && !Configuration.IsRunningOnLinux())
                NativeMethods.AttachConsole(ATTACH_PARENT_PROCESS);

            var currentFolder = Directory.GetCurrentDirectory();

            Console.WriteLine();
            Console.WriteLine(title + " - Batch converter");
            Console.WriteLine();

            if (args.Length < 4)
            {
                if (args.Length == 3 && (args[2].Equals("/list", StringComparison.OrdinalIgnoreCase) || args[2].Equals("-list", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("- Supported formats (input/output):");
                    foreach (SubtitleFormat format in SubtitleFormat.AllSubtitleFormats)
                    {
                        Console.WriteLine("    " + format.Name.Replace(" ", string.Empty));
                    }
                    Console.WriteLine();
                    Console.WriteLine("- Supported formats (input only):");
                    Console.WriteLine("    " + CapMakerPlus.NameOfFormat);
                    Console.WriteLine("    " + Captionate.NameOfFormat);
                    Console.WriteLine("    " + Cavena890.NameOfFormat);
                    Console.WriteLine("    " + CheetahCaption.NameOfFormat);
                    Console.WriteLine("    " + Chk.NameOfFormat);
                    Console.WriteLine("    Matroska (.mkv)");
                    Console.WriteLine("    Matroska subtitle (.mks)");
                    Console.WriteLine("    " + NciCaption.NameOfFormat);
                    Console.WriteLine("    " + AvidStl.NameOfFormat);
                    Console.WriteLine("    " + Pac.NameOfFormat);
                    Console.WriteLine("    " + Spt.NameOfFormat);
                    Console.WriteLine("    " + Ultech130.NameOfFormat);
                    Console.WriteLine("- For Blu-ray .sup output use: '" + BatchConvert.BluRaySubtitle.Replace(" ", string.Empty) + "'");
                    Console.WriteLine("- For VobSub .sub output use: '" + BatchConvert.VobSubSubtitle.Replace(" ", string.Empty) + "'");
                }
                else
                {
                    Console.WriteLine("- Usage: SubtitleEdit /convert <pattern> <name-of-format-without-spaces> [<optional-parameters>]");
                    Console.WriteLine();
                    Console.WriteLine("    pattern:");
                    Console.WriteLine("        one or more file name patterns separated by commas");
                    Console.WriteLine("        relative patterns are relative to /inputfolder if specified");
                    Console.WriteLine("    optional-parameters:");
                    Console.WriteLine("        /offset:hh:mm:ss:ms");
                    Console.WriteLine("        /fps:<frame rate>");
                    Console.WriteLine("        /targetfps:<frame rate>");
                    Console.WriteLine("        /encoding:<encoding name>");
                    Console.WriteLine("        /pac-codepage:<code page>");
                    Console.WriteLine("        /inputfolder:<folder name>");
                    Console.WriteLine("        /outputfolder:<folder name>");
                    Console.WriteLine("        /removetextforhi");
                    Console.WriteLine("        /fixcommonerrors");
                    Console.WriteLine("        /redocasing");
                    Console.WriteLine("        /multiplereplace");
                    Console.WriteLine();
                    Console.WriteLine("    example: SubtitleEdit /convert *.srt sami");
                    Console.WriteLine("    list available formats: SubtitleEdit /convert /list");
                }
                Console.WriteLine();

                if (!Configuration.IsRunningOnMac() && !Configuration.IsRunningOnLinux())
                {
                    Console.Write(currentFolder + ">");
                    NativeMethods.FreeConsole();
                }
                Environment.Exit(1);
            }

            int count = 0;
            int converted = 0;
            int errors = 0;
            try
            {
                var pattern = args[2].Trim();
                var targetFormat = args[3].Trim().Replace(" ", string.Empty);
                var offset = GetArgument(args, "offset:");

                double? targetFrameRate = null;
                {
                    var fps = GetArgument(args, "targetfps:");
                    if (fps.Length > 1)
                    {
                        fps = fps.Replace(',', '.').Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, ".");
                        double d;
                        if (double.TryParse(fps, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out d))
                        {
                            targetFrameRate = d;
                        }
                    }

                    fps = GetArgument(args, "fps:");
                    if (fps.Length > 1)
                    {
                        fps = fps.Replace(',', '.').Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, ".");
                        double d;
                        if (double.TryParse(fps, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out d))
                        {
                            Configuration.Settings.General.CurrentFrameRate = d;
                        }
                    }
                }

                var targetEncoding = Encoding.UTF8;
                try
                {
                    var encodingName = GetArgument(args, "encoding:");
                    if (encodingName.Length > 0)
                    {
                        targetEncoding = Encoding.GetEncoding(encodingName);
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Unable to set encoding (" + exception.Message + ") - using UTF-8");
                }

                var outputFolder = string.Empty;
                {
                    var folder = GetArgument(args, "outputfolder:");
                    if (folder.Length > 0 && Directory.Exists(folder))
                    {
                        outputFolder = folder;
                    }
                }

                var inputFolder = currentFolder;
                {
                    var folder = GetArgument(args, "inputFolder:");
                    if (folder.Length > 0 && Directory.Exists(folder))
                    {
                        inputFolder = folder;
                    }
                }

                int pacCodePage = -1;
                {
                    var pcp = GetArgument(args, "pac-codepage:");
                    if (pcp.Length > 0)
                    {
                        if (pcp.Equals("Latin", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageLatin;
                        else if (pcp.Equals("Greek", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageGreek;
                        else if (pcp.Equals("Czech", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageLatinCzech;
                        else if (pcp.Equals("Arabic", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageArabic;
                        else if (pcp.Equals("Hebrew", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageHebrew;
                        else if (pcp.Equals("Thai", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageThai;
                        else if (pcp.Equals("Cyrillic", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageCyrillic;
                        else if (pcp.Equals("CHT", StringComparison.OrdinalIgnoreCase) || pcp.Replace(" ", string.Empty).Equals("TraditionalChinese", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageChineseTraditional;
                        else if (pcp.Equals("CHS", StringComparison.OrdinalIgnoreCase) || pcp.Replace(" ", string.Empty).Equals("SimplifiedChinese", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageChineseSimplified;
                        else if (pcp.Equals("Korean", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageKorean;
                        else if (pcp.Equals("Japanese", StringComparison.OrdinalIgnoreCase))
                            pacCodePage = Pac.CodePageJapanese;
                        else if (!int.TryParse(pcp, out pacCodePage) || !Pac.IsValidCodePage(pacCodePage))
                        {
                            Console.WriteLine("Unknown pac code page '" + pcp + "' - using default code page");
                            pacCodePage = -1;
                        }
                    }
                }

                bool overwrite = GetArgument(args, "overwrite").Equals("overwrite");
                bool removeTextForHi = GetArgument(args, "removetextforhi").Equals("removetextforhi");
                bool fixCommonErrors = GetArgument(args, "fixcommonerrors").Equals("fixcommonerrors");
                bool redoCasing = GetArgument(args, "redocasing").Equals("redocasing");
                bool multipleReplace = GetArgument(args, "multiplereplace").Equals("multiplereplace");

                var patterns = Enumerable.Empty<string>();

                if (pattern.Contains(',') && !File.Exists(pattern))
                {
                    patterns = pattern.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(fn => fn.Trim()).Where(fn => fn.Length > 0);
                }
                else
                {
                    patterns = patterns.DefaultIfEmpty(pattern);
                }

                var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in patterns)
                {
                    var folderName = Path.GetDirectoryName(p);
                    var fileName = Path.GetFileName(p);
                    if (string.IsNullOrEmpty(folderName) || string.IsNullOrEmpty(fileName))
                    {
                        folderName = inputFolder;
                        fileName = p;
                    }
                    else if (!Path.IsPathRooted(folderName))
                    {
                        folderName = Path.Combine(inputFolder, folderName);
                    }
                    foreach (var fn in Directory.EnumerateFiles(folderName, fileName))
                    {
                        files.Add(fn); // silently ignore duplicates
                    }
                }

                var formats = SubtitleFormat.AllSubtitleFormats;
                foreach (var fileName in files)
                {
                    count++;

                    var fileInfo = new FileInfo(fileName);
                    if (fileInfo.Exists)
                    {
                        var sub = new Subtitle();
                        SubtitleFormat format = null;
                        bool done = false;

                        if (fileInfo.Extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) || fileInfo.Extension.Equals(".mks", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var matroska = new MatroskaFile(fileName))
                            {
                                if (matroska.IsValid)
                                {
                                    var tracks = matroska.GetTracks();
                                    if (tracks.Count > 0)
                                    {
                                        foreach (var track in tracks)
                                        {
                                            if (track.CodecId.Equals("S_VOBSUB", StringComparison.OrdinalIgnoreCase))
                                            {
                                                Console.WriteLine("{0}: {1} - Cannot convert from VobSub image based format!", fileName, targetFormat);
                                            }
                                            else if (track.CodecId.Equals("S_HDMV/PGS", StringComparison.OrdinalIgnoreCase))
                                            {
                                                Console.WriteLine("{0}: {1} - Cannot convert from Blu-ray image based format!", fileName, targetFormat);
                                            }
                                            else
                                            {
                                                var ss = matroska.GetSubtitle(track.TrackNumber, null);
                                                format = Utilities.LoadMatroskaTextSubtitle(track, matroska, ss, sub);
                                                string newFileName = fileName;
                                                if (tracks.Count > 1)
                                                    newFileName = fileName.Insert(fileName.Length - 4, "_" + track.TrackNumber + "_" + track.Language.Replace("?", string.Empty).Replace("!", string.Empty).Replace("*", string.Empty).Replace(",", string.Empty).Replace("/", string.Empty).Trim());

                                                if (format.GetType() == typeof(AdvancedSubStationAlpha) || format.GetType() == typeof(SubStationAlpha))
                                                {
                                                    if (!AdvancedSubStationAlpha.NameOfFormat.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase) &&
                                                        !SubStationAlpha.NameOfFormat.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        foreach (SubtitleFormat sf in formats)
                                                        {
                                                            if (sf.Name.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                format.RemoveNativeFormatting(sub, sf);
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }

                                                BatchConvertSave(targetFormat, offset, targetEncoding, outputFolder, count, ref converted, ref errors, formats, newFileName, sub, format, overwrite, pacCodePage, targetFrameRate, removeTextForHi, fixCommonErrors, redoCasing, multipleReplace);
                                                done = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (!done && FileUtil.IsBluRaySup(fileName))
                        {
                            Console.WriteLine("Found Blu-Ray subtitle format");
                            ConvertBluRaySubtitle(fileName, targetFormat, offset, targetEncoding, outputFolder, count, ref converted, ref errors, formats, overwrite, pacCodePage, targetFrameRate, removeTextForHi, fixCommonErrors, redoCasing, multipleReplace);
                            done = true;
                        }
                        if (!done && FileUtil.IsVobSub(fileName))
                        {
                            Console.WriteLine("Found VobSub subtitle format");
                            ConvertVobSubSubtitle(fileName, targetFormat, offset, targetEncoding, outputFolder, count, ref converted, ref errors, formats, overwrite, pacCodePage, targetFrameRate, removeTextForHi, fixCommonErrors, redoCasing, multipleReplace);
                            done = true;
                        }

                        if (!done && fileInfo.Length < 10 * 1024 * 1024) // max 10 mb
                        {
                            Encoding encoding;
                            format = sub.LoadSubtitle(fileName, out encoding, null, true);

                            if (format == null || format.GetType() == typeof(Ebu))
                            {
                                var ebu = new Ebu();
                                if (ebu.IsMine(null, fileName))
                                {
                                    ebu.LoadSubtitle(sub, null, fileName);
                                    format = ebu;
                                }
                            }
                            if (format == null)
                            {
                                var pac = new Pac();
                                if (pac.IsMine(null, fileName))
                                {
                                    pac.BatchMode = true;
                                    pac.CodePage = pacCodePage;
                                    pac.LoadSubtitle(sub, null, fileName);
                                    format = pac;
                                }
                            }
                            if (format == null)
                            {
                                var cavena890 = new Cavena890();
                                if (cavena890.IsMine(null, fileName))
                                {
                                    cavena890.LoadSubtitle(sub, null, fileName);
                                    format = cavena890;
                                }
                            }
                            if (format == null)
                            {
                                var spt = new Spt();
                                if (spt.IsMine(null, fileName))
                                {
                                    spt.LoadSubtitle(sub, null, fileName);
                                    format = spt;
                                }
                            }
                            if (format == null)
                            {
                                var cheetahCaption = new CheetahCaption();
                                if (cheetahCaption.IsMine(null, fileName))
                                {
                                    cheetahCaption.LoadSubtitle(sub, null, fileName);
                                    format = cheetahCaption;
                                }
                            }
                            if (format == null)
                            {
                                var chk = new Chk();
                                if (chk.IsMine(null, fileName))
                                {
                                    chk.LoadSubtitle(sub, null, fileName);
                                    format = chk;
                                }
                            }
                            if (format == null)
                            {
                                var ayato = new Ayato();
                                if (ayato.IsMine(null, fileName))
                                {
                                    ayato.LoadSubtitle(sub, null, fileName);
                                    format = ayato;
                                }
                            }
                            if (format == null)
                            {
                                var capMakerPlus = new CapMakerPlus();
                                if (capMakerPlus.IsMine(null, fileName))
                                {
                                    capMakerPlus.LoadSubtitle(sub, null, fileName);
                                    format = capMakerPlus;
                                }
                            }
                            if (format == null)
                            {
                                var captionate = new Captionate();
                                if (captionate.IsMine(null, fileName))
                                {
                                    captionate.LoadSubtitle(sub, null, fileName);
                                    format = captionate;
                                }
                            }
                            if (format == null)
                            {
                                var ultech130 = new Ultech130();
                                if (ultech130.IsMine(null, fileName))
                                {
                                    ultech130.LoadSubtitle(sub, null, fileName);
                                    format = ultech130;
                                }
                            }
                            if (format == null)
                            {
                                var nciCaption = new NciCaption();
                                if (nciCaption.IsMine(null, fileName))
                                {
                                    nciCaption.LoadSubtitle(sub, null, fileName);
                                    format = nciCaption;
                                }
                            }
                            if (format == null)
                            {
                                var tsb4 = new TSB4();
                                if (tsb4.IsMine(null, fileName))
                                {
                                    tsb4.LoadSubtitle(sub, null, fileName);
                                    format = tsb4;
                                }
                            }
                            if (format == null)
                            {
                                var avidStl = new AvidStl();
                                if (avidStl.IsMine(null, fileName))
                                {
                                    avidStl.LoadSubtitle(sub, null, fileName);
                                    format = avidStl;
                                }
                            }
                            if (format == null)
                            {
                                var elr = new ELRStudioClosedCaption();
                                if (elr.IsMine(null, fileName))
                                {
                                    elr.LoadSubtitle(sub, null, fileName);
                                    format = elr;
                                }
                            }
                        }

                        if (format == null)
                        {
                            if (fileInfo.Length < 1024 * 1024) // max 1 mb
                                Console.WriteLine("{0}: {1} - input file format unknown!", fileName, targetFormat);
                            else
                                Console.WriteLine("{0}: {1} - input file too large!", fileName, targetFormat);
                        }
                        else if (!done)
                        {
                            BatchConvertSave(targetFormat, offset, targetEncoding, outputFolder, count, ref converted, ref errors, formats, fileName, sub, format, overwrite, pacCodePage, targetFrameRate, removeTextForHi, fixCommonErrors, redoCasing, multipleReplace);
                        }
                    }
                    else
                    {
                        Console.WriteLine("{0}: {1} - file not found!", count, fileName);
                        errors++;
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine();
                Console.WriteLine("Oops - an error occured: " + exception.Message);
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("{0} file(s) converted", converted);
            Console.WriteLine();

            if (!Configuration.IsRunningOnMac() && !Configuration.IsRunningOnLinux())
            {
                Console.Write(currentFolder + ">");
                NativeMethods.FreeConsole();
            }

            if (count == converted && errors == 0)
                Environment.Exit(0);
            else
                Environment.Exit(1);
        }

        private static void ConvertBluRaySubtitle(string fileName, string targetFormat, string offset, Encoding targetEncoding, string outputFolder, int count, ref int converted, ref int errors, IEnumerable<SubtitleFormat> formats, bool overwrite, int pacCodePage, double? targetFrameRate, bool removeTextForHi, bool fixCommonErrors, bool redoCasing, bool multipleReplace)
        {
            var format = Utilities.GetSubtitleFormatByFriendlyName(targetFormat) ?? new SubRip();

            var log = new StringBuilder();
            Console.WriteLine("Loading subtitles from file \"{0}\"", fileName);
            var bluRaySubtitles = BluRaySupParser.ParseBluRaySup(fileName, log);
            Subtitle sub;
            using (var vobSubOcr = new VobSubOcr())
            {
                Console.WriteLine("Using OCR to extract subtitles");
                vobSubOcr.FileName = Path.GetFileName(fileName);
                vobSubOcr.InitializeBatch(bluRaySubtitles, Configuration.Settings.VobSubOcr, fileName);
                sub = vobSubOcr.SubtitleFromOcr;
                Console.WriteLine("Extracted subtitles from file \"{0}\"", fileName);
            }

            if (sub != null)
            {
                Console.WriteLine("Converted subtitle");
                BatchConvertSave(targetFormat, offset, targetEncoding, outputFolder, count, ref converted, ref errors, formats, fileName, sub, format, overwrite, pacCodePage, targetFrameRate, removeTextForHi, fixCommonErrors, redoCasing, multipleReplace);
            }
        }

        private static void ConvertVobSubSubtitle(string fileName, string targetFormat, string offset, Encoding targetEncoding, string outputFolder, int count, ref int converted, ref int errors, IEnumerable<SubtitleFormat> formats, bool overwrite, int pacCodePage, double? targetFrameRate, bool removeTextForHi, bool fixCommonErrors, bool redoCasing, bool multipleReplace)
        {
            var format = Utilities.GetSubtitleFormatByFriendlyName(targetFormat) ?? new SubRip();

            Console.WriteLine("Loading subtitles from file \"{0}\"", fileName);
            Subtitle sub;
            using (var vobSubOcr = new VobSubOcr())
            {
                Console.WriteLine("Using OCR to extract subtitles");
                vobSubOcr.InitializeBatch(fileName, Configuration.Settings.VobSubOcr);
                sub = vobSubOcr.SubtitleFromOcr;
                Console.WriteLine("Extracted subtitles from file \"{0}\"", fileName);
            }

            if (sub != null)
            {
                Console.WriteLine("Converted subtitle");
                BatchConvertSave(targetFormat, offset, targetEncoding, outputFolder, count, ref converted, ref errors, formats, fileName, sub, format, overwrite, pacCodePage, targetFrameRate, removeTextForHi, fixCommonErrors, redoCasing, multipleReplace);
            }
        }

        /// <summary>
        /// Gets an argument from the command line
        /// </summary>
        /// <param name="commandLineArguments">All arguments from the command line</param>
        /// <param name="requestedArgumentName">The name of the argument that is requested</param>
        private static string GetArgument(string[] commandLineArguments, string requestedArgumentName)
        {
            return GetArgument(commandLineArguments, requestedArgumentName, string.Empty);
        }

        /// <summary>
        /// Gets an argument from the command line
        /// </summary>
        /// <param name="commandLineArguments">All arguments from the command line</param>
        /// <param name="requestedArgumentName">The name of the argument that is requested</param>
        /// <param name="defaultValue">The default value, if the parameter could not be found</param>
        private static string GetArgument(string[] commandLineArguments, string requestedArgumentName, string defaultValue)
        {
            var prefixWithSlash = '/' + requestedArgumentName;
            var prefixWithHyphen = '-' + requestedArgumentName;

            for (int i = 4; i < commandLineArguments.Length; i++)
            {
                var argument = commandLineArguments[i].Trim();
                if (argument.StartsWith(prefixWithSlash, StringComparison.OrdinalIgnoreCase) || argument.StartsWith(prefixWithHyphen, StringComparison.OrdinalIgnoreCase))
                {
                    if (prefixWithSlash[prefixWithSlash.Length - 1] == ':')
                        return argument.Substring(prefixWithSlash.Length);
                    else
                        return argument.Substring(1).ToLower();
                }
            }
            return defaultValue;
        }

        internal static bool BatchConvertSave(string targetFormat, string offset, Encoding targetEncoding, string outputFolder, int count, ref int converted, ref int errors, IEnumerable<SubtitleFormat> formats, string fileName, Subtitle sub, SubtitleFormat format, bool overwrite, int pacCodePage, double? targetFrameRate, bool removeTextForHi, bool fixCommonErrors, bool redoCasing, bool multipleReplace)
        {
            double oldFrameRate = Configuration.Settings.General.CurrentFrameRate;
            try
            {
                // adjust offset
                if (offset.Length > 0)
                {
                    var parts = offset.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 5)
                    {
                        try
                        {
                            var ts = new TimeSpan(0, int.Parse(parts[1].TrimStart('-')), int.Parse(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]));
                            if (parts[1].StartsWith('-'))
                                sub.AddTimeToAllParagraphs(ts.Negate());
                            else
                                sub.AddTimeToAllParagraphs(ts);
                            parts = null;
                        }
                        catch
                        {
                        }
                    }
                    if (parts != null)
                    {
                        Console.Write(" (unable to read offset " + offset + ")");
                    }
                }

                // adjust frame rate
                if (targetFrameRate.HasValue)
                {
                    sub.ChangeFrameRate(Configuration.Settings.General.CurrentFrameRate, targetFrameRate.Value);
                    Configuration.Settings.General.CurrentFrameRate = targetFrameRate.Value;
                }

                if (removeTextForHi)
                {
                    var hiSettings = new Core.Forms.RemoveTextForHISettings();
                    var hiLib = new Core.Forms.RemoveTextForHI(hiSettings);
                    foreach (var p in sub.Paragraphs)
                    {
                        p.Text = hiLib.RemoveTextFromHearImpaired(p.Text);
                    }
                }
                if (fixCommonErrors)
                {
                    using (var fce = new FixCommonErrors { BatchMode = true })
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            fce.RunBatch(sub, format, targetEncoding, Configuration.Settings.Tools.BatchConvertLanguage);
                            sub = fce.FixedSubtitle;
                        }
                    }
                }
                if (redoCasing)
                {
                    using (var changeCasing = new ChangeCasing())
                    {
                        changeCasing.FixCasing(sub, LanguageAutoDetect.AutoDetectGoogleLanguage(sub));
                    }
                    using (var changeCasingNames = new ChangeCasingNames())
                    {
                        changeCasingNames.Initialize(sub);
                        changeCasingNames.FixCasing();
                    }
                }
                if (multipleReplace)
                {
                    using (var mr = new MultipleReplace())
                    {
                        mr.RunFromBatch(sub);
                        sub = mr.FixedSubtitle;
                        sub.RemoveParagraphsByIndices(mr.DeleteIndices);
                    }
                }

                bool targetFormatFound = false;
                string outputFileName;
                foreach (SubtitleFormat sf in formats)
                {
                    if (sf.IsTextBased && sf.Name.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFormatFound = true;
                        sf.BatchMode = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, sf.Extension, outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        if (sf.IsFrameBased && !sub.WasLoadedWithFrameNumbers)
                            sub.CalculateFrameNumbersFromTimeCodesNoCheck(Configuration.Settings.General.CurrentFrameRate);
                        else if (sf.IsTimeBased && sub.WasLoadedWithFrameNumbers)
                            sub.CalculateTimeCodesFromFrameNumbers(Configuration.Settings.General.CurrentFrameRate);

                        if ((sf.GetType() == typeof(WebVTT) || sf.GetType() == typeof(WebVTTFileWithLineNumber)))
                        {
                            targetEncoding = Encoding.UTF8;
                        }

                        try
                        {
                            if (sf.GetType() == typeof(ItunesTimedText) || sf.GetType() == typeof(ScenaristClosedCaptions) || sf.GetType() == typeof(ScenaristClosedCaptionsDropFrame))
                            {
                                var outputEnc = new UTF8Encoding(false); // create encoding with no BOM
                                using (var file = new StreamWriter(outputFileName, false, outputEnc)) // open file with encoding
                                {
                                    file.Write(sub.ToText(sf));
                                } // save and close it
                            }
                            else if (targetEncoding == Encoding.UTF8 && (format.GetType() == typeof(TmpegEncAW5) || format.GetType() == typeof(TmpegEncXml)))
                            {
                                var outputEnc = new UTF8Encoding(false); // create encoding with no BOM
                                using (var file = new StreamWriter(outputFileName, false, outputEnc)) // open file with encoding
                                {
                                    file.Write(sub.ToText(sf));
                                } // save and close it
                            }
                            else
                            {
                                File.WriteAllText(outputFileName, sub.ToText(sf), targetEncoding);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            errors++;
                            return false;
                        }

                        if (format.GetType() == typeof(Sami) || format.GetType() == typeof(SamiModern))
                        {
                            var sami = (Sami)format;
                            foreach (string className in Sami.GetStylesFromHeader(sub.Header))
                            {
                                var newSub = new Subtitle();
                                foreach (Paragraph p in sub.Paragraphs)
                                {
                                    if (p.Extra != null && p.Extra.Trim().Equals(className.Trim(), StringComparison.OrdinalIgnoreCase))
                                        newSub.Paragraphs.Add(p);
                                }
                                if (newSub.Paragraphs.Count > 0 && newSub.Paragraphs.Count < sub.Paragraphs.Count)
                                {
                                    string s = fileName;
                                    if (s.LastIndexOf('.') > 0)
                                        s = s.Insert(s.LastIndexOf('.'), "_" + className);
                                    else
                                        s += "_" + className + format.Extension;
                                    outputFileName = FormatOutputFileNameForBatchConvert(s, sf.Extension, outputFolder, overwrite);
                                    File.WriteAllText(outputFileName, newSub.ToText(sf), targetEncoding);
                                }
                            }
                        }
                        Console.WriteLine(" done.");
                        break;
                    }
                }
                if (!targetFormatFound)
                {
                    var ebu = new Ebu();
                    if (ebu.Name.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFormatFound = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, ebu.Extension, outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        Ebu.Save(outputFileName, sub, true);
                        Console.WriteLine(" done.");
                    }
                }
                if (!targetFormatFound)
                {
                    var pac = new Pac();
                    if (pac.Name.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase) || targetFormat.Equals(".pac", StringComparison.OrdinalIgnoreCase) || targetFormat.Equals("pac", StringComparison.OrdinalIgnoreCase))
                    {
                        pac.BatchMode = true;
                        pac.CodePage = pacCodePage;
                        targetFormatFound = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, pac.Extension, outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        pac.Save(outputFileName, sub);
                        Console.WriteLine(" done.");
                    }
                }
                if (!targetFormatFound)
                {
                    var cavena890 = new Cavena890();
                    if (cavena890.Name.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFormatFound = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, cavena890.Extension, outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        cavena890.Save(outputFileName, sub);
                        Console.WriteLine(" done.");
                    }
                }
                if (!targetFormatFound)
                {
                    var cheetahCaption = new CheetahCaption();
                    if (cheetahCaption.Name.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFormatFound = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, cheetahCaption.Extension, outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        CheetahCaption.Save(outputFileName, sub);
                        Console.WriteLine(" done.");
                    }
                }
                if (!targetFormatFound)
                {
                    var ayato = new Ayato();
                    if (ayato.Name.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFormatFound = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, ayato.Extension, outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        ayato.Save(outputFileName, null, sub);
                        Console.WriteLine(" done.");
                    }
                }
                if (!targetFormatFound)
                {
                    var capMakerPlus = new CapMakerPlus();
                    if (capMakerPlus.Name.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFormatFound = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, capMakerPlus.Extension, outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        CapMakerPlus.Save(outputFileName, sub);
                        Console.WriteLine(" done.");
                    }
                }
                if (!targetFormatFound)
                {
                    if (Configuration.Settings.Language.BatchConvert.PlainText.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFormatFound = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, ".txt", outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        File.WriteAllText(outputFileName, ExportText.GeneratePlainText(sub, false, false, false, false, false, false, string.Empty, true, false, true, true, false), targetEncoding);
                        Console.WriteLine(" done.");
                    }
                }
                if (!targetFormatFound)
                {
                    if (BatchConvert.BluRaySubtitle.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFormatFound = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, ".sup", outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        using (var form = new ExportPngXml())
                        {
                            form.Initialize(sub, format, "BLURAYSUP", fileName, null, null);
                            int width = 1920;
                            int height = 1080;
                            var parts = Configuration.Settings.Tools.ExportBluRayVideoResolution.Split('x');
                            if (parts.Length == 2 && Utilities.IsInteger(parts[0]) && Utilities.IsInteger(parts[1]))
                            {
                                width = int.Parse(parts[0]);
                                height = int.Parse(parts[1]);
                            }

                            using (var binarySubtitleFile = new FileStream(outputFileName, FileMode.Create))
                            {
                                for (int index = 0; index < sub.Paragraphs.Count; index++)
                                {
                                    var mp = form.MakeMakeBitmapParameter(index, width, height);
                                    mp.LineJoin = Configuration.Settings.Tools.ExportPenLineJoin;
                                    mp.Bitmap = ExportPngXml.GenerateImageFromTextWithStyle(mp);
                                    ExportPngXml.MakeBluRaySupImage(mp);
                                    binarySubtitleFile.Write(mp.Buffer, 0, mp.Buffer.Length);
                                }
                            }
                        }
                        Console.WriteLine(" done.");
                    }
                    else if (BatchConvert.VobSubSubtitle.Replace(" ", string.Empty).Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFormatFound = true;
                        outputFileName = FormatOutputFileNameForBatchConvert(fileName, ".sub", outputFolder, overwrite);
                        Console.Write("{0}: {1} -> {2}...", count, Path.GetFileName(fileName), outputFileName);
                        using (var form = new ExportPngXml())
                        {
                            form.Initialize(sub, format, "VOBSUB", fileName, null, null);
                            int width = 720;
                            int height = 576;
                            var parts = Configuration.Settings.Tools.ExportVobSubVideoResolution.Split('x');
                            if (parts.Length == 2 && Utilities.IsInteger(parts[0]) && Utilities.IsInteger(parts[1]))
                            {
                                width = int.Parse(parts[0]);
                                height = int.Parse(parts[1]);
                            }

                            var cfg = Configuration.Settings.Tools;
                            var language = DvdSubtitleLanguage.GetLanguageOrNull(LanguageAutoDetect.AutoDetectGoogleLanguage(sub)) ?? DvdSubtitleLanguage.English;
                            using (var vobSubWriter = new VobSubWriter(outputFileName, width, height, cfg.ExportBottomMargin, cfg.ExportBottomMargin, 32, cfg.ExportFontColor, cfg.ExportBorderColor, !cfg.ExportVobAntiAliasingWithTransparency, language))
                            {
                                for (int index = 0; index < sub.Paragraphs.Count; index++)
                                {
                                    var mp = form.MakeMakeBitmapParameter(index, width, height);
                                    mp.LineJoin = Configuration.Settings.Tools.ExportPenLineJoin;
                                    mp.Bitmap = ExportPngXml.GenerateImageFromTextWithStyle(mp);
                                    vobSubWriter.WriteParagraph(mp.P, mp.Bitmap, mp.Alignment);
                                }
                                vobSubWriter.WriteIdxFile();
                            }
                        }
                        Console.WriteLine(" done.");
                    }
                }
                if (!targetFormatFound)
                {
                    Console.WriteLine("{0}: {1} - target format '{2}' not found!", count, fileName, targetFormat);
                    errors++;
                    return false;
                }
                converted++;
                return true;
            }
            finally
            {
                Configuration.Settings.General.CurrentFrameRate = oldFrameRate;
            }
        }

        private static string FormatOutputFileNameForBatchConvert(string fileName, string extension, string outputFolder, bool overwrite)
        {
            string outputFileName = Path.ChangeExtension(fileName, extension);
            if (!string.IsNullOrEmpty(outputFolder))
                outputFileName = Path.Combine(outputFolder, Path.GetFileName(outputFileName));
            if (!overwrite && File.Exists(outputFileName))
                outputFileName = Path.ChangeExtension(outputFileName, Guid.NewGuid() + extension);
            return outputFileName;
        }

    }
}
