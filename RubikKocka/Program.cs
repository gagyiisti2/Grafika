using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace RubikKocka
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

        private static void fillWithColor(float[] colorArray, ref int index, float[] color)
        {
            for (int i = 0; i < 4; i++)
            {
                colorArray[index++] = color[0];
                colorArray[index++] = color[1];
                colorArray[index++] = color[2];
                colorArray[index++] = color[3];
            }
        }
        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s]");

            Gl.Clear(ClearBufferMask.ColorBufferBit);

            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);
            int size = (3 * 3 * 4) * 3 * 3; // 3 oldal, mindegyiken 9 negyzet, 3 pontos koordinatakkal
            float[] vertexArray = new float[size];

            int cnt = 0;

            float dx = 0.13f;
            float dy = 0.08f;
            float vertY = -0.15f;

            // jobb oldali lap
            float x = 0.0f;
            float y = 0.0f;
            for (int i = 0; i < 3; i++) 
            {
                for (int j = 0; j < 3; j++) 
                {
                    x = j * dx;
                    y = j * dy + i * vertY;

                    float[][] dirs = new float[][]{
                      [0, 0],                              
                      [0, vertY],                          
                      [dx, vertY + dy],                   
                      [dx, dy]                             
                    };

                    for (int k = 0; k < 4; k++)
                    {
                        vertexArray[cnt++] = x + dirs[k][0];
                        vertexArray[cnt++] = y + dirs[k][1];
                        vertexArray[cnt++] = 0.0f;
                    }
                }
            }


            // 2. bal oldali lap
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    x = j * -dx;
                    y = j * dy  + i * vertY;

                    float[][] dirs = new float[][]{
                        [0, 0],
                        [0, vertY],
                        [-dx, vertY + dy],
                        [-dx, dy]
                    };

                    for (int k = 0; k < 4; k++)
                    {
                        vertexArray[cnt++] = x + dirs[k][0];
                        vertexArray[cnt++] = y + dirs[k][1];
                        vertexArray[cnt++] = 0.0f;
                    }
                }
            }

            // felso lap
            //dy = 0.088f;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    x = i * -dx + j * dx;
                    y = i * dy + j * dy;

                    float[][] dirs = new float[][]{
                        [0, 0],
                        [-dx, dy],
                        [0, 2 * dy],
                        [dx, dy]
                    };
                    for (int k = 0; k < 4; k++)
                    {
                        vertexArray[cnt++] = x + dirs[k][0];
                        vertexArray[cnt++] = y + dirs[k][1];
                        vertexArray[cnt++] = 0.0f;
                    }
                }
            }
            
            float[] red = new float[] {
                1.0f, 0.0f, 0.0f, 1.0f
            };
            float[] green = new float[] {
                0.0f, 1.0f, 0.0f, 1.0f
            };
            float[] blue = new float[] {
                0.0f, 0.0f, 1.0f, 1.0f
            };
            float[] orange = new float[] {
                1.0f, 0.5f, 0.0f, 1.0f
            };
            float[] purple = new float[] {
                0.5f, 0.0f, 0.5f, 1.0f
            };
            float[] yellow = new float[] {
                1.0f, 1.0f, 0.0f, 1.0f
            };
            
            float[] colorArray = new float[108 * 4];
            int index = 0;

            // jobb oldal
            fillWithColor(colorArray, ref index, red);
            fillWithColor(colorArray, ref index, green);
            fillWithColor(colorArray, ref index, blue);
            fillWithColor(colorArray, ref index, yellow);
            fillWithColor(colorArray, ref index, purple);
            fillWithColor(colorArray, ref index, orange);
            fillWithColor(colorArray, ref index, red);
            fillWithColor(colorArray, ref index, green);
            fillWithColor(colorArray, ref index, blue);
            
            // bal oldal
            fillWithColor(colorArray, ref index, green);
            fillWithColor(colorArray, ref index, red);
            fillWithColor(colorArray, ref index, blue);
            fillWithColor(colorArray, ref index, purple);
            fillWithColor(colorArray, ref index, orange);
            fillWithColor(colorArray, ref index, yellow);
            fillWithColor(colorArray, ref index, green);
            fillWithColor(colorArray, ref index, red);
            fillWithColor(colorArray, ref index, blue);

            // fent oldal
            fillWithColor(colorArray, ref index, yellow);
            fillWithColor(colorArray, ref index, orange);
            fillWithColor(colorArray, ref index, purple);
            fillWithColor(colorArray, ref index, green);
            fillWithColor(colorArray, ref index, blue);
            fillWithColor(colorArray, ref index, red);
            fillWithColor(colorArray, ref index, purple);
            fillWithColor(colorArray, ref index, green);
            fillWithColor(colorArray, ref index, blue);

            uint[] indexArray = new uint[27 * 6];

            // jobb oldal
            cnt = 0;
            for(uint i = 0; i < 27;  i++)
            {
               
               indexArray[cnt++] = i*4 + 0;
               indexArray[cnt++] = i*4 + 1;
               indexArray[cnt++] = i*4 + 2;
               indexArray[cnt++] = i*4 + 0;
               indexArray[cnt++] = i*4 + 2;
               indexArray[cnt++] = i*4 + 3;
            }

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
