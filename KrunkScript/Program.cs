using KrunkScriptParser.Models;
using KrunkScriptParser.Validator;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;

namespace KrunkScript
{
    class Program
    {
        private static int _lastLine = 0;
        private static FileInfo _fileName = null;
        private static FileSystemWatcher watcher;
        private static Timer _timer = new Timer(50);

        static void Main(string[] args)
        {
            _timer.Elapsed += _timer_Elapsed;
            string directory = Directory.GetCurrentDirectory();

            if (args.Length > 0)
            {
                directory = args[0];
            }

            //Was a file
            if(File.Exists(directory))
            {
                directory = new FileInfo(directory).DirectoryName;
            }
            else if(!Directory.Exists(directory))
            {
                Console.WriteLine($"'{directory}' does not exist");
                Console.ReadLine();

                return;
            }

            watcher = new FileSystemWatcher(directory, "*.krnk");
            //Letting it throw any exceptions it wants
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.IncludeSubdirectories = true;

            watcher.Changed += Watcher_Changed;
            watcher.EnableRaisingEvents = true;

            Console.WriteLine($"Watching directory '{directory}' for changes");


            Console.ReadLine();
        }

        private static void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Stop();

            ValidateFile(_fileName.FullName);
        }

        private static void ValidateFile(string file)
        {
            Console.Clear();

            Console.WriteLine($"Parsing '{file}'");

            Stopwatch sw = Stopwatch.StartNew();
            string text = String.Empty;

            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read file due to error\n\n{ex}");

                return;
            }

            KSValidator validator = new KSValidator(text);
            validator.OnValidationError += Validator_OnValidationError;
            validator.Validate();
            validator.OnValidationError -= Validator_OnValidationError;

            Console.WriteLine($"\nValidation complete in {sw.ElapsedMilliseconds}ms");
        }

        private static void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                _timer.Stop();

                _fileName = new FileInfo(e.FullPath);

                _timer.Start();
            }
        }

        private static void Validator_OnValidationError(object sender, ValidationException e)
        {
            ConsoleColor prevColor = Console.ForegroundColor;

            Console.ForegroundColor = e.Level == Level.Error ? ConsoleColor.Red : 
                e.Level == Level.Warning ? ConsoleColor.Yellow : ConsoleColor.Cyan;
            
            if (_lastLine == e.LineNumber)
            {
                Console.Write("\t");
                Console.WriteLine($"({e.LineNumber}:{e.ColumnNumber}) {e.Message}");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine(e);
            }

            Console.ForegroundColor = prevColor;

            _lastLine = e.LineNumber;
        }
    }
}
