using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace GrafikaHomework
{
    internal static class Program
    {
        private static IWindow graphicWindow;

        private static GL Gl;

        private static uint program;

        // 5. a shader stringek modositasa
        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
		layout (location = 1) in vec4 vCol;

		out vec4 outCol;
        
        void main()
        {
			outCol = vCol;
            gl_Position = vec4(vPos.x, vPos.y, vPos.z, 1.0);
        }
        ";
        // ha a locationt valtoztatom akkor nem tolti be a szint/formakat
        // Ha a poziciokat felcserelem (x-et y-al) : at
        private static readonly string FragmentShaderSource = @"
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
            windowOptions.Title = "1. szeminárium - háromszög";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(500, 500);

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
            graphicWindow.Render += GraphicWindow_Render;

            graphicWindow.Run();
        }

        private static void Check_Error(string message)
        {
            GLEnum error = Gl.GetError();
            if (error != GLEnum.NoError)
            {
                throw new Exception(message + ": " + Gl.GetError());
            }
        }

        private static void GraphicWindow_Load()
        {

            Gl = graphicWindow.CreateOpenGL();

            Gl.ClearColor(System.Drawing.Color.White);

            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Check_Error("Failed to create shaders");

            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            // 4. CompileShader, AttachShader, LinkProgram sorrendek, kihagyások

            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader); // kitorolve ezt a sort
                                       // Hiba: Error linking shader Attached fragment shader is not compiled.
                                       // tehat a shader nem jon letre hogy leforditsa GLSL-re
            Check_Error("Failed to compile shaders");
            program = Gl.CreateProgram();

            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program); // Ha az attach ele tettem: Error linking shader Link called without any attached shader objects.
            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Check_Error("Failed to finish the shader's job");
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(program)}");
            }

        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            // NO GL
            // make it threadsave
            //Console.WriteLine($"Update after {deltaTime} [s]");
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s]");

            Gl.Clear(ClearBufferMask.ColorBufferBit);

            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            float[] vertexArray = new float[] {
                 // jobb oldali lap
                 0.0f,  0.0f, 0.0f,
                 0.0f, -0.5f, 0.0f,
                 0.4f, -0.3f, 0.0f,
                 0.4f,  0.15f, 0.0f,
                 // bal oldali lap
                 0.0f,  0.0f, 0.0f,
                 0.0f, -0.5f, 0.0f,
                -0.4f, -0.3f, 0.0f,
                -0.4f,  0.15f, 0.0f,
                 // felso lap
                 0.0f,  0.0f, 0.0f,
                 0.4f,  0.15f, 0.0f,
                 0.0f,  0.3f, 0.0f,
                -0.4f,  0.15f, 0.0f
                 // 1. Hiba: 0.0f, +0.5f, 0.0f, kitorolni ezt a sort
                 // azt eredmenyezte, hogy nem negyszog, hanem haromszog alakja lett a kirajzolt formanak
                 // eltunt a pont ami a kozepponttol eszakra volt a canvas kozepen
            };

            float[] colorArray = new float[] {
                // jobb oldali lap
                1.0f, 0.0f, 0.0f, 1.0f,
                1.0f, 0.0f, 0.0f, 1.0f,
                1.0f, 0.0f, 0.0f, 1.0f,
                1.0f, 0.0f, 0.0f, 1.0f,
                // bal oldali lap
                0.0f, 1.0f, 0.0f, 1.0f,
                0.0f, 1.0f, 0.0f, 1.0f,
                0.0f, 1.0f, 0.0f, 1.0f,
                0.0f, 1.0f, 0.0f, 1.0f,
                // felso lap
                0.0f, 0.0f, 1.0f, 1.0f,
                0.0f, 0.0f, 1.0f, 1.0f,
                0.0f, 0.0f, 1.0f, 1.0f,
                0.0f, 0.0f, 1.0f, 1.0f
            };

            uint[] indexArray = new uint[] {
                0, 1, 2,
                0, 2, 3,

                4, 5, 6,
                4, 6, 7,
                
                8, 9, 10,
                8, 10,11
            };

            uint vertices = Gl.GenBuffer();
            // 2. BindBuffer kitorlese : lefutott a program, nem toltodott be a kep a bufferbe
            // igy a grafikus kartya nem fogja tudni hova toltse az adatot
            Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)vertexArray.AsSpan(), GLEnum.StaticDraw); // itt amiatt nem tolti be, mert ki van hagyva az a fuggveny, hibat szinten nem dob
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(0);
            Check_Error("Failed to complete the vertices buffer");

            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)colorArray.AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            // 3. Hivas parameter atallitasa
            Gl.EnableVertexAttribArray(1); // ha itt 2-re vagy barmi mas szamra irom at a szinek nem toltotnek be, es csak fekete negyszog rajzolodik ki
            // a 0 a poziciokat tolti be, az 1 meg a szineket
            Check_Error("Failed to complete the color buffer");

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)indexArray.AsSpan(), GLEnum.StaticDraw);

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);

            Gl.UseProgram(program);

            Gl.DrawElements(GLEnum.Triangles, (uint)indexArray.Length, GLEnum.UnsignedInt, null); // we used element buffer
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);
            Gl.BindVertexArray(vao);
            Check_Error("Failed to complete the indicies buffer");

            // always unbound the vertex buffer first, so no halfway results are displayed by accident
            Gl.DeleteBuffer(vertices);
            Gl.DeleteBuffer(colors);
            Gl.DeleteBuffer(indices);
            Gl.DeleteVertexArray(vao);
        }
    }
}
