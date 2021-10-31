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
            KSValidator validator = new KSValidator(File.ReadAllText("client.krnk"));

            validator.OnValidationError += Validator_OnValidationError;

            validator.Validate();

            Console.ReadLine();
        }

        private static void Validator_OnValidationError(object sender, ValidationException e)
        {
            Console.WriteLine(e);
        }
    }
}
