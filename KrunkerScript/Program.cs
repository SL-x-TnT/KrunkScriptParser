﻿using KrunkScriptParser.Models;
using KrunkScriptParser.Validator;
using System;
using System.IO;
using System.Linq;

namespace KrunkScript
{
    class Program
    {
        private static int _lastLine = 0;

        static void Main(string[] args)
        {
            KSValidator validator = new KSValidator(File.ReadAllText("client.krnk"));

            validator.OnValidationError += Validator_OnValidationError;

            validator.Validate();

            Console.ReadLine();
        }

        private static void Validator_OnValidationError(object sender, ValidationException e)
        {
            ConsoleColor prevColor = Console.ForegroundColor;

            Console.ForegroundColor = e.Level == Level.Error ? ConsoleColor.Red : 
                e.Level == Level.Warning ? ConsoleColor.Yellow : ConsoleColor.Gray;

            if (_lastLine == e.LineNumber)
            {
                Console.Write("\t");
                Console.WriteLine($"({e.LineNumber}:{e.ColumnNumber}) {e.Message}");
            }
            else
            {
                Console.WriteLine(e);
            }
            Console.ForegroundColor = prevColor;

            _lastLine = e.LineNumber;
        }
    }
}
