namespace VTT.Network.VSCC
{
    using Newtonsoft.Json.Linq;
    using System.Text;
    using VTT.Util;

    public interface IIntegrator
    {
        public bool Accepts(string type);
        public bool Process(JObject data, string fullMessage, VSCCIntegration integration);

        public static string SanitizeInput(string input)
        {
            StringBuilder sb = new StringBuilder();
            bool hasBracket = false;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '[')
                {
                    bool escaped = IsEscaped(input, i);
                    if (!escaped)
                    {
                        if (!hasBracket)
                        {
                            hasBracket = true;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }

                if (c == ']')
                {
                    bool escaped = IsEscaped(input, i);
                    if (!escaped)
                    {
                        if (hasBracket)
                        {
                            hasBracket = false;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }

                sb.Append(c);
            }

            return ChatParser.FixXdY(sb.ToString());
        }

        public static string SanitizeR20Harsh(string input)
        {
            if (input.StartsWith("[["))
            {
                input = input[2..];
            }

            if (input.EndsWith("]]"))
            {
                input = input[..^2];
            }

            input = input.Replace("[[", "(");
            input = input.Replace("]]", ")");
            int nBrackets = 0;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '[')
                {
                    nBrackets++;
                    continue;
                }

                if (c == ']')
                {
                    nBrackets--;
                    continue;
                }

                if (nBrackets == 0)
                {
                    sb.Append(c);
                }
            }

            return ChatParser.FixXdY(sb.ToString());
        }

        private static bool IsEscaped(string sIn, int index) => index != 0 && sIn[index - 1] == '\\';
    }
}
