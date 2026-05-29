namespace VTT.Network.HTTPAPI
{
    using System;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property)]
    public class APIMethodDocsAttribute : Attribute
    {
        public string Name { get; set; }
        public string[] Tags { get; set; }
        public string Desc { get; set; }
        public string ValueKey { get; set; }
        public string[] Returns { get; set; }
        public bool EscapeValue { get; set; } = true;
    }
}
