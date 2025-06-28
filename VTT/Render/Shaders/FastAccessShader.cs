namespace VTT.Render.Shaders
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using VTT.GL;
    using VTT.Network;

    public class FastAccessShader<T> where T : new()
    {
        public ShaderProgram Program { get; }

        public T Uniforms { get; }

        public FastAccessShader(ShaderProgram shader)
        {
            this.Program = shader;
            this.Program.Bind();

            this.Uniforms = new T();
            Dictionary<string, object> knownUniformStates = new Dictionary<string, object>();

            static void VisitType(Type t, object instance, ShaderProgram shader, Dictionary<string, object> cache, ref int usageMetricsNominal, ref int usageMetricsWorst, ref int slotsTaken)
            {
                foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() == typeof(UniformState<>))
                    {
                        UniformReferenceAttribute attrib = pi.GetCustomAttribute<UniformReferenceAttribute>();
                        if (attrib != null)
                        {
                            string key = attrib.Value;
                            if (!cache.TryGetValue(key, out object val))
                            {
                                dynamic value = Activator.CreateInstance(pi.PropertyType, shader, attrib.Value, attrib.Array, attrib.CheckValue);
                                usageMetricsNominal += value.MachineUnitsNominalUsage;
                                usageMetricsWorst += value.MachineUnitsWorstCaseUsage;
                                slotsTaken += value.UniformSlotsTaken;
                                cache[key] = val = value;
                            }

                            pi.SetValue(instance, val);
                        }
                    }
                    else
                    {
                        if (pi.GetCustomAttribute<UniformContainerAttribute>() != null)
                        {
                            object o = pi.GetValue(instance);
                            o ??= Activator.CreateInstance(pi.PropertyType);
                            VisitType(o.GetType(), o, shader, cache, ref usageMetricsNominal, ref usageMetricsWorst, ref slotsTaken);
                        }
                    }
                }

                MethodInfo mi = t.GetMethod("PostConstruct", BindingFlags.Public | BindingFlags.Instance);
                mi?.Invoke(instance, Array.Empty<object>());
            }

            int nominal = 0, worst = 0, slots = 0;
            VisitType(this.Uniforms.GetType(), this.Uniforms, this, knownUniformStates, ref nominal, ref worst, ref slots);
            Client.Instance.Logger.Log(Util.LogLevel.Debug, $"Program {shader.ProgramID} uniform metrics usage: {slots} uniform slots taken, {nominal} bytes used, {worst} bytes usage in worst-case (forced std140)");
            if (slots >= 1024)
            {
                Client.Instance.Logger.Log(Util.LogLevel.Warn, $"Program {shader.ProgramID} potentially uses too many uniform bindings ({slots}/1024 spec min), and may not compile on all target GPU drivers, unless ubos are enabled!");
            }

            if (worst > 65536)
            {
                Client.Instance.Logger.Log(Util.LogLevel.Warn, $"Program {shader.ProgramID} potentially uses too many machine units for uniforms ({worst}/65536 spec min), and may not compile on all target GPU drivers!");
            }
        }

        public void Bind() => this.Program.Bind();

        public static implicit operator ShaderProgram(FastAccessShader<T> self) => self.Program;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class UniformReferenceAttribute : Attribute
    {
        public string Value { get; set; }
        public bool Array { get; set; } = false;
        public bool CheckValue { get; set; } = true;

        public UniformReferenceAttribute(string value)
        {
            this.Value = value;
            this.Array = false;
            this.CheckValue = true;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class UniformContainerAttribute : Attribute
    {
    }
}
