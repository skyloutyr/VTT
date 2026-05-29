namespace VTT.Network.HTTPAPI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    public abstract class APIMethod
    {
        public static Dictionary<string, Type> MethodsByIdentifier { get; } = new Dictionary<string, Type>();
        public static List<string> MethodDocs { get; } = new();

        static APIMethod()
        {
            List<Type> apiTypes = new List<Type>();

            foreach (Type t in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (t.IsAssignableTo(typeof(APIMethod)) && !t.IsAbstract)
                {
                    apiTypes.Add(t);
                }
            }

            foreach (Type t in apiTypes)
            {
                APIMethod pb = (APIMethod)Activator.CreateInstance(t);
                MethodsByIdentifier.Add(pb.Identifier, t);
                MethodDocs.Add(GenerateDocs(t, pb));
            }
        }

        public static bool TryAccept(string identifier, out APIMethod method)
        {
            if (MethodsByIdentifier.TryGetValue(identifier, out Type t))
            {
                method = (APIMethod)Activator.CreateInstance(t);
                return true;
            }

            method = null;
            return false;
        }

        public abstract string Identifier { get; }

        public ServerClient OnlineClient { get; set; }
        public ClientInfo IdentifiedClient { get; set; }
        public bool IsIdentified => this.IdentifiedClient != null;

        public abstract void Construct(HTTPAPIEndpoint api, Dictionary<string, string> kvs);
        public abstract void Act(HTTPAPIEndpoint api);

        protected bool TryGet(Dictionary<string, string> kvs, string key, out Guid id)
        {
            if (!kvs.TryGetValue(key, out string val) || !Guid.TryParse(val, out id))
            {
                id = Guid.Empty;
                return false;
            }

            return true;
        }

        protected bool TryGet(Dictionary<string, string> kvs, string key, out string str) => kvs.TryGetValue(key, out str);

        private static string GenerateDocs(Type t, APIMethod instance)
        {
            StringBuilder sb = new StringBuilder();
            APIMethodDocsAttribute typeAttribute = t.GetCustomAttribute<APIMethodDocsAttribute>();
            if (typeAttribute != null)
            {
                sb.AppendLine($"<h3>{typeAttribute.Name}</h3>");
                foreach (string tag in typeAttribute.Tags)
                {
                    sb.AppendLine($"<div class=\"prop\">{tag}</div>");
                }

                sb.AppendLine("<br>");
            }

            List<(string paramKey, string paramValue, bool escapeValue, string paramDesc)> parameters = new List<(string paramKey, string paramValue, bool escapeValue, string paramDesc)>
            {
                ("method", instance.Identifier.ToLower(), true, $"must be <b>{instance.Identifier.ToLower()}</b>.")
            };

            foreach (APIMethodDocsAttribute doc in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(x => x.GetCustomAttribute<APIMethodDocsAttribute>()).Where(x => x != null))
            {
                parameters.Add((doc.Name, doc.ValueKey, doc.EscapeValue, doc.Desc));
            }

            sb.AppendLine($"POST: {string.Join("&", parameters.Select(x => $"{x.paramKey}={x.paramValue}"))}");
            sb.AppendLine("<br>");
            sb.AppendLine($"WS: {{ {string.Join(", ", parameters.Select(x => $"\"{x.paramKey}\": {(x.escapeValue ? $"\"{x.paramValue}\"" : $"{x.paramValue}")}"))} }}");
            sb.AppendLine("<br>");
            sb.AppendLine("<br>");
            sb.AppendLine(typeAttribute?.Desc ?? string.Empty);
            sb.AppendLine("<br>");
            sb.AppendLine("<br>");
            sb.AppendLine("<b>Parameters:</b>");
            sb.AppendLine("<div class=\"indentblock\">");
            foreach ((string paramKey, string paramValue, bool escapeValue, string paramDesc) in parameters)
            {
                sb.AppendLine($"<b>{paramKey}</b>: {paramDesc}");
                sb.AppendLine("<br>");
            }

            sb.AppendLine("</div>");
            sb.AppendLine("<b>Returns:</b>");
            sb.AppendLine("<div class=\"indentblock\">");
            if (typeAttribute != null && typeAttribute.Returns != null)
            {
                foreach (string ret in typeAttribute.Returns)
                {
                    sb.AppendLine(ret);
                    sb.AppendLine("<br>");
                }
            }

            sb.AppendLine("</div>");
            return sb.ToString();
        }
    }
}
