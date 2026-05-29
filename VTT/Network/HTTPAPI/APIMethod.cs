namespace VTT.Network.HTTPAPI
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    public abstract class APIMethod
    {
        public static Dictionary<string, Type> MethodsByIdentifier { get; } = new Dictionary<string, Type>();

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
    }
}
