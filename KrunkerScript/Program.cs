using KrunkScriptParser.Models;
using KrunkScriptParser.Validator;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace KrunkScript
{
    class Program
    {
        private static int _lastLine = 0;

        static void Main(string[] args)
        {
            foreach (string file in Directory.GetFiles("Tests", "*.krnk", SearchOption.AllDirectories))
            {
                if (file.EndsWith("globalObjects.krnk"))
                {
                    continue;
                }

                Console.WriteLine($"Parsing ... {file}");

                KSValidator validator = new KSValidator(File.ReadAllText(file));
                validator.OnValidationError += Validator_OnValidationError;

                Stopwatch sw = Stopwatch.StartNew();

                validator.Validate();

                Console.WriteLine($"\nValidation completed in {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"{file}");
            }

            Console.ReadLine();
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
