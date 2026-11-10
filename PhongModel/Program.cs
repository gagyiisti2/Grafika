using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Net.NetworkInformation;
using System.Numerics;

namespace PhongModel
{
    internal static class Program
    {
        private static CameraDescriptor cameraDescriptor = new();

        private static CubeArrangementModel cubeArrangementModel = new();

        private static IWindow window;

        private static IInputContext inputContext;

        private static GL Gl;

        private static ImGuiController controller;

        private static uint program;

        private static GlCube glCubeCentered;

        private static GlCube glCubeRotating;


        ///  UI-on allithato:
        private static float Shininess = 50;

        private static Vector3 AmbientStrength = new(0.2f, 0.2f, 0.2f);
        private static Vector3 DiffuseStrength = new(0.3f, 0.3f, 0.3f);
        private static Vector3 SpecularStrength = new (0.5f, 0.5f, 0.5f);
        private static Vector3 AmbientColor = new (1f, 1f, 1f); // Hatterfeny
        private static Vector3 LightColor = new (1f, 1f, 1f); // Fenyforras

        // kozponti kocka elulso oldala

        private const int EditableFace = GlCube.FrontFace;
        private static Vector4 FaceColor = new(1f, 0f, 0f, 1f); // kezdetben piros

        private static int SelectedMaterial = 0;
        private static readonly string[] MaterialNames = Material.Presets.Select(m => m.Name).ToArray();

        private const string ModelMatrixVariableName = "uModel";
        private const string NormalMatrixVariableName = "uNormal";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private static readonly string VertexShaderSource = @"
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

        private const string LightColorVariableName = "lightColor";
        private const string LightPositionVariableName = "lightPos";
        private const string ViewPosVariableName = "viewPos";
        private const string ShininessVariableName = "shininess";
        private const string AmbientStrengthVariableName = "ambientStrength";
        private const string DiffuseStrengthVariableName = "diffuseStrength";
        private const string SpecularStrengthVariableName = "specularStrength";
        private const string AmbientColorVariablename = "ambientColor";

        private static readonly string FragmentShaderSource = @"
        #version 330 core
        
        uniform vec3 lightColor;
        uniform vec3 lightPos;
        uniform vec3 viewPos;
        uniform float shininess;

        uniform vec3 ambientStrength;
        uniform vec3 diffuseStrength;
        uniform vec3 specularStrength;
        uniform vec3 ambientColor;

        out vec4 FragColor;

		in vec4 outCol;
        in vec3 outNormal;
        in vec3 outWorldPosition;

        void main()
        {
            vec3 ambient = ambientStrength * ambientColor;

            vec3 norm = normalize(outNormal);
            vec3 lightDir = normalize(lightPos - outWorldPosition);
            float diff = max(dot(norm, lightDir), 0.0);
            vec3 diffuse = diff * lightColor * diffuseStrength;

            vec3 viewDir = normalize(viewPos - outWorldPosition);
            vec3 reflectDir = reflect(-lightDir, norm);
            float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess) / max(dot(norm,viewDir), -dot(norm,lightDir));
            vec3 specular = specularStrength * spec * lightColor;  

            vec3 result = (ambient + diffuse + specular) * outCol.xyz;
            FragColor = vec4(result, outCol.w);
        }
        ";

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Phong Model";
            windowOptions.Size = new Vector2D<int>(1024, 768);

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

            LinkProgram();

