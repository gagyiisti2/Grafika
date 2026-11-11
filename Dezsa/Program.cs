using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Dezsa
{
    internal static class Program
    {
        private static CameraDescriptor cameraDescriptor = new();
        private static IWindow window;
        private static IInputContext inputContext;
        private static GL Gl;
        private static ImGuiController controller;

        private static uint program;

        private static GlStave flatStave;
        private static GlStave smoothStave;

        private static float Shininess = 50f;

        // Dezsa adatai
        private static int StaveCount = 18;
        private static float StaveWidth = 1f;
        private static float StaveHeight = 2f;
        private static float GapBetweenBarrels = 1f;
        private static readonly float[] StaveColor = [0.65f, 0.45f, 0.25f, 1.0f]; // fa-barna

        // Szogek kiszamolasa
        private static readonly double StepAngle = 2 * Math.PI / StaveCount;
        private static readonly double HalfAngle = Math.PI / StaveCount;

        // tavolsagok kiszamolasa
        private static readonly float StaveDistance = (float)(StaveWidth / 2 / Math.Tan(HalfAngle));
        private static readonly float BarellOuterRadius = (float)(StaveWidth / 2 / Math.Sin(HalfAngle));
        private static readonly float BarrellOffsetX = BarellOuterRadius + GapBetweenBarrels / 2;

        // Guard vagy Phong
        private static uint phongProgram;
        private static uint gouraudProgram;

        private static int ShadingMode = 0; // 0 - phong, 1 - gouraud

        private const string ModelMatrixVariableName = "uModel";
        private const string NormalMatrixVariableName = "uNormal";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private static readonly string PhongVertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;
        layout (location = 2) in vec3 vNorm;
 
        uniform mat4 uModel;
        uniform mat3 uNormal;
        uniform mat4 uView;
        uniform mat4 uProjection;
 
        out vec4 outCol;
        out vec3 outNormal;
        out vec3 outWorldPosition;
 
        void main()
        {
            outCol = vCol;
            gl_Position = uProjection*uView*uModel*vec4(vPos.x, vPos.y, vPos.z, 1.0);
            outNormal = uNormal*vNorm;
            outWorldPosition = vec3(uModel*vec4(vPos.x, vPos.y, vPos.z, 1.0));
        }
        ";

        private static readonly string GouraudVertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;
        layout (location = 2) in vec3 vNorm;
 
        uniform mat4 uModel;
        uniform mat3 uNormal;
        uniform mat4 uView;
        uniform mat4 uProjection;

        uniform vec3 lightColor;
        uniform vec3 lightPos;
        uniform vec3 viewPos;
        uniform float shininess;
 
        out vec4 outCol;
 
        void main()
        {
            vec4 worldPosition = uModel * vec4(vPos, 1.0);
            gl_Position = uProjection * uView * worldPosition;

            vec3 worldPos = worldPosition.xyz;
            vec3 norm = normalize(uNormal * vNorm);
            
            float ambientStrength = 0.2;
            vec3 ambient = ambientStrength * lightColor;
            
            float diffuseStrength = 0.3;
            vec3 lightDir = normalize(lightPos - worldPos);
            float diff = max(dot(norm, lightDir), 0.0);
            vec3 diffuse = diff * lightColor * diffuseStrength;

            float specularStrength = 0.5;
            vec3 viewDir = normalize(viewPos - worldPos);
            vec3 reflectDir = reflect(-lightDir, norm);
            float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess) / max(dot(norm,viewDir), -dot(norm,lightDir));
            vec3 specular = specularStrength * spec * lightColor;

            vec3 result = (ambient + diffuse + specular) * vCol.rgb;
            outCol = vec4(result, vCol.a);
        }
        ";

        private const string LightColorVariableName = "lightColor";
        private const string LightPositionVariableName = "lightPos";
        private const string ViewPosVariableName = "viewPos";
        private const string ShininessVariableName = "shininess";

        private static readonly string PhongFragmentShaderSource = @"
        #version 330 core
 
        uniform vec3 lightColor;
        uniform vec3 lightPos;
        uniform vec3 viewPos;
        uniform float shininess;
 
        out vec4 FragColor;
 
        in vec4 outCol;
        in vec3 outNormal;
        in vec3 outWorldPosition;
 
        void main()
        {
            float ambientStrength = 0.2;
            vec3 ambient = ambientStrength * lightColor;
 
            float diffuseStrength = 0.3;
            vec3 norm = normalize(outNormal);
            vec3 lightDir = normalize(lightPos - outWorldPosition);
            float diff = max(dot(norm, lightDir), 0.0);
            vec3 diffuse = diff * lightColor * diffuseStrength;
 
            float specularStrength = 0.5;
            vec3 viewDir = normalize(viewPos - outWorldPosition);
            vec3 reflectDir = reflect(-lightDir, norm);
            float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess) / max(dot(norm,viewDir), -dot(norm,lightDir));
            vec3 specular = specularStrength * spec * lightColor;
 
            vec3 result = (ambient + diffuse + specular) * outCol.xyz;
            FragColor = vec4(result, outCol.w);
        }
        ";

        private static readonly string GouraudFragmentShaderSource = @"
        #version 330 core
 
        out vec4 FragColor;
 
        in vec4 outCol;
 
        void main()
        {
            FragColor = outCol;
        }
        ";

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Dezsa";
            windowOptions.Size = new Vector2D<int>(1000, 600);

            // on some systems there is no depth buffer by default, so we need to make sure one is created
            windowOptions.PreferredDepthBufferBits = 24;

            window = Window.Create(windowOptions);

            window.Load += Window_Load;
            window.Update += Window_Update;
            window.Render += Window_Render;
            window.Closing += Window_Closing;

            window.Run();
        }
        private static void Window_Load()
        {
            //Console.WriteLine("Load");

            // set up input handling
            inputContext = window.CreateInput();
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }

            Gl = window.CreateOpenGL();

            controller = new ImGuiController(Gl, window, inputContext);

            // Handle resizes
            window.FramebufferResize += s =>
            {
                // Adjust the viewport to the new window size
                Gl.Viewport(s);
            };


            Gl.ClearColor(System.Drawing.Color.White);

            SetUpObjects();

            phongProgram = CreateProgram(PhongVertexShaderSource, PhongFragmentShaderSource);
            gouraudProgram = CreateProgram(GouraudVertexShaderSource, GouraudFragmentShaderSource);

            Gl.Enable(EnableCap.CullFace);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);
        }

        private static uint CreateProgram(string vertexSource, string fragmentSource)
        {
            uint vshader = CompileShader(ShaderType.VertexShader, vertexSource);
            uint fshader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            uint newProgram = Gl.CreateProgram();
            Gl.AttachShader(newProgram, vshader);
            Gl.AttachShader(newProgram, fshader);
            Gl.LinkProgram(newProgram);

            Gl.GetProgram(newProgram, GLEnum.LinkStatus, out int status);
            if (status == 0)
            {
                throw new Exception("Error linking shader: " + Gl.GetProgramInfoLog(newProgram));
            }

            Gl.DetachShader(newProgram, vshader);
            Gl.DetachShader(newProgram, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

            return newProgram;
        }

        private static uint CompileShader(ShaderType type, string source)
        {
            uint shader = Gl.CreateShader(type);
            Gl.ShaderSource(shader, source);
            Gl.CompileShader(shader);

            Gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status != (int)GLEnum.True)
            {
                throw new Exception($"{type} failed to compile: " + Gl.GetShaderInfoLog(shader));
            }

            return shader;
        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            switch (key)
            {
                case Key.Left:
                    cameraDescriptor.DecreaseZYAngle();
                    break;
                case Key.Right:
                    cameraDescriptor.IncreaseZYAngle();
                    break;
                case Key.Down:
                    cameraDescriptor.IncreaseDistance();
                    break;
                case Key.Up:
                    cameraDescriptor.DecreaseDistance();
                    break;
                case Key.U:
                    cameraDescriptor.IncreaseZXAngle();
                    break;
                case Key.D:
                    cameraDescriptor.DecreaseZXAngle();
                    break;
            }
        }

        private static void Window_Update(double deltaTime)
        {
            controller.Update((float)deltaTime);
        }

        private static unsafe void Window_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s].");

            // GL here
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);

            program = ShadingMode == 0 ? phongProgram : gouraudProgram;
            Gl.UseProgram(program);

            SetViewMatrix();
            SetProjectionMatrix();

            SetLightColor();
            SetLightPosition();
            SetViewerPosition();
            SetShininess();

            // Drawbarell
            DrawBarell(flatStave, -BarrellOffsetX); // bal oldalra
            DrawBarell(smoothStave, +BarrellOffsetX); // jobb oldalra

            //ImGuiNET.ImGui.ShowDemoWindow();
            ImGuiNET.ImGui.Begin("Lighting properties",
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);

            ImGuiNET.ImGui.Text("Arnyalas");
            ImGuiNET.ImGui.RadioButton("Phong", ref ShadingMode, 0);
            ImGuiNET.ImGui.SameLine();
            ImGuiNET.ImGui.RadioButton("Gouraud", ref ShadingMode, 1);
            
            ImGuiNET.ImGui.SliderFloat("Shininess", ref Shininess, 1, 200);
            ImGuiNET.ImGui.End();


            controller.Render();
        }

        private static unsafe void DrawBarell(GlStave stave, float offsetX)
        {
            Matrix4X4<float> pushOut = Matrix4X4.CreateTranslation(0f, 0f, StaveDistance);
            Matrix4X4<float> placeBarell = Matrix4X4.CreateTranslation(offsetX, 0f, 0f);

            Gl.BindVertexArray(stave.Vao);
            for(int i = 0; i < StaveCount; i++)
            {
                Matrix4X4<float> rotate = Matrix4X4.CreateRotationY((float)(i * StepAngle));

                Matrix4X4<float> modelMatrix = pushOut * rotate * placeBarell;

                SetModelMatrix(modelMatrix);
                Gl.DrawElements(GLEnum.Triangles, stave.IndexArrayLength, GLEnum.UnsignedInt, null);
            }
            Gl.BindVertexArray(0);
        }

        private static unsafe void SetLightColor()
        {
            int location = Gl.GetUniformLocation(program, LightColorVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightColorVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, 1f, 1f, 1f);
            CheckError();
        }

        private static unsafe void SetLightPosition()
        {
            int location = Gl.GetUniformLocation(program, LightPositionVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightPositionVariableName} uniform not found on shader.");
            }

            var lightPos = cameraDescriptor.Position;
            Gl.Uniform3(location, lightPos.X, lightPos.Y, lightPos.Z);
            CheckError();
        }

        private static unsafe void SetViewerPosition()
        {
            int location = Gl.GetUniformLocation(program, ViewPosVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewPosVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, cameraDescriptor.Position.X, cameraDescriptor.Position.Y, cameraDescriptor.Position.Z);
            CheckError();
        }

        private static unsafe void SetShininess()
        {
            int location = Gl.GetUniformLocation(program, ShininessVariableName);

            if (location == -1)
            {
                throw new Exception($"{ShininessVariableName} uniform not found on shader.");
            }

            Gl.Uniform1(location, Shininess);
            CheckError();
        }
           

        private static unsafe void SetModelMatrix(Matrix4X4<float> modelMatrix)
        {
            int location = Gl.GetUniformLocation(program, ModelMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{ModelMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&modelMatrix);
            CheckError();

            var modelMatrixWithoutTranslation = new Matrix4X4<float>(modelMatrix.Row1, modelMatrix.Row2, modelMatrix.Row3, modelMatrix.Row4);
            modelMatrixWithoutTranslation.M41 = 0;
            modelMatrixWithoutTranslation.M42 = 0;
            modelMatrixWithoutTranslation.M43 = 0;
            modelMatrixWithoutTranslation.M44 = 1;

            Matrix4X4<float> modelInvers;
            Matrix4X4.Invert<float>(modelMatrixWithoutTranslation, out modelInvers);
            Matrix3X3<float> normalMatrix = new Matrix3X3<float>(Matrix4X4.Transpose(modelInvers));
            location = Gl.GetUniformLocation(program, NormalMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{NormalMatrixVariableName} uniform not found on shader.");
            }
            Gl.UniformMatrix3(location, 1, false, (float*)&normalMatrix);
            CheckError();
        }

        private static unsafe void SetUpObjects()
        {
            flatStave = GlStave.CreateStave(Gl, StaveWidth, StaveHeight, StaveColor, false, HalfAngle);
            smoothStave = GlStave.CreateStave(Gl, StaveWidth, StaveHeight, StaveColor, true, HalfAngle);
        }


        private static void Window_Closing()
        {
            smoothStave.ReleaseGlStave();
            flatStave.ReleaseGlStave();
            Gl.DeleteProgram(phongProgram);
            Gl.DeleteProgram(gouraudProgram);
        }

        private static unsafe void SetProjectionMatrix()
        {
            var size = window.FramebufferSize;
            float aspect = size.Y > 0 ? (float)size.X / size.Y : 1f; 

            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)Math.PI / 4f, aspect, 0.1f, 100);
            int location = Gl.GetUniformLocation(program, ProjectionMatrixVariableName);

            if (location == -1)
            {
                throw new Exception($"{ProjectionMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&projectionMatrix);
            CheckError();
        }

        private static unsafe void SetViewMatrix()
        {
            var viewMatrix = Matrix4X4.CreateLookAt(cameraDescriptor.Position, 
                cameraDescriptor.Target, cameraDescriptor.UpVector);
            int location = Gl.GetUniformLocation(program, ViewMatrixVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&viewMatrix);
            CheckError();
        }

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
        }

    }
}
