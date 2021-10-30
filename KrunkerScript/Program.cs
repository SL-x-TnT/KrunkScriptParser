using KrunkScriptParser;
using KrunkScriptParser.Models;
using System;
using System.IO;
using System.Linq;

namespace KrunkScript
{
    class Program
    {
        static void Main(string[] args)
        {
            KrunkScriptValidator validator = new KrunkScriptValidator();

            KSInfo info = validator.Validate(File.ReadAllText("client.krnk"));

            Console.WriteLine(String.Join("\n", info.ValidationExceptions.Select(x => $"{x.Message}. ({x.LineNumber}:{x.ColumnNumber})")));

            Console.ReadLine();
        }
    }
}