            Gl.Enable(EnableCap.CullFace);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);
        }

        private static void LinkProgram()
        {
            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader);

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);
            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(program)}");
            }
            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            switch (key)
            {
                case Key.Left:
                    cameraDescriptor.DecreaseZYAngle();
                    break;
                    ;
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
                case Key.Space:
                    cubeArrangementModel.AnimationEnabeld = !cubeArrangementModel.AnimationEnabeld;
                    break;
            }
        }

        private static void Window_Update(double deltaTime)
        {
            //Console.WriteLine($"Update after {deltaTime} [s].");
            // multithreaded
            // make sure it is threadsafe
            // NO GL calls
            cubeArrangementModel.AdvanceTime(deltaTime);

            controller.Update((float)deltaTime);
        }

        private static unsafe void Window_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s].");

            // GL here
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);


            Gl.UseProgram(program);

            SetViewMatrix();
            SetProjectionMatrix();

            SetLightingUniforms();

            DrawPulsingCenterCube();

            DrawRevolvingCube();

            DrawUi();

            controller.Render();
        }

        private static void DrawUi()
        {
            ImGui.Begin("Lighting properties", ImGuiWindowFlags.AlwaysAutoResize);

            // Az anyagokat betoltjuk egy listaba
            if(ImGui.Combo("Material", ref SelectedMaterial, MaterialNames, MaterialNames.Length))
            {
                ApplyMaterial(Material.Presets[SelectedMaterial]);
            }
            ImGui.Text("(during the selection, the front face is white)");

            ImGui.Separator();

            // Az illuminacios komponensek
            ImGui.Text("Reflection parameters (R, G, B)");
            ImGui.SliderFloat3("Ambient", ref AmbientStrength, 0f, 1f);
            ImGui.SliderFloat3("Diffuse", ref DiffuseStrength, 0f, 1f);
            ImGui.SliderFloat3("Specular", ref SpecularStrength, 0f, 1f);
            ImGui.SliderFloat("Shininess", ref Shininess, 1f, 200f);

            ImGui.Separator();

            // A fenyek szine
            ImGui.Text("Light colors(R, G, B)");
            ImGui.SliderFloat3("Ambient light", ref AmbientColor, 0f, 1f);
            ImGui.SliderFloat3("Light source", ref LightColor, 0f, 1f);

            ImGui.Separator();

            if (ImGui.ColorEdit4("Front face color", ref FaceColor))
            {
                glCubeCentered.UpdateFaceColor(EditableFace,ToArray(FaceColor));
            }

            if (ImGui.Button("All the sides are white"))
            {
                FaceColor = new Vector4(1f, 1f, 1f, 1f);
                for (int face = 0; face < 6; face++)
                    glCubeCentered.UpdateFaceColor(face, ToArray(FaceColor));
            }

            ImGui.End();
        }

        private static void ApplyMaterial(Material material)
        {
            AmbientStrength = material.Ambient;
            DiffuseStrength = material.Diffuse;
            SpecularStrength = material.Specular;
            Shininess = material.Shininess;

            FaceColor = new Vector4(1f,1f, 1f, 1f);
            glCubeCentered.UpdateFaceColor(EditableFace, ToArray(FaceColor));
        }

        private static float[] ToArray(Vector4 v) => [v.X, v.Y, v.Z, v.W];

        private static void SetLightingUniforms()
        {
            SetUniform3(LightColorVariableName, LightColor);
            SetUniform3(AmbientColorVariablename, AmbientColor);
            SetUniform3(AmbientStrengthVariableName, AmbientStrength);
            SetUniform3(DiffuseStrengthVariableName, DiffuseStrength);
            SetUniform3(SpecularStrengthVariableName, SpecularStrength);

            SetUniform3(LightPositionVariableName, new Vector3(0f, 2f, 0f));

            var cameraPosition = cameraDescriptor.Position;
            SetUniform3(ViewPosVariableName, new Vector3(cameraPosition.X, cameraPosition.Y, cameraPosition.Z));

            SetUniform1(ShininessVariableName, Shininess);
        }

        private static void SetUniform3(string name, Vector3 value)
        {
            int location = Gl.GetUniformLocation(program, name);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }

            Gl.Uniform3(location, value.X, value.Y, value.Z);
            CheckError();
        }

        private static void SetUniform1(string name, float value)
        {
            int location = Gl.GetUniformLocation(program, name);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }

            Gl.Uniform1(location, value);
            CheckError();
        }
        
        private static unsafe void DrawRevolvingCube()
        {
            // set material uniform to metal

            Matrix4X4<float> diamondScale = Matrix4X4.CreateScale(0.25f);
            Matrix4X4<float> rotx = Matrix4X4.CreateRotationX((float)Math.PI / 4f);
            Matrix4X4<float> rotz = Matrix4X4.CreateRotationZ((float)Math.PI / 4f);
            Matrix4X4<float> rotLocY = Matrix4X4.CreateRotationY((float)cubeArrangementModel.DiamondCubeAngleOwnRevolution);
            Matrix4X4<float> trans = Matrix4X4.CreateTranslation(1f, 1f, 0f);
            Matrix4X4<float> rotGlobY = Matrix4X4.CreateRotationY((float)cubeArrangementModel.DiamondCubeAngleRevolutionOnGlobalY);
            Matrix4X4<float> modelMatrix = diamondScale * rotx * rotz * rotLocY * trans * rotGlobY;

            SetModelMatrix(modelMatrix);
            Gl.BindVertexArray(glCubeRotating.Vao);
            Gl.DrawElements(GLEnum.Triangles, glCubeRotating.IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);
        }

        private static unsafe void DrawPulsingCenterCube()
        {
            // set material uniform to rubber

            var modelMatrixForCenterCube = Matrix4X4.CreateScale((float)cubeArrangementModel.CenterCubeScale);
            SetModelMatrix(modelMatrixForCenterCube);
            Gl.BindVertexArray(glCubeCentered.Vao);
            Gl.DrawElements(GLEnum.Triangles, glCubeCentered.IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);
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

            float[] face1Color = [1.0f, 0.0f, 0.0f, 1.0f];
            float[] face2Color = [0.0f, 1.0f, 0.0f, 1.0f];
            float[] face3Color = [0.0f, 0.0f, 1.0f, 1.0f];
            float[] face4Color = [1.0f, 0.0f, 1.0f, 1.0f];
            float[] face5Color = [0.0f, 1.0f, 1.0f, 1.0f];
            float[] face6Color = [1.0f, 1.0f, 0.0f, 1.0f];

            glCubeCentered = GlCube.CreateCubeWithFaceColors(Gl, face1Color, face1Color, face3Color, face4Color, face5Color, face6Color);

            glCubeRotating = GlCube.CreateCubeWithFaceColors(Gl, face1Color, face1Color, face1Color, face1Color, face1Color, face1Color);
        }



        private static void Window_Closing()
        {
            glCubeCentered.ReleaseGlCube();
            glCubeRotating.ReleaseGlCube();
        }

        private static unsafe void SetProjectionMatrix()
        {
            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)Math.PI / 4f, 1024f / 768f, 0.1f, 100);
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
            var viewMatrix = Matrix4X4.CreateLookAt(cameraDescriptor.Position, cameraDescriptor.Target, cameraDescriptor.UpVector);
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
