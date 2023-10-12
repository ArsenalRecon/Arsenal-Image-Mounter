//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using LTRData.Extensions.Buffers;
using LTRData.Extensions.Native;
using LTRData.Extensions.Split;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.IO.ConsoleIO;

public static class ConsoleSupport
{
    public static string GetConsoleOutputDeviceName() => NativeLib.IsWindows ? "CONOUT$" : "/dev/tty";

    internal static readonly object ConsoleSync = new();

    public static string LineFormat(this string message,
                                    int IndentWidth = 0,
                                    int? LineWidth = default,
                                    char WordDelimiter = ' ',
                                    char FillChar = ' ')
    {
        int Width;

        if (LineWidth.HasValue)
        {
            Width = LineWidth.Value;
        }
        else if (Console.IsOutputRedirected)
        {
            Width = 79;
        }
        else
        {
            Width = Console.WindowWidth - 1;
        }

        var origLines = message.AsMemory().Split('\n');

        var resultLines = new List<string>();

        var result = new StringBuilder();

        var line = new StringBuilder(Width);

        foreach (var origLineIt in origLines)
        {

            result.Length = 0;
            line.Length = 0;

            var origLine = origLineIt.TrimEnd('\r');

            foreach (var Word in origLine.Split(WordDelimiter))
            {
                if (Word.Length >= Width)
                {
                    result.AppendLine(Word.ToString());
                    continue;
                }

                if (Word.Length + line.Length >= Width)
                {
                    result.AppendLine(line.ToString());
                    line.Length = 0;
                    line.Append(FillChar, IndentWidth);
                }
                else if (line.Length > 0)
                {
                    line.Append(WordDelimiter);
                }

                line.Append(Word);
            }

            if (line.Length > 0)
            {
                result.Append(line);
            }

            resultLines.Add(result.ToString());

        }

        return string.Join(Environment.NewLine, resultLines);

    }

    public static TraceLevel WriteMsgTraceLevel { get; set; } = TraceLevel.Info;

    public static void WriteMsg(TraceLevel level, string msg)
    {
        var color = level switch
        {
            TraceLevel.Off => ConsoleColor.Cyan,
            TraceLevel.Error => ConsoleColor.Red,
            TraceLevel.Warning => ConsoleColor.Yellow,
            TraceLevel.Info => ConsoleColor.Gray,
            TraceLevel.Verbose => ConsoleColor.DarkGray,
            _ => ConsoleColor.DarkGray,
        };

        if (level <= WriteMsgTraceLevel)
        {
            lock (ConsoleSync)
            {
                Console.ForegroundColor = color;

                Console.WriteLine(msg.LineFormat());

                Console.ResetColor();
            }
        }
    }

    public static Dictionary<string, string[]> ParseCommandLine(IEnumerable<string> args, StringComparer comparer)
    {
        var dict = ParseCommandLineParameter(args)
            .GroupBy(item => item.Key, item => item.Value, comparer)
            .ToDictionary(item => item.Key, item => item.SelectMany(i => i ?? Enumerable.Empty<string>()).ToArray(), comparer);

        return dict;
    }

    public static IEnumerable<KeyValuePair<string, IEnumerable<string>?>> ParseCommandLineParameter(IEnumerable<string> args)
    {
        var switches_finished = false;

        foreach (var arg in args)
        {
            if (switches_finished)
            {
            }
            else if (arg.Length == 0 || arg == "-")
            {
                switches_finished = true;
            }
            else if (arg == "--")
            {
                switches_finished = true;
                continue;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal)
                || Path.DirectorySeparatorChar != '/'
                && arg.StartsWith('/'))
            {
                var namestart = 1;
                if (arg[0] == '-')
                {
                    namestart = 2;
                }

                var valuepos = arg.IndexOf('=');
                if (valuepos < 0)
                {
                    valuepos = arg.IndexOf(':');
                }

                string name;
                IEnumerable<string> value;

                if (valuepos >= 0)
                {
                    name = arg.Substring(namestart, valuepos - namestart);
                    value = SingleValueEnumerable.Get(arg.Substring(valuepos + 1));
                }
                else
                {
                    name = arg.Substring(namestart);
                    value = Enumerable.Empty<string>();
                }

                yield return new(name, value);
            }
            else if (arg.StartsWith('-'))
            {
                for (int i = 1, loopTo = arg.Length - 1; i <= loopTo; i++)
                {
                    var name = arg.Substring(i, 1);

                    if (i + 1 < arg.Length && (arg[i + 1] == '=' || arg[i + 1] == ':'))
                    {
                        var value = SingleValueEnumerable.Get(arg.Substring(i + 2));

                        yield return new(name, value);
                        break;
                    }

                    yield return new(name, null);
                }
            }
            else
            {
                switches_finished = true;
            }

            if (switches_finished)
            {
                yield return new(string.Empty, SingleValueEnumerable.Get(arg));
            }
        }
    }
}

