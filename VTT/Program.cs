namespace VTT
{
    using Newtonsoft.Json.Linq;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using VTT.Network;
    using VTT.Util;

    public static class Program
    {
        public static Assembly Code { get; internal set; }

        public static Version Version { get; internal set; }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            void Run()
            {
                CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
                Code = Assembly.GetExecutingAssembly();
                Console.Clear();
                Version = new Version(1, 1, 3);
                ArgsManager.Parse(args);
                if (ArgsManager.TryGetValue("server", out int port))
                {
                    Server s = new Server(IPAddress.Any, port);
                    AutoResetEvent are = new AutoResetEvent(false);
                    s.Create(are);
                    are.WaitOne();
                    s.Dispose();
                }
                else
                {
                    Client c = new Client();
                    if (ArgsManager.TryGetValue("connect", out IPEndPoint ip))
                    {
                        c.Connect(ip);
                    }
                    else
                    {
                        if (ArgsManager.TryGetValue("quick", out bool b) && b)
                        {
                            int sport = 23551;
                            Server s = new Server(IPAddress.Any, sport);
                            s.LocalAdminID = c.ID;
                            s.Create();
                            c.Connect(new IPEndPoint(IPAddress.Loopback, sport));
                        }
                    }

                    c.Frontend.Run();
                }
            }
#if DEBUG
            Run();
#else
            try
            {
                Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("A critical exception occured!");
                Console.WriteLine(e.Message);
                foreach (string s in e.StackTrace.Split('\n'))
                {
                    Console.WriteLine(s);
                }

                Console.ReadKey();
            }
#endif
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                string eT = e.ExceptionObject?.ToString() ?? string.Empty;
                if (e.ExceptionObject is Exception exe && !string.IsNullOrEmpty(exe.StackTrace))
                {
                    foreach (string l in exe.StackTrace.Split('\n'))
                    {
                        eT += l + '\n';
                    }
                }

                File.WriteAllText(Path.Combine(IOVTT.AppDir, "crash-" + DateTimeOffset.Now.ToString("dd-MM-yyyy-HH-mm-ss") + ".txt"), eT);
            }
            catch
            {
                // NOOP
            }
        }

        public static ulong GetVersionBytes() => (((ulong)(ushort)Version.Major) << 48) | (((ulong)(ushort)Version.Minor) << 32) | (uint)Version.Build;

        private static System.Numerics.Matrix4x4 RotationMatrixFromQuat(System.Numerics.Quaternion q)
        {
            float sqx = q.X * q.X;
            float sqy = q.Y * q.Y;
            float sqz = q.Z * q.Z;
            float sqw = q.W * q.W;

            float xy = q.X * q.Y;
            float xz = q.X * q.Z;
            float xw = q.X * q.W;

            float yz = q.Y * q.Z;
            float yw = q.Y * q.W;

            float zw = q.Z * q.W;

            float s2 = 2f / (sqx + sqy + sqz + sqw);

            System.Numerics.Matrix4x4 result = new System.Numerics.Matrix4x4();

            result.M11 = 1f - (s2 * (sqy + sqz));
            result.M22 = 1f - (s2 * (sqx + sqz));
            result.M33 = 1f - (s2 * (sqx + sqy));

            result.M12 = s2 * (xy + zw);
            result.M21 = s2 * (xy - zw);

            result.M31 = s2 * (xz + yw);
            result.M13 = s2 * (xz - yw);

            result.M32 = s2 * (yz - xw);
            result.M23 = s2 * (yz + xw);

            result.M14 = 0;
            result.M24 = 0;
            result.M34 = 0;

            result.M41 = 0;
            result.M42 = 0;
            result.M43 = 0;
            result.M44 = 1;
            return result;
        }

        public static void Consistensy()
        {
            Random rand = new Random();
            float tX = (float)rand.NextDouble();
            float tY = (float)rand.NextDouble();
            float tZ = (float)rand.NextDouble();

            float sX = (float)rand.NextDouble();
            float sY = (float)rand.NextDouble();
            float sZ = (float)rand.NextDouble();

            float qX = (float)rand.NextDouble();
            float qY = (float)rand.NextDouble();
            float qZ = (float)rand.NextDouble();
            float qW = (float)rand.NextDouble();

            OpenTK.Mathematics.Matrix4 glTranslation = OpenTK.Mathematics.Matrix4.CreateTranslation(new (tX, tY, tZ));
            OpenTK.Mathematics.Matrix4 glScale = OpenTK.Mathematics.Matrix4.CreateScale(new OpenTK.Mathematics.Vector3(sX, sY, sZ));
            OpenTK.Mathematics.Quaternion glQuat = new OpenTK.Mathematics.Quaternion(qX, qY, qZ, qW);
            OpenTK.Mathematics.Matrix4 glRotation = OpenTK.Mathematics.Matrix4.CreateFromQuaternion(glQuat);

            System.Numerics.Matrix4x4 sysTranslation = System.Numerics.Matrix4x4.CreateTranslation(new(tX, tY, tZ));
            System.Numerics.Matrix4x4 sysScale = System.Numerics.Matrix4x4.CreateScale(new System.Numerics.Vector3(sX, sY, sZ));
            System.Numerics.Quaternion sysQuat = new System.Numerics.Quaternion(qX, qY, qZ, qW);
            System.Numerics.Matrix4x4 sysRotation = RotationMatrixFromQuat(sysQuat);

            OpenTK.Mathematics.Matrix4 glMat = glScale * glRotation * glTranslation;
            System.Numerics.Matrix4x4 sysMat = sysScale * sysRotation * sysTranslation;
            System.Diagnostics.Debugger.Break();
        }

        public static void Profile()
        {
            Random rand = new Random();
            OpenTK.Mathematics.Vector3 glAccum = default;
            OpenTK.Mathematics.Vector4 glTrans = default;
            System.Numerics.Vector3 sysAccum = new System.Numerics.Vector3();
            System.Numerics.Vector4 sysTrans = new System.Numerics.Vector4();
            int numTests = 10000000;
            Stopwatch sw = new Stopwatch();
            Console.WriteLine("GL testing");
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                glAccum += new OpenTK.Mathematics.Vector3((float)rand.Next());
            }

            sw.Stop();
            Console.WriteLine("    op_Addition(vec3, vec3) took " + sw.ElapsedTicks + " ticks");
            sw.Reset();
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                glAccum -= new OpenTK.Mathematics.Vector3((float)rand.Next());
            }

            sw.Stop();
            Console.WriteLine("    op_Subtraction(vec3, vec3) took " + sw.ElapsedTicks + " ticks");
            sw.Reset();
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                glAccum *= new OpenTK.Mathematics.Vector3((float)rand.Next());
            }

            sw.Stop();
            Console.WriteLine("    op_Multiplication(vec3, vec3) took " + sw.ElapsedTicks + " ticks"); 
            sw.Reset();
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                glAccum /= (float)rand.Next();
            }

            sw.Stop();
            Console.WriteLine("    op_Division(vec3, float) took " + sw.ElapsedTicks + " ticks");
            sw.Reset();
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                OpenTK.Mathematics.Matrix4 m = new OpenTK.Mathematics.Matrix4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
                glTrans = glTrans * m;
            }

            sw.Stop();
            Console.WriteLine("    Vector4.TransformRow took " + sw.ElapsedTicks + " ticks");
            sw.Reset();


            Console.WriteLine("");

            Console.WriteLine("System testing");
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                sysAccum += new System.Numerics.Vector3((float)rand.Next());
            }

            sw.Stop();
            Console.WriteLine("    op_Addition(vec3, vec3) took " + sw.ElapsedTicks + " ticks");
            sw.Reset();
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                sysAccum -= new System.Numerics.Vector3((float)rand.Next());
            }

            sw.Stop();
            Console.WriteLine("    op_Subtraction(vec3, vec3) took " + sw.ElapsedTicks + " ticks");
            sw.Reset();
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                sysAccum *= new System.Numerics.Vector3((float)rand.Next());
            }

            sw.Stop();
            Console.WriteLine("    op_Multiplication(vec3, vec3) took " + sw.ElapsedTicks + " ticks");
            sw.Reset();
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                sysAccum /= (float)rand.Next();
            }

            sw.Stop();
            Console.WriteLine("    op_Divisiion(vec3, float) took " + sw.ElapsedTicks + " ticks");
            sw.Reset(); 
            sw.Start();
            for (int i = 0; i < numTests; ++i)
            {
                System.Numerics.Matrix4x4 m = new System.Numerics.Matrix4x4((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
                sysTrans = System.Numerics.Vector4.Transform(sysTrans, m);
            }

            sw.Stop();
            Console.WriteLine("    Vector4.Transform took " + sw.ElapsedTicks + " ticks");
            sw.Reset();

            Console.WriteLine("");
            Console.WriteLine("");
        }

        private static void CreateAtlas()
        {
            string folder = "E:/RPGIconset/Latest";
            string[] files = Directory.GetFiles(folder);
            int maxW = 4096;

            int w = 0;
            int mW = 0;
            int h = 0;
            for (int i = 0; i < files.Length; ++i)
            {
                w += 64;
                if (w >= maxW)
                {
                    w = 0;
                    h += 64;
                    mW = maxW;
                }
            }

            mW = Math.Max(mW, w);
            h += 64;

            Image<Rgba32> r = new Image<Rgba32>(mW, h);
            Image<Rgba32> r_low = new Image<Rgba32>(mW / 2, h / 2);
            (string, float, float)[] imgDatas = new (string, float, float)[files.Length];

            Parallel.For(0, files.Length, i =>
            {
                int cw = (i * 64) % mW;
                int ch = ((i * 64) / mW) * 64;

                Image<Rgba32> img = Image.Load<Rgba32>(files[i]);
                string name = Path.GetFileNameWithoutExtension(files[i]);
                if (name.IndexOf('.') != -1)
                {
                    name = name[..(name.LastIndexOf('.'))];
                }

                imgDatas[i] = (name, (float)cw / mW, (float)ch / h);
                img.Mutate(x => x.Resize(64, 64));
                r.Mutate(x => x.DrawImage(img, new Point(cw, ch), 1));

                img.Mutate(x => x.Resize(32, 32));
                r_low.Mutate(x => x.DrawImage(img, new Point(cw / 2, ch / 2), 1));

                img.Dispose();
            });

            r.SaveAsPng("atlas.png");
            r_low.SaveAsPng("atlas_low.png");
            JObject jr = new JObject();
            jr["width"] = r.Width;
            jr["height"] = r.Height;
            JArray ja = new JArray();
            for (int i = 0; i < imgDatas.Length; ++i)
            {
                (string, float, float) d = imgDatas[i];
                ja.Add(new JObject());
                ja[i]["name"] = d.Item1;
                ja[i]["s"] = d.Item2;
                ja[i]["t"] = d.Item3;
            }

            jr["imgs"] = ja;
            File.WriteAllText("atlas_info.json", jr.ToString(Newtonsoft.Json.Formatting.Indented));
        }
    }
}
