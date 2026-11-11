using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.ComponentModel.Design;
using System.Numerics;
using Szeminarium;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace RubikKocka3D
{
    internal class Program
    {
        private static IWindow graphicWindow;

        private static GL Gl;

        private static List<ModelObjectDescriptor> cubes = new List<ModelObjectDescriptor>();
        private static List<RubikPiece> pieces = new List<RubikPiece>();

        private static CameraDescriptor camera = new CameraDescriptor();

        // Pozicio, animacio
        private static bool isAnimating = false;
        private static float currentAngle = 0f;
        private static float targetAngle = 0f;
        private static int animatingLayer = 0; // -1, 0, 1
        private static int rotationAxis = 1; // X:0, Y:1, Z:2
        private static float rotationDirection = 1f; // 1, -1
        private static Queue<Tuple<int, int, float>> shuffleQueue = new Queue<Tuple<int, int, float>>();
        private static bool isSolved = true;
        private static double totalTime = 0;

        // Nev konstansok
        private const string ModelMatrixVariableName = "uModel";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";
        private const string NormalMatrixVariableName = "uNormal";
        private const string LightColorVariableName = "lightColor";
        private const string LightPositionVariableName = "lightPos";
        private const string ViewPosVariableName = "viewPos";
        private const string ShininessVariableName = "shininess";

        // Fenyek
        private static Vector3 LightColor = new(1f, 1f, 1f);
        private static Vector3 LightPosition = new(3f, 4f, 5f);
        private static float Shininess = 50f;

        // Gui
        private static IInputContext inputContext = null!;
        private static ImGuiController controller = null!;

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
            gl_Position = uProjection*uView*uModel*vec4(vPos, 1.0);
            outNormal = uNormal*vNorm;
            outWorldPosition = vec3(uModel*vec4(vPos, 1.0));
        }
        ";


        private static readonly string FragmentShaderSource = @"
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

        private static uint program;

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Grafika szeminárium";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(1024, 768);

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;
            graphicWindow.Closing += GraphicWindow_Closing;

            graphicWindow.Run();
        }

        private static void GraphicWindow_Closing()
        {
            foreach (var piece in pieces)
            {
                piece.ModelDescriptor.Dispose();
            }
            Gl.DeleteProgram(program);
        }

        private static void GraphicWindow_Load()
        {
            Gl = graphicWindow.CreateOpenGL();

            Gl.ClearColor(System.Drawing.Color.White);

            Gl.Enable(EnableCap.CullFace);
            Gl.CullFace(TriangleFace.Back);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);

            inputContext = graphicWindow.CreateInput();
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }

            controller = new ImGuiController(Gl, graphicWindow, inputContext);

            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader);
            Gl.GetShader(fshader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
                throw new Exception("Fragment shader failed to compile: " + Gl.GetShaderInfoLog(fshader));

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);

            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(program)}");
            }

            Gl.UseProgram(program);

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {

                        var model = ModelObjectDescriptor.CreateCube(Gl, GenerateColors(x, y, z));
                        var position = new Vector3D<float>(x, y, z);
                        pieces.Add(new RubikPiece(position, model));
                    }
                }
            }
        }

        private static void Start_Animation(int axis, int layer, float dir)
        {
            if (isAnimating) return;

            isAnimating = true;
            rotationAxis = axis;
            animatingLayer = layer;
            rotationDirection = dir;
            currentAngle = 0f;
            targetAngle = (float)(Math.PI / 2) * dir;
        }
        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (ImGui.GetIO().WantCaptureKeyboard)
                return;
            switch (key)
            {
                /// Y tengely ->:
                case Key.Q:
                    Start_Animation(1, -1, 1f);
                    break;
                case Key.A:
                    Start_Animation(1, 0, 1f);
                    break;
                case Key.Z:
                    Start_Animation(1, 1, 1f);
                    break;
                /// Y tengely <-:
                case Key.W:
                    Start_Animation(1, -1, -1f);
                    break;
                case Key.S:
                    Start_Animation(1, 0, -1f);
                    break;
                case Key.X:
                    Start_Animation(1, 1, -1f);
                    break;
                /// X tengely ->:
                case Key.U:
                    Start_Animation(0, -1, 1f);
                    break;
                case Key.I:
                    Start_Animation(0, 0, 1f);
                    break;
                case Key.O:
                    Start_Animation(0, 1, 1f);
                    break;
                /// X tengely <-:
                case Key.J:
                    Start_Animation(0, -1, -1f);
                    break;
                case Key.K:
                    Start_Animation(0, 0, -1f);
                    break;
                case Key.L:
                    Start_Animation(0, 1, -1f);
                    break;
                /// r - random keveres:
                case Key.R:
                    StartShuffle();
                    break;
                /// Kocka forgatasa, megnezese iranyokbol:
                case Key.E:
                    camera.IncreaseZXAngle();
                    break;
                case Key.D:
                    camera.DecreaseZXAngle();
                    break;
                case Key.Down:
                    camera.IncreaseDistance();
                    break;
                case Key.Up:
                    camera.DecreaseDistance();
                    break;
                case Key.Right:
                    camera.DecreaseZYAngle();
                    break;
                case Key.Left:
                    camera.IncreaseZYAngle();
                    break;
            }
        }

        private static void StartShuffle()
        {
            if (shuffleQueue.Count > 0 || isAnimating)
                return;

            isSolved = false;
            Random r = new Random();
            for (int i = 0; i < 30; i++)
            {
                int axis = r.Next(0, 2);
                int layer = r.Next(-1, 2);
                float dir = r.Next(0, 2) == 0 ? -1f : 1f;
                shuffleQueue.Enqueue(new Tuple<int, int, float>(axis, layer, dir));
            }
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            totalTime += deltaTime;
            if (isAnimating)
            {
                float speed = (float)Math.PI * 1.5f;
                currentAngle += speed * (float)deltaTime * rotationDirection;

                if (rotationDirection > 0 && currentAngle >= targetAngle ||
                    rotationDirection < 0 && currentAngle <= targetAngle)
                {
                    currentAngle = targetAngle;
                    FinalizeRotation();

                    if (shuffleQueue.Count > 0)
                    {
                        var next = shuffleQueue.Dequeue();
                        Start_Animation(next.Item1, next.Item2, next.Item3);
                    }
                    else
                    {
                        CheckIfSolved();
                    }
                }
            }
            else if (shuffleQueue.Count > 0)
            {
                // elso keveres
                var next = shuffleQueue.Dequeue();
                Start_Animation(next.Item1, next.Item2, next.Item3);
            }

            controller.Update((float)deltaTime);
        }

        private static void CheckIfSolved()
        {
            bool solved = true;
            foreach (var piece in pieces)
            {
                // Ha barhol nincs a helyen false
                if (Math.Abs(piece.CurrentPosition.X - piece.OriginalPosition.X) > 0.1f ||
                    Math.Abs(piece.CurrentPosition.Y - piece.OriginalPosition.Y) > 0.1f ||
                    Math.Abs(piece.CurrentPosition.Z - piece.OriginalPosition.Z) > 0.1f)
                {
                    solved = false;
                    break;
                }
            }
            isSolved = solved;
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);
            Gl.UseProgram(program);

            var viewMatrix = Matrix4X4.CreateLookAt(camera.Position, camera.Target, camera.UpVector);
            SetMatrix(viewMatrix, ViewMatrixVariableName);

            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)(Math.PI / 2), 1024f / 768f, 0.1f, 100f);
            SetMatrix(projectionMatrix, ProjectionMatrixVariableName);

            SetLightingUniforms();

            var globalVictoryAnim = Matrix4X4<float>.Identity;
            if (isSolved && !isAnimating && shuffleQueue.Count == 0)
            {
                float scale = 1.0f + 0.15f * (float)Math.Sin(totalTime * 3.0);
                var scaleMatrix = Matrix4X4.CreateScale(scale);
                var rotMatrix = Matrix4X4.CreateRotationY((float)totalTime);
                globalVictoryAnim = scaleMatrix * rotMatrix;
            }

            float spacing = 1.1f;
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];

                // Eltolas a megfelelo pozicioba
                var translation = Matrix4X4.CreateTranslation(
                    piece.CurrentPosition.X * spacing,
                    piece.CurrentPosition.Y * spacing,
                    piece.CurrentPosition.Z * spacing
                );

                var animRotation = Matrix4X4<float>.Identity;

                if (isAnimating)
                {
                    bool inLayerY = (rotationAxis == 1 && Math.Round(piece.CurrentPosition.Y) == animatingLayer);
                    bool inLayerX = (rotationAxis == 0 && Math.Round(piece.CurrentPosition.X) == animatingLayer);
                    if (inLayerY)
                    {
                        animRotation = Matrix4X4.CreateRotationY(currentAngle);
                    }else if (inLayerX)
                    {
                        animRotation = Matrix4X4.CreateRotationX(currentAngle);
                    }
                } 

                // Orientacioval, es eltolassal egyutt kirajzolas
                var modelMatrix = piece.OrientationMatrix * translation * animRotation * globalVictoryAnim;

                SetModelMatrix(modelMatrix);
                DrawModelObject(piece.ModelDescriptor);
            }
            DrawUi();
            controller.Render();
        }

        private static void DrawUi()
        {
            ImGui.SetNextWindowPos(new Vector2(10,10), ImGuiCond.FirstUseEver);
            ImGui.Begin("Navigation", ImGuiWindowFlags.AlwaysAutoResize);

            // Feny szine:
            ImGui.Text("Light color");
            ImGui.SliderFloat("R", ref LightColor.X, 0f, 1f);
            ImGui.SliderFloat("G", ref LightColor.Y, 0f, 1f);
            ImGui.SliderFloat("B", ref LightColor.Z, 0f, 1f);

            ImGui.Separator();

            // Feny pozicio
            ImGui.Text("Light position");
            ImGui.InputFloat("X", ref LightPosition.X, 0f, 0f, "%.2f");
            ImGui.InputFloat("Y", ref LightPosition.Y, 0f, 0f, "%.2f");
            ImGui.InputFloat("Z", ref LightPosition.Z, 0f, 0f, "%.2f");

            ImGui.Separator();

            // Forgasok:
            ImGui.BeginDisabled(isAnimating || shuffleQueue.Count > 0);

            DrawRotationButtons("Around Y axis", 1, ["Bottom", "Middle", "Top"]);
            ImGui.Spacing();
            DrawRotationButtons("Around X axis", 0, ["Left", "Middle", "Right"]);
            ImGui.Spacing();

            if (ImGui.Button("Shuffle"))
            {
                StartShuffle();
            }

            ImGui.EndDisabled();

            if (isSolved && !isAnimating && shuffleQueue.Count == 0)
            {
                ImGui.Text("Solved!");
            }

            ImGui.End();
        }

        private static void DrawRotationButtons(string title, int axis, string[] layerNames)
        {
            ImGui.Text(title);
            for (int layer = -1; layer <= 1; layer++)
            {
                ImGui.Text(layerNames[layer + 1]);
                ImGui.SameLine(90);

                if (ImGui.Button($"+90##{axis}_{layer}_p")) // ## utan azonosito
                    Start_Animation(axis, layer, 1f);

                ImGui.SameLine();

                if (ImGui.Button($"-90##{axis}_{layer}_n"))
                    Start_Animation(axis, layer, -1f);
            }
        }

        private static void FinalizeRotation()
        {
            isAnimating = false;
            var finalRot = Matrix4X4<float>.Identity;

            if (rotationAxis == 1) finalRot = Matrix4X4.CreateRotationY(targetAngle);
            if (rotationAxis == 0) finalRot = Matrix4X4.CreateRotationX(targetAngle);

            foreach (var piece in pieces)
            {
                bool inLayer = ((rotationAxis == 1 && Math.Round(piece.CurrentPosition.Y) == animatingLayer) || 
                                (rotationAxis == 0 && Math.Round(piece.CurrentPosition.X) == animatingLayer));
                if (inLayer)
                {
                    piece.OrientationMatrix = piece.OrientationMatrix * finalRot; // orientaciot felulirjuk

                    var pos = piece.CurrentPosition;
                    var newPos = Vector3D.Transform(pos, finalRot);

                    piece.CurrentPosition = new Vector3D<float>(
                        (float)Math.Round(newPos.X),
                        (float)Math.Round(newPos.Y),
                        (float)Math.Round(newPos.Z)
                    );
                }
            }
        }

        private static unsafe void DrawModelObject(ModelObjectDescriptor modelObject)
        {
            Gl.BindVertexArray(modelObject.Vao);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, modelObject.Indices);
            Gl.DrawElements(PrimitiveType.Triangles, modelObject.IndexArrayLength, DrawElementsType.UnsignedInt, null);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);
            Gl.BindVertexArray(0);
        }

        private static unsafe void SetModelMatrix(Matrix4X4<float> modelMatrix)
        {
            SetMatrix(modelMatrix, ModelMatrixVariableName);

            var modelMatrixWithoutTranslation = new Matrix4X4<float>(modelMatrix.Row1, modelMatrix.Row2, modelMatrix.Row3, modelMatrix.Row4);
            modelMatrixWithoutTranslation.M41 = 0;
            modelMatrixWithoutTranslation.M42 = 0;
            modelMatrixWithoutTranslation.M43 = 0;
            modelMatrixWithoutTranslation.M44 = 1;

            Matrix4X4<float> modelInvers;
            Matrix4X4.Invert<float>(modelMatrixWithoutTranslation, out modelInvers);
            Matrix3X3<float> normalMatrix = new Matrix3X3<float>(Matrix4X4.Transpose(modelInvers));
            
            int location = Gl.GetUniformLocation(program, NormalMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{NormalMatrixVariableName} uniform not found on shader.");
            }
            Gl.UniformMatrix3(location, 1, false, (float*)&normalMatrix);
            CheckError();
        }

        private static unsafe void SetMatrix(Matrix4X4<float> mx, string uniformName)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location == -1)
            {
                throw new Exception($"{uniformName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&mx);
            CheckError();
        }

        private static void SetLightingUniforms()
        {
            SetUniform3(LightColorVariableName, LightColor);
            SetUniform3(LightPositionVariableName, LightPosition);

            var cameraPosition = camera.Position;
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

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
        }

        private static float[] GenerateColors(int x, int y, int z) 
        {
            float[] colors = new float[96];
            for (int i = 0; i < 96; i++)
            {
                colors[i] = (i % 4 == 3) ? 1.0f : 0.2f; // szurke szin
            }

            // Felso
            if (y == 1) SetFaceColor(colors, 0, 1.0f, 0.0f, 0.0f); // piros
            // Elulso
            if (z == 1) SetFaceColor(colors, 1, 0.0f, 1.0f, 0.0f); // zold
            // Bal
            if (x == -1) SetFaceColor(colors, 2, 0.0f, 0.0f, 1.0f); // kek
            // Also
            if (y == -1) SetFaceColor(colors, 3, 1.0f, 0.0f, 1.0f); // rozsaszin
            // Hatso
            if (z == -1) SetFaceColor(colors, 4, 0.0f, 1.0f, 1.0f); // vilagoskek
            // Jobb
            if (x == 1) SetFaceColor(colors, 5, 1.0f, 1.0f, 0.0f); // sarga

            return colors;
        }

        private static unsafe void SetFaceColor(float[] colors, int faceIndex, float r, float g, float b)
        {
            int start = faceIndex * 16;
            for (int i = 0; i < 4; i++)
            {
                colors[start + i * 4 + 0] = r;
                colors[start + i * 4 + 1] = g;
                colors[start + i * 4 + 2] = b;
                colors[start + i * 4 + 3] = 1.0f;
            }
        }
    }
}