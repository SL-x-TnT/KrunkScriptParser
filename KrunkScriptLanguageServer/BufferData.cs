using KrunkScriptParser.Validator;
using System;
using System.Collections.Generic;
using System.Text;

namespace KrunkScriptLanguageServer
{
    class BufferData
    {
        public KSValidator Validator { get; set; }
        public string Buffer { get; set; }

        public string GetText(int line, int position)
        {
            int cursorPosition = GetPosition(line, position);

            int startPosition = cursorPosition;

            for(int i = cursorPosition - 1; i >= 0; --i)
            {
                if(!ValidChar(Buffer[i]))
                {
                    break;
                }

                --startPosition;
            }

            return Buffer.Substring(startPosition, cursorPosition - startPosition);
        }

        private int GetPosition(int line, int position)
        {
            int count = 0;
            for(int i = 0; i < Buffer.Length; i++)
            {
                if(Buffer[i] == '\n')
                {
                    --line;
                }

                ++count;

                if (line == 0)
                {
                    break;
                }
            }

            return count + position;
        }

        private bool ValidChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '.' || c== ' ';
        }
    }
}
