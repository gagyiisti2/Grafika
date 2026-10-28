using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;
using Szeminarium;

namespace RubikKocka3D
{
    internal class Program
    {
        private static IWindow graphicWindow;

        private static GL Gl;

        private static List<ModelObjectDescriptor> cubes = new List<ModelObjectDescriptor>();
        private static List<Vector3> cubePositions = new List<Vector3>();

        private static CameraDescriptor camera = new CameraDescriptor();

        private static float currentAngle = 0f;
        private static float targetAngle = 0f;

        private const string ModelMatrixVariableName = "uModel";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
		layout (location = 1) in vec4 vCol;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

		out vec4 outCol;
        
        void main()
        {
			outCol = vCol;
            gl_Position = uProjection*uView*uModel*vec4(vPos.x, vPos.y, vPos.z, 1.0);
        }
        ";


        private static readonly string FragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;
		
		in vec4 outCol;

        void main()
        {
            FragColor = outCol;
        }
        ";

        private static uint program;

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Grafika szeminárium";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(500, 500);

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;
            graphicWindow.Closing += GraphicWindow_Closing;

            graphicWindow.Run();
        }

        private static void GraphicWindow_Closing()
        {
            foreach(var cube in cubes)
            {
                cube.Dispose();
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

            var inputContext = graphicWindow.CreateInput();
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }

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

        }
        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            switch (key)
            {
                case Key.W:
                    camera.IncreaseZXAngle();
                    break;
                case Key.A:
                    camera.IncreaseZYAngle();
                    break;
                case Key.S:
                    camera.DecreaseZXAngle();
                    break;
                case Key.D:
                    camera.DecreaseZYAngle();
                    break;
                case Key.Down:
                    camera.IncreaseDistance();
                    break;
                case Key.Up:
                    camera.DecreaseDistance();
                    break;
                case Key.Right:
                    camera.MoveRight();
                    break;
                case Key.Left:
                    camera.MoveLeft();
                    break;
                case Key.Space:
                    targetAngle += (float)(Math.PI / 2); // 90 fokkal kell elforgatni
                    break;
                case Key.Backspace:
                    targetAngle -= (float)(Math.PI / 2);
                    break;
            }
        }
        private static void GraphicWindow_Update(double deltaTime)
        {
            float speed = (float)Math.PI * 1.5f;
            if (currentAngle < targetAngle)
            {
                currentAngle += speed * (float)deltaTime;
                if(currentAngle > targetAngle) currentAngle = targetAngle; 
            }
            else if (currentAngle > targetAngle)
            {
                currentAngle -= speed * (float)deltaTime;
                if (currentAngle < targetAngle) currentAngle = targetAngle;
            }
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

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {

                        cubes.Add(ModelObjectDescriptor.CreateCube(Gl, GenerateColors(x, y, z)));
                        cubePositions.Add(new Vector3(x, y, z));
                    }
                }
            }

            float spacing = 1.1f;
            for (int i = 0; i < cubes.Count; i++)
            {
                var pos = cubePositions[i];
                var translation = Matrix4X4.CreateTranslation(pos.X * spacing, pos.Y * spacing, pos.Z * spacing);

                Matrix4X4<float> modelMatrix;

                if(pos.X == 1)
                {
                    var rotation = Matrix4X4.CreateRotationX(currentAngle);
                    modelMatrix = translation * rotation;   
                }
                else
                {
                    modelMatrix = translation;
                }

                SetMatrix(modelMatrix, ModelMatrixVariableName);
                DrawModelObject(cubes[i]);
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

        private static unsafe void SetMatrix(Matrix4X4<float> mx, string uniformName)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location == -1)
            {
                throw new Exception($"{ViewMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&mx);
            CheckError();
        }

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
        }

        private static float[] GenerateColors(int x, int y, int z) // 
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