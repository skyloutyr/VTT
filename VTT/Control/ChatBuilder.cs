namespace VTT.Util
{
    using NCalc.Domain;
    using SixLabors.ImageSharp;
    using System;
    using System.Runtime.ConstrainedExecution;
    using System.Text;
    using VTT.Control;

    public class ChatBuilder
    {
        private readonly StringBuilder _chatLine = new StringBuilder();

        public ChatBuilder SpecifyRenderType(ChatLine.RenderType rt)
        {
            this._chatLine.Append($"[m:{Enum.GetName(rt)}]");
            return this;
        }

        public ChatBuilder AddText(string text)
        {
            this._chatLine.Append(text);
            return this;
        }

        public ChatBuilder AddCustomBlock(char identifier, string contents)
        {
            this._chatLine.Append($"[{identifier}:{contents}]");
            return this;
        }

        public ChatBuilder AddPassthrough(string contents)
        {
            this._chatLine.Append($"[p:{contents}]");
            return this;
        }

        public ChatBuilder SetColor(Color clr)
        {
            this._chatLine.Append($"[c:0x{clr.Argb():x8}]");
            return this;
        }

        public ChatBuilder ResetColor()
        {
            this._chatLine.Append("[c:r]");
            return this;
        }

        public ChatBuilder SetColorToSender()
        {
            this._chatLine.Append("[c:u]");
            return this;
        }

        public ChatBuilder SetTooltip(string tt)
        {
            this._chatLine.Append($"[t:{Escape(tt)}]");
            return this;
        }

        public ChatBuilder EnterRecursive()
        {
            this._chatLine.Append($"[r:");
            return this;
        }

        public ChatBuilder EndRecursive()
        {
            this._chatLine.Append(']');
            return this;
        }

        public ChatBuilder SpecifyDestination(Guid id)
        {
            this._chatLine.Append($"[d:{id}]");
            return this;
        }

        public ChatBuilder SpecifySenderName(string name)
        {
            this._chatLine.Append($"[n:{name}]");
            return this;
        }

        public ChatBuilder SpecifyObjectRef(Guid id)
        {
            this._chatLine.Append($"[o:{id}]");
            return this;
        }

        public ChatBuilder AddInlineImage(Guid id)
        {
            this._chatLine.Append($"[i:{id}]");
            return this;
        }

        public ChatBuilder AddInlineImage(Uri uri)
        {
            this._chatLine.Append($"[i:{uri}]");
            return this;
        }

        public static string Escape(string s)
        {
            char prev;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; ++i)
            {
                prev = i > 0 ? s[i - 1] : '\0';
                char c = s[i];
                if (c is '[' or ']')
                {
                    if (prev != '\\')
                    {
                        sb.Append('\\');
                    }
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        public string Build() => this._chatLine.ToString();
        public override string ToString() => this.Build();
    }
}
